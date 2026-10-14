# Milestone 4: Angular MVP

## Scope and Implementation

Implemented the local Angular MVP using the configured Angular 22 standalone setup, signals, reactive forms, and existing Vitest toolchain. The app provides demo identity selection and request headers, paginated alert-rule CRUD for demo users, and admin event submission/detail plus notification-delivery monitoring. No runtime dependencies or backend changes were introduced.

The development proxy targets the local HTTPS API; the API's existing migration was applied to the local demo database. Shared UI styles were moved to the global stylesheet to remove component style-budget warnings without changing build budgets. The existing partial history remains preserved in [the earlier Milestone 4 transcript excerpt](transcripts/06-milestone4-angular-frontend.md); the [complete recovered conversation](transcripts/07-milestone4-angular-frontend-complete.md) is the authoritative chronological record.

## End-to-End Smoke Test

- Created `Milestone 4 smoke` as `demo-user` with `Transport`, `smokeprobe`, Email, and Slack.
- Submitted matching event `smokeprobe transit alert` as `demo-admin`; API returned `201 Created`, event ID 1.
- Verified exactly two event-1 deliveries, Slack and Email; both reached `Recorded` at one attempt.
- Edited the rule, paused it, re-enabled it, and logically deleted it. The user list returned empty; both Recorded deliveries remained in the admin monitor.
- No application defect was confirmed. The integrated browser automation did not invoke native HTML checkbox/submit default activation for synthetic clicks; native `requestSubmit()` exercised the Angular form path and real API successfully.

## Validation

- Angular tests: **6 passed** across 3 files.
- Angular production build: **passed without stylesheet budget warnings**; configured budgets were unchanged.
- Backend tests: **116 passed, 0 failed, 0 skipped**.
- Backend solution build: **passed**.
- `git diff --check`: **passed**.
- The implementation and smoke-test sessions did not commit or push. This follow-up request handles checkpoint staging, review, commit, and push after validation.

## Limitations

- Demo identity is not production authentication; the development proxy disables certificate validation for local development only.
- The current backend delivery DTO omits `LastError`, so the UI cannot display it.
- The recovered VS Code JSONL stores success/failure status for most tool invocations but omits their detailed result bodies. The complete transcript embeds the five saved output payloads and marks the remaining gaps. The last developer prompt is truncated in the archived text after "Review the staged".