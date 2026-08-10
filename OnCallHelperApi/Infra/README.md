# OnCall Helper API — AWS deploy runbook

How to ship C#/Lambda changes to AWS. The API runs as a **container image on
Lambda**, fronted by an **API Gateway HTTP API**. Terraform manages all the AWS
resources; the application code ships as a Docker image in ECR.

## Key facts

| Thing | Value |
|---|---|
| AWS account | `848362861133` |
| Region | `us-east-1` |
| Lambda function | `oncall-helper-api-prod` |
| ECR repo | `848362861133.dkr.ecr.us-east-1.amazonaws.com/oncall-helper-api-prod` |
| Live API URL | `https://a4ak402qf5.execute-api.us-east-1.amazonaws.com` |
| Lambda arch | `x86_64` (Dockerfile is pinned to `linux/amd64` to match) |
| Auth0 audience | `http://localhost:5172` (API identifier — not a URL) |

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
