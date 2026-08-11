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
| API returns 401 | Expected without a valid Auth0 Bearer token — auth is enforced in prod. |
| Terraform state | Local (`terraform.tfstate` in this folder, git-ignored, contains secrets — do not commit or share). |
