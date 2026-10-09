# OnCall Helper MCP server

Lets any MCP-capable LLM client (Claude Desktop, Claude Code, etc.) triage incidents conversationally
using the existing OnCallHelperApi. It is a separate process that calls the API over HTTP; the API
and UI are unchanged.

## Tools
| Tool | Type | Purpose |
|---|---|---|
| `triage_incident` | read | Similar incidents + AI guidance (`POST /api/oncall/analyze`) |
| `find_similar_incidents` | read | Vector search (`POST /api/incidents/similar`) |
| `search_incidents` | read | Filter/keyword search (`GET /api/incidents`) |
| `extract_incident_draft` | read | Chat transcript → draft (`POST /api/incidents/extract`) |
| `create_incident` | write | `POST /api/incidents` |
| `update_incident` | write | Merge + `PUT /api/incidents/{id}` |

Also: prompts `oncall_triage`, `oncall_postmortem_record`, and server instructions that tell the model
to ask clarifying questions, re-triage as new facts arrive, and get approval before writes.
`rebuild-embeddings` is intentionally not exposed.

## Config (env vars)
- `ONCALL_API_BASE_URL` – default `http://localhost:5172`
- `ONCALL_API_TOKEN` – fixed bearer token (manual override; it expires). Not needed when the API runs with `Auth:Enabled=false`

### Authenticating to the deployed API (Auth0 machine-to-machine)
Create an Auth0 **Machine to Machine** application authorized for the API (`OnCallHelperApi`), then set:
- `ONCALL_AUTH0_DOMAIN` – e.g. `dev-k0sl1xaa1o87ofbn.us.auth0.com`
- `ONCALL_AUTH0_CLIENT_ID`
- `ONCALL_AUTH0_CLIENT_SECRET` – **secret**; keep it out of git and chat
- `ONCALL_AUTH0_AUDIENCE` – optional, defaults to `http://localhost:5172` (the API identifier)

The server fetches a token with the client-credentials grant, caches it until ~60s before expiry, and
on a 401 refreshes it and retries once. If `ONCALL_API_TOKEN` is set it takes priority. If only some
`ONCALL_AUTH0_*` variables are set, calls fail with a message naming the missing ones.

## Run / register
```bash
dotnet build OnCallHelperMcp -c Release
claude mcp add oncall-helper -e ONCALL_API_BASE_URL=http://localhost:5172 -- dotnet /ABS/PATH/OnCallHelperMcp/bin/Release/net9.0/OnCallHelperMcp.dll
```
Claude Desktop (`claude_desktop_config.json`):
```json
{ "mcpServers": { "oncall-helper": {
  "command": "dotnet",
  "args": ["/ABS/PATH/OnCallHelperMcp/bin/Release/net9.0/OnCallHelperMcp.dll"],
  "env": { "ONCALL_API_BASE_URL": "http://localhost:5172" } } } }
```
