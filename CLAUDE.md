# Working agreement for this repo

This is a **live production app** (API on AWS Lambda, UI, MCP server). The owner wants to understand and
approve each change. These rules override any default habit of "just doing it".

## Ask before changing anything
- **Do not write or edit code, config, or docs until the owner has said yes to that specific change.**
  Discussion, design, questions and research are not permission to build.
- First say what you intend to change (which files, why, what it affects), then wait for an explicit OK.
- If a decision is still open, or the owner's answers are unclear or contradict each other, **ask**.
  Do not pick an interpretation and start building.
- Work one step at a time, in plain language. Do not batch a whole feature into one pass.
- When the owner asks a question, answer it. Do not start implementing.

## Never without an explicit yes at that moment
- `git commit`, `git push`.
- Any deploy: pushing images to ECR, `terraform apply`, changing Lambda/API Gateway/CloudFront/SSM.
- Anything outside this repo: Claude Desktop config, Auth0, GoDaddy DNS, MongoDB Atlas. Give the owner
  exact steps instead; they perform them.
- `rebuild-embeddings` or anything else that writes to the production database or spends OpenAI credits.
- Approval for one action does not carry over to the next one.

## Be precise, not confident
- Do not assume infrastructure exists (hostnames, DNS records, certificates, Auth0 objects, SSM parameters).
  Verify it read-only, or ask.
- For third-party dashboards, quote the documented steps. If something is unverified, say so plainly.
- Say what was actually tested and what was not.

## Secrets
- Never print, log, commit, or ask for secret values (client secrets, API keys, connection strings, tokens).
- The owner places secrets themselves (SSM, local config). Show masked values or lengths only.

## Project facts
- Deploy runbook: `OnCallHelperApi/Infra/README.md` (manual: image to ECR, bump `image_tag`, plan, apply).
- The existing UI must keep working: API changes must be backward compatible.
- `OnCallHelperMcp` is one project with two modes: stdio (local, default) and HTTP (hosted).
