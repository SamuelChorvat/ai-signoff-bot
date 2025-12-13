# AI Signoff Bot 🤖

[Jira link](https://acbot.atlassian.net?continue=https%3A%2F%2Facbot.atlassian.net%2Fwelcome%2Fsoftware&atlOrigin=eyJpIjoiYzZjMjZmZjM0MGU4NGI5Mzg5ZjExMDNmOTZkMzBhZmMiLCJwIjoiaiJ9) for the demo board

AI Signoff Bot is a Jira automation that reviews acceptance-criteria evidence and nudges issues through the workflow. It aims to cut QA busywork while keeping humans in control.

The project moves in clear versions. Each one adds a slice of capability.

---

## V2 – Evidence-aware Vision AI Signoff

**Status:** ✅ Implemented

V2 grabs real Jira evidence and runs AC checks with a vision model.

### What V2 adds

- **Jira evidence ingestion**
  - Pulls the latest PNG/JPEG/WEBP attachments (capped by config) and downloads them for analysis.
  - Posts a short comment listing which files were used.

- **Vision-based AC evaluation**
  - Sends the AC list and gathered images to a vision model, then maps the JSON response back to each AC.
  - Falls back to "NotMet"/"NoEvidence" when the AI reply is missing fields or malformed.

- **Configurable AI settings**
  - Provider/model/API key are configurable (default OpenAI gpt-4o) in `appsettings.json`.

---

## V1 – Jira-native AI Signoff (Plumbing & Workflow)

**Status:** ✅ Implemented

V1 sets up Jira wiring and a realistic signoff flow with a stub evaluator.

### What V1 does

#### Triggers
- Auto-runs when an issue moves into **AI Signoff**
- Can also be manually triggered via a comment containing `@ai`

#### Acceptance Criteria handling
- Reads ACs directly from the Jira issue description
- Uses a simple, deterministic format (bullet points under *Acceptance Criteria*)

#### Signoff evaluation (stub)
- Uses a placeholder evaluator (random pass/fail) to mimic AI decisions
- Produces per-AC results (Met / Not Met / No Evidence)

#### Jira automation behaviour
- Posts a structured comment summarising the signoff result
- Assigns the issue to the AI bot during AI Signoff
- Applies labels to reflect outcome:
  - `ai_processed`
  - `ai_signed_off`
  - `needs_human_review`

#### Workflow transitions
- **PASS**
  - Issue is automatically moved to **Done**
- **FAIL**
  - Issue is flagged 🚩 for visibility
  - Issue stays in **AI Signoff** until a human responds

#### QA feedback loop
- When an issue moves from **AI Signoff → QA**:
  - Clears AI flags and AI-related labels
  - Adds label `addressing_ai_feedback`
  - Reassigns the issue to the user who moved it
- When the issue returns to **AI Signoff**, the feedback label is removed and the signoff process runs again

#### Safety & idempotency
- Prevents duplicate processing via labels
- Avoids trigger loops from bot comments or unrelated updates
- Logs all Jira operations and keeps them fault-tolerant

---

## Architecture overview

- **JiraWebhookService**
  - Detects triggers only (status changes, comment tags)

- **SignoffWorkflow**
  - Orchestrates the signoff process

- **JiraClient**
  - Encapsulates all Jira REST API interactions

- **AC Provider / Evaluator / Reporter**
  - Separates parsing, decision-making, and output formatting

This separation lets you swap AI components without changing the Jira plumbing.

---

## Disclaimer

V1 uses a stub evaluator to demonstrate workflow and integration only. No real signoff decisions are made by AI in this version.

---

## Team

Built by **Not Great, Not Terrible** ☢️
