# AI Signoff Bot 🤖

[Jira link](https://acbot.atlassian.net?continue=https%3A%2F%2Facbot.atlassian.net%2Fwelcome%2Fsoftware&atlOrigin=eyJpIjoiYzZjMjZmZjM0MGU4NGI5Mzg5ZjExMDNmOTZkMzBhZmMiLCJwIjoiaiJ9) for demo board

AI Signoff Bot is a Jira automation that assists teams with acceptance-criteria signoff by reviewing test evidence and managing workflow transitions.  
The goal is to reduce manual QA overhead while keeping humans in control.

This repository evolves in clear iterations. Each version builds on the previous one.

---

## V2 – Evidence-aware Vision AI Signoff

**Status:** ✅ Implemented

V2 now grabs real Jira evidence and sends it to a vision model for AC checks.

### What V2 adds

- **Jira evidence ingestion**
  - Pulls the most recent PNG/JPEG/WEBP attachments (capped by configuration) and downloads them for analysis.
  - Posts a short comment listing which files were used in the run.

- **Vision-based AC evaluation**
  - Sends acceptance criteria plus the gathered images to a vision model and maps its JSON response back to the AC list.
  - Falls back to "NotMet"/"NoEvidence" when the AI response is invalid or missing fields.
  - Includes a fake analyzer for demos that marks only the first criterion as met.

- **Configurable AI settings**
  - Provider/model/API key are configurable (defaults to OpenAI gpt-4o) via `appsettings.json`.

---

## V1 – Jira-native AI Signoff (Plumbing & Workflow)

**Status:** ✅ Implemented

V1 focuses on establishing robust Jira integration and a realistic signoff workflow, with a stubbed evaluator in place of real AI.

### What V1 does

#### Triggers
- Automatically triggers when an issue is moved into **AI Signoff**
- Can also be manually triggered via a comment containing `@ai`

#### Acceptance Criteria Handling
- Reads Acceptance Criteria directly from the Jira issue description
- Uses a simple, deterministic format (bullet points under *Acceptance Criteria*)

#### Signoff Evaluation (Stub)
- Uses a placeholder evaluator (random pass/fail) to simulate AI decision-making
- Produces per-AC results (Met / Not Met / No Evidence)

#### Jira Automation Behaviour
- Posts a structured comment summarising the signoff result
- Assigns the issue to the AI bot during AI Signoff
- Applies labels to reflect outcome:
  - `ai_processed`
  - `ai_signed_off`
  - `needs_human_review`

#### Workflow Transitions
- **PASS**
  - Issue is automatically moved to **Done**
- **FAIL**
  - Issue is flagged 🚩 for visibility
  - Issue remains in **AI Signoff** awaiting human action

#### QA Feedback Loop
- When an issue is moved from **AI Signoff → QA**:
  - Clears AI flags and AI-related labels
  - Adds label `addressing_ai_feedback`
  - Reassigns the issue to the user who moved it
- When the issue returns to **AI Signoff**, the feedback label is removed and the signoff process runs again

#### Safety & Idempotency
- Prevents duplicate processing via labels
- Avoids trigger loops from bot comments or unrelated updates
- All Jira operations are logged and fault-tolerant

---

## Architecture Overview

- **JiraWebhookService**  
  Detects triggers only (status changes, comment tags)

- **SignoffWorkflow**  
  Orchestrates the signoff process

- **JiraClient**  
  Encapsulates all Jira REST API interactions

- **AC Provider / Evaluator / Reporter**  
  Clean separation of parsing, decision-making, and output formatting

This separation allows AI components to be swapped in without changing Jira plumbing.

---

## Disclaimer

V1 uses a stub evaluator to demonstrate workflow and integration only.  
No production signoff decisions are made by AI in this version.

---

## Team

Built by **Not Great, Not Terrible** ☢️  
