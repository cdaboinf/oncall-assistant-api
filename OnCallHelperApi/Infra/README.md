# OnCall Helper API — AWS deploy runbook

How to ship C#/Lambda changes to AWS. The API runs as a **container image on
Lambda**, fronted by an **API Gateway HTTP API**, with **CloudFront** on a custom
domain (`api.oncallhelper.com`) in front so networks that block
`*.execute-api.amazonaws.com` still allow it. Terraform manages all the AWS
resources; the application code ships as a Docker image in ECR.

## Key facts

| Thing | Value |
|---|---|
| AWS account | `848362861133` |
| Region | `us-east-1` |
| Lambda function | `oncall-helper-api-prod` |
| ECR repo | `848362861133.dkr.ecr.us-east-1.amazonaws.com/oncall-helper-api-prod` |
| **Public API URL** | `https://api.oncallhelper.com` (use this) |
| API Gateway origin | `https://a4ak402qf5.execute-api.us-east-1.amazonaws.com` (behind CloudFront) |
| CloudFront domain | `d22bqsx5mg3g9t.cloudfront.net` (distribution `E5M5693PBGTZ`) |
| ACM cert | `*.oncallhelper.com`, DNS-validated, **free**, auto-renews, in `us-east-1` |
| Lambda arch | `x86_64` (Dockerfile is pinned to `linux/amd64` to match) |
| Auth0 audience | `http://localhost:5172` (API identifier — not a URL) |

### Costs
Only the **domain** (~$12–20/yr, GoDaddy) is a real cost. The ACM certificate is
**free**, and CloudFront usage falls within the AWS free tier (1 TB out + 10M
requests/month). No fixed monthly fees.

All commands below are run **from this `Infra/` folder**. The Docker build
context is the project root (`..`), where the `Dockerfile` and `.csproj` live.

## The golden rule

**Terraform only redeploys the Lambda when `image_tag` changes.** Pushing a new
image over the *same* tag will NOT update the running function. Every code
release = **new tag + bump `image_tag` in `terraform.tfvars`**.

---

## Deploy new C#/Lambda code

```bash
# 0. (once per shell) point at the right AWS account if you use profiles
export AWS_PROFILE=default        # or your profile; skip if default already works
export ACCOUNT_ID=848362861133
export REGION=us-east-1
export REPO=$ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com/oncall-helper-api-prod
```

```bash
# 1. Pick a NEW tag (must differ from the current image_tag). A git sha is handy:
export TAG=$(git rev-parse --short HEAD)   # or e.g. v3
echo "building $REPO:$TAG"
```

```bash
# 2. Log in to ECR (token lasts ~12h)
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com
```

```bash
# 3. Build for x86_64 WITHOUT provenance attestation, and push (build+push in one step).
#    --provenance=false is REQUIRED: Lambda rejects the OCI image-index that buildx adds by default.
docker buildx build --platform linux/amd64 --provenance=false -t $REPO:$TAG --push ..
```

4. Edit **`terraform.tfvars`** → set `image_tag = "<the TAG you just pushed>"`.

```bash
# 5. Review exactly what will change
terraform plan -out=tfplan
```

```bash
# 6. Apply only the reviewed plan (no-op if plan was empty)
terraform apply tfplan
```

```bash
# 7. Verify: no token -> 401 (auth is enforced in prod)
curl -s -o /dev/null -w "%{http_code}\n" https://a4ak402qf5.execute-api.us-east-1.amazonaws.com/api/incidents
```

```bash
# 8. Watch logs if needed
aws logs tail /aws/lambda/oncall-helper-api-prod --region us-east-1 --follow
```

---

## Config-only changes (no code rebuild)

If you only changed Terraform (env vars, IAM, CORS origins, etc.) and **not** the
C# code, skip the Docker steps:

```bash
terraform plan -out=tfplan && terraform apply tfplan
```

Env vars set on the Lambda (see `lambda.tf`): `Auth__Enabled`, `Auth0__Authority`,
`Auth0__Audience`, `Cors__AllowedOrigins__*`, and the SSM parameter-name pointers.

## CORS

Origins come from `cors_allowed_origins` in `terraform.tfvars`:
- `[]` (empty) → **allow any origin** (call the API from anywhere).
- Add origins to lock it down, e.g. `cors_allowed_origins = ["https://oncall.example.com"]`.

No code change needed either way — the app reads these at startup.

## Secrets (never in git or the image)

The Mongo connection string, Mongo database, and OpenAI key live in **SSM
Parameter Store** (SecureString), read by the Lambda at startup. IAM in `iam.tf`
grants read access. To rotate a value:

```bash
aws ssm put-parameter --overwrite --type SecureString --region us-east-1 \
  --name "/oncall-helper/prod/OpenAI__ApiKey" --value "sk-..."
```

Parameter names: `/oncall-helper/prod/OpenAI__ApiKey`,
`/oncall-helper/prod/Mongo__ConnectionString`, `/oncall-helper/prod/Mongo__Database`.
(No redeploy needed — the Lambda picks up the new value on its next cold start.)

## Auth0

- The Lambda validates JWTs using `Auth0__Authority` + `Auth0__Audience`. Tokens
  the SPA issues (audience `http://localhost:5172`) are accepted as-is in prod.
- To sign in from a **hosted UI**, add that UI's origin to the SPA app's
  **Allowed Callback URLs / Logout URLs / Web Origins** in the Auth0 dashboard.
- The SPA must be authorized for this API under **APIs → OnCallHelperApi →
  Application Access** (User-delegated Access).

## Custom domain via CloudFront (api.oncallhelper.com)

CloudFront fronts the API on `https://api.oncallhelper.com` so networks that block
`*.execute-api.amazonaws.com` still allow it. **Already deployed** — this section
is for reference / rebuilding from scratch.

**Terraform (`cloudfront.tf`, plus `providers.tf` / `variables.tf`):**
- `provider "aws"` alias **`us_east_1`** — CloudFront certs must live in us-east-1.
- `aws_acm_certificate.cf` — **free** wildcard cert `*.oncallhelper.com`, DNS-validated.
- `aws_acm_certificate_validation.cf` — waits until the cert is ISSUED (validation
  record is added manually in GoDaddy since DNS is external).
- `aws_cloudfront_distribution.api` — origin = the API Gateway; forwards the
  Authorization header + query strings and disables caching via AWS managed
  policies (`AllViewerExceptHostHeader` + `CachingDisabled`); alias
  `api.oncallhelper.com`; `PriceClass_100` (cheapest).
- Outputs: `acm_validation_record`, `cloudfront_domain`, `api_public_url`.

DNS is at **GoDaddy**, so cert validation + the `api` record are added there
manually. Two-step because the cert must be ISSUED before CloudFront can attach it:

**Step 1 — create the cert and get the validation record:**
```bash
terraform apply -target=aws_acm_certificate.cf
terraform output acm_validation_record
```
In **GoDaddy DNS**, add that as a **CNAME** — Name = the record name with
`.oncallhelper.com` stripped off (e.g. `_abc123` or `_abc123.api`), Value = the
`value` shown. Wait a few minutes for ACM to show **Issued**:
```bash
aws acm list-certificates --region us-east-1 --query "CertificateSummaryList[?DomainName=='*.oncallhelper.com']"
```

**Step 2 — create CloudFront and get the api record:**
```bash
terraform apply
terraform output cloudfront_domain
```
In **GoDaddy DNS**, add a **CNAME**: Name = `api`, Value = the `cloudfront_domain`
(e.g. `d123.cloudfront.net`). CloudFront takes ~5–15 min to deploy. Then:
```bash
curl -sI https://api.oncallhelper.com/api/incidents   # expect HTTP/2 401
```

**Point the UI at it:** `.env.production` already uses `https://api.oncallhelper.com`
— rebuild/repush the UI image (see UI deploy) once the domain resolves.

Notes: Auth0 audience stays `http://localhost:5172` (unchanged). The CloudFront
behavior forwards the Authorization header and disables caching (managed policies),
so tokens pass through and responses aren't cached.

## Hosted MCP server (mcp.oncallhelper.com)

The `OnCallHelperMcp` project in HTTP mode, so LLM clients (Claude on the web/desktop/phone) can use the
incident tools without anything running on your laptop. Defined in **`mcp.tf`**; same shape as the API
(container image on Lambda → API Gateway HTTP API → CloudFront), but with its own ECR repo, IAM role
(can read only the three MCP parameters), log group, gateway and distribution. It reuses the existing
wildcard certificate and calls the API at `api.oncallhelper.com` with an Auth0 machine-to-machine token.

**Who can connect:** only requests carrying an Auth0 access token for audience
`https://mcp.oncallhelper.com` whose `sub` is in `Mcp__AllowedUserIds`. No token → 401; valid token for
another user → 403; empty allowlist → everyone rejected.

| Thing | Value |
|---|---|
| Lambda / log group | `oncall-helper-mcp-prod` / `/aws/lambda/oncall-helper-mcp-prod` |
| ECR repo | `848362861133.dkr.ecr.us-east-1.amazonaws.com/oncall-helper-mcp-prod` |
| Public URL (= Auth0 API identifier) | `https://mcp.oncallhelper.com` |
| SSM parameters | `/oncall-helper/prod/Mcp__Auth0ClientId`, `…/Mcp__Auth0ClientSecret`, `…/Mcp__AllowedUserIds` |

### One-time Auth0 setup (tenant `dev-k0sl1xaa1o87ofbn`)
1. **API** `OnCall Helper MCP`, identifier `https://mcp.oncallhelper.com`, RS256, *Allow Offline Access* on.
2. **Application** `Claude connector` (Regular Web Application): callback URLs
   `https://claude.ai/api/mcp/auth_callback` and `https://claude.com/api/mcp/auth_callback`;
   grant types Authorization Code + Refresh Token.
3. **Settings → Advanced:** turn on *Resource Parameter Compatibility Profile* and *Include Issuer in
   Authorization Responses*. Leave *Dynamic Client Registration* off.
4. **Database connection → Settings:** *Disable Sign Ups* (create users in the dashboard instead).
5. The existing machine-to-machine app (used for local calls to the API) must be authorized for
   `OnCallHelperApi`. Its client id/secret go into SSM below.

### First deploy
All commands run from this `Infra/` folder. Each step that changes AWS is preceded by `plan`.

```bash
export ACCOUNT_ID=848362861133 REGION=us-east-1
export MCP_REPO=$ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com/oncall-helper-mcp-prod
```

**1. Create only the (empty) image repository.** The Lambda cannot exist before its image does.
```bash
terraform plan -target=aws_ecr_repository.mcp
terraform apply -target=aws_ecr_repository.mcp
```

**2. Create the three SSM parameters** (values are yours; `read -s` keeps the secret out of your
shell history and the screen). These are **zsh** commands (the macOS default shell): the prompt goes
inside the variable name as `"V?prompt"`. In bash, use `read -s -p "prompt: " V` instead.
At each prompt, paste the value and press Enter; nothing is echoed for the `-s` ones.
```zsh
read -s "V?M2M client id: "; echo
aws ssm put-parameter --region $REGION --type SecureString --name /oncall-helper/prod/Mcp__Auth0ClientId --value "$V"
read -s "V?M2M client secret: "; echo
aws ssm put-parameter --region $REGION --type SecureString --name /oncall-helper/prod/Mcp__Auth0ClientSecret --value "$V"
read "V?Allowed Auth0 user id (e.g. auth0|... or google-oauth2|...): "
aws ssm put-parameter --region $REGION --type SecureString --name /oncall-helper/prod/Mcp__AllowedUserIds --value "$V"
unset V
```
A successful call prints `"Version": 1`. An empty value is rejected by AWS ("length greater than or
equal to 1"), which usually means the `read` prompt did not run (wrong shell syntax).
Several users: comma-separate the ids in the last parameter.

**3. Build for x86_64 and push** (`--provenance=false` is required, same as the API). The build context
is the MCP project folder:
```bash
export TAG=$(git rev-parse --short HEAD)
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com
docker buildx build --platform linux/amd64 --provenance=false -t $MCP_REPO:$TAG --push ../../OnCallHelperMcp
```

**4. Set `mcp_image_tag = "<TAG>"` in `terraform.tfvars`, review, apply.** The plan must say
`12 to add, 0 to change, 0 to destroy`; if it shows any change to an existing resource, stop.
```bash
terraform plan -out=tfplan
terraform apply tfplan
```

**5. DNS (GoDaddy, manual).** Add a **CNAME**: Name = `mcp`, Value = the output below. Do **not** use
the API's CloudFront domain. CloudFront takes ~5–15 min to deploy.
```bash
terraform output mcp_cloudfront_domain
```

**6. Verify** (no token must be rejected, and the sign-in metadata must be public). Use **POST**, as a
real MCP client does: a plain GET returns 405 in stateless mode, before the auth check.
```bash
dig +short CNAME mcp.oncallhelper.com
curl -si -X POST https://mcp.oncallhelper.com/ \
  -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}' \
  | head -5        # expect HTTP 401 + WWW-Authenticate: Bearer resource_metadata="https://mcp.oncallhelper.com/.well-known/..."
curl -s https://mcp.oncallhelper.com/.well-known/oauth-protected-resource   # JSON: resource + authorization_servers
aws logs tail /aws/lambda/oncall-helper-mcp-prod --region $REGION --since 10m
```
The `resource_metadata` URL must start with `https://mcp.oncallhelper.com`. If it shows an
`execute-api.amazonaws.com` host, the host-rewrite in `HttpHost.cs` is missing (CloudFront replaces the
Host header with the API Gateway hostname).

To test before DNS exists, send the same requests to the CloudFront domain with
`--resolve mcp.oncallhelper.com:443:$(dig +short <mcp_cloudfront_domain> | head -1)`.

**7. Connect Claude.** In Claude: *Settings → Connectors → Add custom connector*, URL
`https://mcp.oncallhelper.com`; under *Advanced settings* enter the **Client ID and Client Secret of the
`Claude connector` Auth0 application**. Sign in with your Auth0 user when prompted. Start with a
read-only request ("search my last 3 incidents") before using `create_incident`/`update_incident`.
Menu labels may differ slightly between Claude versions.

### Later releases
Push a new tag (steps 3–4). Rotating a secret: `aws ssm put-parameter --overwrite ...`; the Lambda picks
it up on its next cold start. **Rollback:** set `mcp_image_tag` back to the previous tag, plan, apply.

### MCP troubleshooting
| Symptom | Cause / fix |
|---|---|
| `plan` fails: "Push the MCP image … set mcp_image_tag" | `mcp_image_tag` is still `"unset"`. Do steps 1–3, then set the tag. |
| `https://mcp.oncallhelper.com` 403 / "Bad request" from CloudFront | CNAME missing, or it points at the API's CloudFront domain. Use `terraform output mcp_cloudfront_domain`. |
| Sign-in fails with an audience/userinfo error | *Resource Parameter Compatibility Profile* is off (Auth0 step 3), or the API identifier isn't exactly `https://mcp.oncallhelper.com` (no trailing slash). |
| Sign-in works but every request is 403 | Your `sub` is not in `Mcp__AllowedUserIds` (check the exact `auth0\|…` id), or the parameter is empty. |
| Every request 401 after sign-in | Token audience/issuer mismatch: check `MCP_PUBLIC_URL`/`MCP_AUTHORITY` in the Lambda env and the Auth0 API identifier. |
| Tools return "Auth0 rejected the token request" | Wrong M2M client id/secret in SSM, or the M2M app isn't authorized for `OnCallHelperApi`. |
| Lambda fails to start | `aws logs tail /aws/lambda/oncall-helper-mcp-prod`. A missing `MCP_PUBLIC_URL`/`MCP_AUTHORITY` or an unreadable SSM parameter stops startup with a clear message. |

## MongoDB Atlas vector index (required for triage)

Similar-incident search (`/api/oncall/analyze`, `/api/incidents/similar`) runs an Atlas
`$vectorSearch` and **the app never creates the index** (the migrations in `Program.cs` are
commented out). It lives only in Atlas, so if it is deleted, searches silently return `[]`
and triage falls back to generic advice (no error is raised).

| Setting | Value |
|---|---|
| Index type | **Vector Search** (Atlas Search tab, not the regular "Indexes" tab) |
| Index name | `vector_index` (hard-coded in `IncidentRepository.FindSimilarAsync`) |
| Database | the one in `Mongo__Database` (SSM `/oncall-helper/prod/Mongo__Database`) |
| Collection | `incidents` |
| Embeddings | "Bring your own embeddings" (the API generates them with OpenAI `text-embedding-3-small`) |

Definition (JSON editor):

```json
{
  "fields": [
    {
      "type": "vector",
      "path": "Embedding",
      "numDimensions": 1536,
      "similarity": "cosine"
    }
  ]
}
```

Create it in Atlas: cluster → **Atlas Search** → **Create Search Index** → **Vector Search** →
JSON Editor. Wait until the status is **Active/Ready**.

Check it works (any running API; use a real incident topic from your history):

```bash
curl -s -X POST http://localhost:5172/api/incidents/similar \
  -H 'Content-Type: application/json' \
  -d '{"description":"credit card payments not reflecting","top":3}'
```

An empty `[]` means the index is missing/not ready, or incidents have no `Embedding`. In the
second case, `POST /api/incidents/rebuild-embeddings` regenerates them (writes to the database
and uses OpenAI credits). If you change the embedding model, `numDimensions` must change too.

## Rollback

Set `image_tag` back to the previous tag in `terraform.tfvars`, then:

```bash
terraform plan -out=tfplan && terraform apply tfplan
```

(Old image tags remain in ECR unless you delete them.)

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `InvalidParameterValueException: ... media type ... is not supported` | buildx provenance attestation. Rebuild with **`--provenance=false`** (step 3). |
| Lambda starts but crashes with exec/format error | Image built for arm64. Dockerfile is pinned to `linux/amd64`; always keep `--platform linux/amd64`. |
| `terraform apply` shows no Lambda change after a new push | You reused the same `image_tag`. Bump it (golden rule). |
| ECR push `denied` | Login token expired (~12h). Re-run step 2. |
| Triage works but gives generic answers, `similarIncidents` is empty | The Atlas `vector_index` is missing or not Ready. See **MongoDB Atlas vector index**. |
| API returns 401 | Expected without a valid Auth0 Bearer token — auth is enforced in prod. |
| Terraform state | Local (`terraform.tfstate` in this folder, git-ignored, contains secrets — do not commit or share). |
