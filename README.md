# AI Signoff Bot 🤖


AI Signoff Bot automates Jira signoff by evaluating acceptance criteria with a vision model, validating screenshot quality, and nudging issues through the workflow while keeping humans in control.

## What the bot does
- **Watch Jira for signoff triggers**
  - Auto-runs when an issue moves into **AI Signoff** or when someone mentions `@ai` in a comment.
- **Parse acceptance criteria**
  - Reads bullet-point ACs directly from the Jira issue description.
- **Collect evidence**
  - Downloads the latest PNG/JPEG/WEBP attachments (respecting config limits) and posts a short evidence comment with links.
- **Run vision-based evaluation**
  - Sends ACs and gathered screenshots to a configurable AI vision model (default OpenAI gpt-4o) and maps the JSON response back to each AC.
- **Enforce evidence quality rules**
  - Blocks signoff if screenshots come from local/private URLs or hide the browser URL bar.
- **Report outcomes in Jira**
  - Posts a structured comment summarizing AC results, linked evidence, and any quality-check failures.
- **Drive workflow transitions**
  - Adds labels (`ai_processed`, `ai_signed_off`, `needs_human_review`) and transitions passing issues to **Done** while flagging failures for human review.
- **Handle QA feedback loops**
  - When issues move back to **QA**, clears AI labels/flags, adds `addressing_ai_feedback`, and resets state for the next run.

## Architecture at a glance
- **JiraWebhookService** — detects triggers only (status changes, comment tags).
- **SignoffWorkflow** — orchestrates AC extraction, evidence gathering, AI analysis, rule evaluation, commenting, and workflow updates.
- **JiraClient** — encapsulates all Jira REST API interactions.
- **AC Provider / Evidence Analyzer / Reporter** — separates parsing, AI decision-making, and Jira comment formatting.
- **Rule engine** — pluggable checks that validate evidence quality before signoff completes.

## Configuration
- Set Jira connection details under `Jira` in `appsettings.json`.
- Configure AI provider, model, and API key under `Ai` in `appsettings.json` (OpenAI gpt-4o by default).

## Future Work
- **Video test evidence via frame sampling (not full video analysis)**  
  - Treat screen recordings as sampled frames or short clips, not continuous playback.
  - Best for UI testing, silent recordings, and flows already validated visually.
  - Sampling is cheaper, more deterministic, and easier to reason about.
  - Dedicated video models could be explored later, but sampling stays the main path.
- **Test evidence versioning and change detection**  
  - Track evidence across signoff runs.
  - Detect new, removed, or replaced screenshots.
  - Call out improvements/regressions in the signoff comment.
  - Makes re-runs more useful in iterative QA.
- **Evidence quality and completeness metrics (non-blocking)**  
  - Add informational metrics: AC coverage %, evidence per AC, basic clarity/resolution checks.
  - Advisory only; no gating or confidence scoring.
- **Design intent comparison (e.g. Figma)**  
  - Optional, higher-level check against design intent instead of pixel diffs.
  - Look for missing components, unexpected UI, and obvious layout/hierarchy issues.
- **AI-suggested missing Acceptance Criteria and edge cases**  
  - AI suggests missing ACs or edge-case tests.
  - Advisory only; helps coverage without changing requirements.
