---
name: oncall-incident-triage
description: Triage an on-call or production issue from a pasted or attached Slack conversation, alert, or incident notes. Finds whether the issue already exists in the team's incident history, returns the past resolution and suggested actions, otherwise drafts a new incident (saved only after the user approves), and later updates it with the resolution. Can optionally check GitLab for code-related causes and search Confluence for runbooks. Use this whenever the user shares text or a file about a page, outage, error, alert, or incident, or asks "have we seen this before", "is this a known issue", "log this incident", "find similar incidents", or "update the incident with the resolution", even if they do not mention this skill by name.
---

# On-call incident triage

Help an on-call engineer who is under pressure. The goal is a fast, honest answer to three questions: have we seen this before, what fixed it last time, and if it is new, what should we record so the next person benefits. The incident history is only useful if it stays clean, so saving to it needs care (see "What may be saved").

## Connectors this skill uses
- **The incident history**, from whichever of these is connected (say which you used; if both are, prefer the one the user names, otherwise the hosted one):
  - **OnCall Helper (hosted connector, `mcp.oncallhelper.com`)**. Tools: `extract_incident_draft`, `find_similar_incidents`, `triage_incident`, `search_incidents`, `create_incident`, `update_incident`.
  - **OnCall Helper Local (`oncall-helper-local`, runs in Docker on the user's laptop)**. Tools: `find_similar_incidents`, `search_incidents`, `create_incident`, `update_incident` only. It has no extraction or triage tools: you do that work yourself (steps 2 and 4a). Its data is separate from the hosted history.
- **GitLab connector** (optional, read-only): find repositories, merge requests, commits, tags and releases.
- **Confluence connector** (optional, read-only, only when the user asks): find runbooks.

Refer to GitLab and Confluence by what they do, because their tool names vary. If a connector the task needs is missing or returns an error, say so plainly and continue with what you have.

## Workflow

### 1. Read the input
Use the attached file or the pasted text. If there is none, ask for it. Slack text may contain names, timestamps, mentions, and chatter; focus on what happened, where (service, environment), when it started, what was tried, and the outcome.

### 2. Understand the incident
If `extract_incident_draft` is available, call it with the conversation text and treat the result as a draft from an AI model: compare it with the source and correct anything that does not match (service, severity, who did what). If it is not available (the local server), extract the incident yourself with the same rules:
- Extract **one** incident (the main one). title: short and searchable. description: 3 to 6 sentences on what happened, the symptoms, the impact and when it started. Never paste the raw chat or code into it.
- serviceName: the affected service, or empty if unclear. environment: production, staging, development or test (prod/prd/live to production, stage/stg to staging, dev to development, qa to test), or empty.
- severity: sev1 to sev4 (P1/S1/critical to sev1, P2/high to sev2, P3/medium to sev3, P4/low/minor to sev4). If none is stated or implied, leave it empty. Never guess.
- resolution, only if the conversation shows one: the final confirmed root cause, a summary, the concrete steps actually performed in order (no speculation), and who resolved it.
- Ignore chat artifacts (mentions, timestamps, emoji codes, message ids), base everything only on the text, do not invent facts, and prefer the latest confirmed statement when they conflict.

Do not save anything yet. The search in step 3 uses only the short description, never the raw chat.

### 3. Search the history
Call `find_similar_incidents` (top 5) with a short description built from the symptoms, service, environment and exact error messages. Show the best matches with title, id, similarity score, root cause and how it was resolved.

Decide whether this is a known issue by judgment, not by a fixed cut-off. A high score (roughly 0.75 or more) together with the same service or symptoms, and a plausible same cause, is strong. A middling score or a different cause means "related, not the same". Explain your reasoning in one or two sentences. When it is genuinely unclear, say so and ask the user. Scores come from text similarity, so a high score can still be a different problem.

### 4a. If it looks known
If `triage_incident` is available, call it with the description to get guidance grounded in those past incidents. If it is not (the local server), write that guidance yourself from the matches you already retrieved, using only what those incidents recorded. Lead with what resolved the earlier incident, then what to try now and in what order. Point out where this incident differs from the old one, because that is where the old fix may not apply.

### 4b. If it looks new
Say so, and go to step 6.

### 5. Optional code check (GitLab)
Only do this when code is a plausible cause. Choose a mode from the evidence, **tell the user which mode you chose and why**, and let them override:

- **Recent change**: the text mentions a deploy, release, or "started after". Look at merge requests, commits, tags and releases around that time. If two releases are available, summarize what changed between them and flag risky kinds of change (configuration, migrations, dependency updates).
- **Where the behavior lives**: an error message, endpoint, or config key, but no recent change. Search the repositories for those strings and check when the relevant files last changed. Do not limit this by date, because many bugs are old.
- **Probably not code**: an external provider, data, or infrastructure problem (for example, when the closest past incident was caused by a third party). Skip GitLab and say why.

Ask for the GitLab group only if you do not already know it. Do not scan a whole group blindly: narrow to repositories that match the service or keywords, look at a handful first, and ask before widening. Show what you found with links, label what is confirmed versus suspected, and never state that a change caused the incident without evidence. GitLab is read-only.

### 5b. Optional runbooks (Confluence)
Only when the user asks. Search by service and symptom, then give page titles and links with a short summary in your own words.

### 6. Save a new incident (after approval)
Search again right before creating, to avoid a duplicate. Show the user the exact record you intend to save (title, description, service, environment, severity, and the resolution if known) and wait for a clear yes. Then call `create_incident`.

### 7. Update later with the resolution
When the user reports how it was resolved, find the incident (the id from earlier in the chat, or `search_incidents` by title or service). Show exactly what will change (root cause, summary, steps taken, who resolved it) and wait for a yes. Then call `update_incident` with those fields. Unspecified fields keep their current values.

## What may be saved
The incident history is shared and also feeds an AI model, so keep it to short summaries of what happened and how it was fixed.

- **Allowed**: a short description, service, environment, severity, root cause in plain words, the steps taken, who resolved it, and **links or identifiers** such as a merge request number, a release tag, or a Confluence page title and URL.
- **Never save or send**: source code, diffs, configuration contents, stack traces that include code, text copied from Confluence pages, or secrets (tokens, passwords, connection strings, keys). Describe a fix in words ("reverted MR !123 that changed the retry setting"), not by pasting it.
- **Applies to every call to the OnCall Helper tools**, including `triage_incident` and `find_similar_incidents`, because their text is sent to the API and an AI service.
- Keep customer names, emails and other personal data out of what you save; summarize instead.
- Always show the exact text before saving, and check it against this list.

## How to answer
Keep it short and practical. Use this shape and drop any part that does not apply:

1. **Verdict**: known issue, related to past incidents, or new, with your confidence.
2. **Evidence**: the matching incidents (title, id, score, root cause and resolution).
3. **What to try**: ordered steps, each tied to its source.
4. **Code findings**: only if step 5 ran, with links and what is confirmed versus suspected.
5. **Proposed record**: for a new incident or an update, shown for approval.

Be honest about uncertainty. If a tool fails (for example, a rate limit), try once more later and otherwise report it. Never present suggested commands as already run.
