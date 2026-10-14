# AI Session 04 – Milestone 2 Alert Rule CRUD

## 1. Objective and Scope

This summary records the incremental implementation of Milestone 2 Steps 1A–1E: the public demo identity lookup, demo identity authentication and role authorization, and alert-rule list, create, full-replacement update, and logical deletion APIs.

It is a checkpoint for the Alert Rule CRUD portion only. Event ingestion, event matching/enqueue orchestration, notification delivery processing, delivery status APIs, and the Angular UI remain future work. The implementation followed accepted ADR-014, ADR-015, ADR-019 through ADR-026; no accepted ADR was changed during checkpoint closeout.

## 2. Implementation Sequence

- **Step 1A – Demo identity lookup:** Added HTTP integration coverage using `WebApplicationFactory`, then added public `GET /api/demo-identities` using `DemoIdentityRegistry` and the three-field identity DTO. The test host was changed from the unrelated weather-controller anchor to `WebApplicationFactory<Program>`.
- **Step 1B – Demo authentication and authorization:** Added an ASP.NET Core authentication handler for `X-Demo-Identity`, canonical identity claims, `AlertRules.User` and `Events.Admin` policies, and authentication middleware ordering. Added `GET /api/alert-rules`, scoped to the authenticated user's non-deleted rules.
- **Pagination correction:** RED tests exposed that the list returned a raw array rather than ADR-025's paginated envelope. The endpoint was corrected to validate parameters, count after filtering, order stably, page in the database, and return `{ items, page, pageSize, totalCount }`.
- **Step 1C – Create:** Added authenticated `POST /api/alert-rules`, request validation, keyword trimming and duplicate rejection, distinct Email/Slack channel validation, authenticated owner assignment, and one EF Core save for the rule graph.
- **Step 1D – Update:** Added authenticated `PUT /api/alert-rules/{id}` with full replacement semantics, owner/non-deleted lookup, shared collection normalization, child replacement, timestamp update, and preservation of ID, owner, and creation time.
- **Step 1E – Delete:** Added authenticated `DELETE /api/alert-rules/{id}` as logical deletion. Tests verify the rule, keyword/channel children, and existing notification delivery remain persisted; GET excludes it and PUT returns `404`.

## 3. TDD and Validation Evidence

- Step 1A: 2 identity tests failed with `404` before implementation; 2 passed afterward. Full backend suite: 19 passed.
- Step 1B authorization: 9 focused tests, 6 failed and 3 passed before implementation; 9 passed afterward. Full suite: 26 passed.
- Pagination correction: 18 focused tests, 12 failed and 6 passed before correction; 18 passed afterward. Full suite: 35 passed.
- Step 1C create: initial 12 cases failed with `405` before implementation; final focused suite: 16 passed. Full suite: 51 passed.
- Step 1D update: 21 cases, 18 failed and 3 passed before implementation; focused suite: 21 passed. Full suite: 72 passed.
- Step 1E delete: 6 cases failed with `405` before implementation; focused suite: 6 passed. Final full suite: 78 passed, 0 failed, 0 skipped.
- Final `dotnet build backend/Sonrisa.EventAlertSystem.slnx` succeeded; `git diff --check` passed. No warnings were reported by final validation.

## 4. Human Review Findings and Fixes

- Replaced the unrelated test-host anchor with the actual `Program` entry point.
- Corrected the GET pagination contract mismatch identified during review.
- Renamed the test factory's `ClientOptions` property to `TestClientOptions`, removing CS0108.
- Used split-query behavior for multi-collection reads exercised by CRUD operations/tests.
- Corrected test-only issues before treating RED runs as meaningful, including response-stream reuse, a missing using directive, and assumptions about unspecified child-array ordering.

## 5. Evidence and Provenance

This remains a concise summary of the implementation and validation. The original repository JSON exports were preserved. A readable transcript was later recovered from the local VS Code session archive: [Milestone 2 alert-rule CRUD conversation](transcripts/03-milestone2-alert-rule-crud.md). The archive contains the prompts, visible Copilot responses, tool names/arguments, and completion status; detailed tool result payloads are not present and require manual VS Code export for complete raw-output fidelity.

The checkpoint validation details are also recorded in [`docs/validation-log.md`](../docs/validation-log.md). No commit or push was made.