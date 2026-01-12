# AI Signoff Bot 🤖

[Jira link](https://acbot.atlassian.net?continue=https%3A%2F%2Facbot.atlassian.net%2Fwelcome%2Fsoftware&atlOrigin=eyJpIjoiYzZjMjZmZjM0MGU4NGI5Mzg5ZjExMDNmOTZkMzBhZmMiLCJwIjoiaiJ9) for the demo board

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

## Team
Built by **Not Great, Not Terrible** ☢️

## Future Work
- **Video test evidence via frame sampling (not full video analysis)**  
  - Extend evidence analysis from screenshots to screen-recorded videos by treating recordings as sampled still frames or short segments rather than continuous playback.
  - Especially suitable for UI testing, screen recordings without audio, and flows already validated visually.
  - Frame sampling keeps analysis more cost-effective, deterministic, and easier to reason about than full video understanding.
  - Future iterations could evaluate dedicated video-capable models, but frame sampling remains the primary path.
- **Test evidence versioning and change detection**  
  - Track evidence across multiple AI signoff runs.
  - Detect what changed since the last attempt, including new screenshots and removed or replaced evidence.
  - Highlight improvements or regressions directly in the signoff comment so reviewers can quickly see what was fixed after feedback.
  - Supports iterative QA workflows and more meaningful re-runs.
- **Evidence quality and completeness metrics (non-blocking)**  
  - Introduce informational metrics such as AC coverage percentage, evidence per AC, and basic clarity or resolution checks.
  - Keep metrics advisory only to support human judgment without gating signoff or acting as probabilistic pass/fail scoring.
- **Design intent comparison (e.g. Figma)**  
  - Add an optional, higher-level check that compares test evidence against design intent rather than pixel-perfect diffs.
  - Focus on missing components, unexpected UI elements, or obvious layout and hierarchy issues.
  - This is an aspirational capability that helps align UI outputs with design expectations.
- **AI-suggested missing Acceptance Criteria and edge cases**  
  - Use AI to suggest additional ACs or edge-case tests as non-blocking, advisory recommendations.
  - Improve test coverage and prompt teams to consider gaps without rewriting or replacing requirements.
