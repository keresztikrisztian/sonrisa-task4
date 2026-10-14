# Validation Log

This document records validation activities, test results, identified issues, and corrective actions.

## Initial Project Validation

### Backend
- Check: `dotnet build backend/Sonrisa.EventAlertSystem.slnx`
- Result: Passed during Milestone 1 final developer verification.
- Check: `dotnet test backend/Sonrisa.EventAlertSystem.slnx`
- Result: Passed; 17 tests succeeded, 0 failed, 0 skipped.

### Frontend
- Check: `npm run build`
- Result: Not performed during Milestone 1; frontend changes are outside the milestone scope.

### Git Repository
- Check: Review staged files before committing.
- Result: Pending final staged-file review.

## AI Output Validation

AI-generated implementations will be evaluated for:

- Functional correctness
- Requirement coverage
- Maintainability
- Extensibility
- Error handling
- Test coverage
- Unsupported assumptions

## Review History

### AI Session 01 – Requirements and Architecture Analysis

- **Tool:** GitHub Copilot Agent Mode
- **Evidence:** `ai-history/01-requirements-analysis.json`
- **Review:** The AI-generated requirements analysis and architecture proposal were reviewed by the developer.
- **Result:** Accepted with developer-defined MVP scope adjustments.
- **Decisions:** ADR-004 through ADR-011 were documented and accepted.
- **Documentation:** `ai-history/01-requirements-analysis.md`, `docs/decisions.md`, and `docs/plan.md` were updated.
- **Implementation status:** No application code was generated or modified during this session.
- **Automated tests:** Not run as part of this review.

The detailed implementation plan will be requested in the next AI session. No implementation is approved until that plan has been reviewed.


### AI Session 02 – Implementation Plan Review

- **Tool:** GitHub Copilot Agent Mode
- **Evidence:** `ai-history/02-implementation-plan.json`
- **Review:** The AI-generated implementation plan was reviewed by the developer.
- **Result:** Accepted with seven explicitly approved implementation decisions.
- **Decisions:** ADR-012 through ADR-018.
- **Documentation:** `ai-history/02-implementation-plan.md` and `docs/decisions.md` were updated.
- **Implementation status:** No application code was generated or modified during this session.
- **Automated tests:** Not run as part of this review.
- **Next step:** Begin incremental implementation with domain entities, SQLite persistence, and alert matching tests.

### AI Session 03 – Milestone 1 Implementation

- **Tool:** GitHub Copilot Agent Mode
- **Evidence:** `ai-history/03-milestone1-implementation.json` (original conversation export); `ai-history/03-milestone1-implementation.md` (human-readable session summary).
- **Scope:** Domain entities, SQLite persistence, initial EF Core migration, demo identity registry, alert matching, and focused backend tests.
- **Migration:** `20261013220534_InitialCreate` generated with EF Core 10.0.12. It was explicitly applied to an in-memory SQLite database for validation; application startup does not apply migrations.
- **Build:** `dotnet build backend/Sonrisa.EventAlertSystem.Api/Sonrisa.EventAlertSystem.Api.csproj` passed.
- **Solution build:** `dotnet build backend/Sonrisa.EventAlertSystem.slnx` passed.
- **Tests:** `dotnet test backend/Sonrisa.EventAlertSystem.slnx` passed: 10 succeeded, 0 failed, 0 skipped.
- **Database constraint validation:** SQLite persistence tests verified unique event fingerprints and unique event-rule-channel delivery combinations.
- **Frontend:** Not modified or validated; it is outside Milestone 1 scope.
- **Implementation status:** Milestone 1 only. No alert/event API endpoints, notification sending, background processing, or production authentication were added.

### Human Review 01 – Alert Matching Whitespace Correction

- **Review:** Human code review identified that category and keyword matching did not trim leading or trailing whitespace.
- **Test-first correction:** Added two regression tests for padded category values and padded keywords. Before the production correction, both new cases failed because the matcher returned no matches; the other eight tests passed.
- **Corrective action:** Trimmed category operands before case-insensitive comparison and trimmed keywords before matching title or description. No unrelated matching behavior was changed.
- **Validation:** The focused matching tests passed after the correction. The complete backend suite passed: 12 succeeded, 0 failed, 0 skipped.
- **Evidence:** `ai-history/03-milestone1-implementation.json` (continuous Copilot Agent session, including human review and corrective actions).

### Human Review 02 – Persistence and Matching Test Coverage

- **Review:** A subsequent human review identified missing coverage for rules containing only empty or whitespace keywords, non-matching keywords, case-insensitive keyword uniqueness, duplicate channels, and applying the committed migration while checking constraints.
- **Test-first action:** Added five tests for those cases. All five passed against the existing implementation; no production changes were required.
- **Persistence setup distinction:** The event fingerprint, event-rule-channel delivery, case-insensitive duplicate-keyword, and duplicate-channel tests initialize an in-memory SQLite schema with `EnsureCreatedAsync()`. The separate `InitialCreateMigration_AppliesAndEnforcesKeyUniqueConstraints` test uses `Database.MigrateAsync()` and does not call `EnsureCreatedAsync()`; it verifies the migrated schema's event fingerprint, event-rule-channel delivery, case-insensitive keyword, and channel uniqueness constraints.
- **Final Copilot-reported validation:** `dotnet test backend/Sonrisa.EventAlertSystem.slnx` passed: 17 succeeded, 0 failed, 0 skipped. `dotnet build backend/Sonrisa.EventAlertSystem.slnx` succeeded. `git diff --check` passed.
- **Human review outcome:** Reviewed domain entities, EF Core configurations, matching service, demo identity registry, dependency injection, persistence and matching tests, and the `InitialCreate` migration. No further blocking issues were identified.
- **Deferred to Milestone 2:** API input validation, keyword normalization on writes, event fingerprint generation, demo identity authorization, and notification delivery implementation.
- **Frontend:** Frontend validation was not performed because it is outside Milestone 1 scope.
- **Evidence:** `ai-history/03-milestone1-implementation.json` (continuous Copilot Agent session, including human review and corrective actions).
- **Final developer verification:** The developer independently executed `dotnet build backend/Sonrisa.EventAlertSystem.slnx` and `dotnet test backend/Sonrisa.EventAlertSystem.slnx` after the human review. Both commands completed successfully; all 17 backend tests passed.

## Milestone 2 Alert Rule CRUD Checkpoint (Steps 1A–1E)

This checkpoint covers the public demo identity lookup, demo identity authentication and role authorization, and alert-rule list/create/update/logical-delete API operations. It does not complete the remaining Milestone 2 event ingestion and notification delivery work. The accepted boundaries are recorded in ADR-019 through ADR-026; those decisions were not changed during this checkpoint closeout.

### TDD Sessions

- **Step 1A – Demo identity lookup:** Added `WebApplicationFactory` integration coverage before implementing `GET /api/demo-identities`. RED: 2 tests failed with `404 NotFound` because the route was absent. GREEN: 2 focused tests passed; the complete backend suite passed with 19 tests. The test host was subsequently anchored on the actual `Program` entry point, and both focused identity tests plus the 19-test suite passed again.
- **Step 1B – Authentication and rule-list authorization:** Added integration coverage for missing/unknown identities, admin role denial, user access, case-insensitive canonical ownership, owner/deleted filtering, and public identity lookup with an unknown header. RED: 9 tests total, 6 failed and 3 passed; the protected route returned `404`. GREEN: all 9 focused tests passed; the complete backend suite passed with 26 tests.
- **Step 1B contract correction – Paginated rule listing:** Human review identified that `GET /api/alert-rules` returned a raw array rather than ADR-025's page envelope. RED: 18 tests total, 12 failed and 6 passed; list responses had the wrong shape and invalid pagination returned `200`. GREEN: all 18 focused tests passed; the complete backend suite passed with 35 tests. Coverage verifies default and explicit pagination, count/filter order, stable ordering, requested-page boundaries, and invalid query values.
- **Step 1C – Rule creation:** Added isolated POST integration tests for defaults, explicit empty collections, field validation, trimmed/unique keywords, channels, owner derivation, response DTO shape, and persisted child rows. RED: 12 tests failed with `405 Method Not Allowed`; compilation succeeded. A test response-stream reuse issue was corrected before final green validation. GREEN: 16 focused tests passed; the complete backend suite passed with 51 tests.
- **Step 1D – Full-replacement rule update:** Added tests for replacement and clearing of child collections, preserved ID/owner/creation time, updated timestamp, authorization, not-found cases, and validation. RED: 21 tests total, 18 failed and 3 passed; PUT was absent. One test's assumption about child-array ordering was removed because ADR-025 does not define that ordering. GREEN: 21 focused tests passed; the complete backend suite passed with 72 tests.
- **Step 1E – Logical rule deletion:** Added tests for authorization, missing/already-deleted/non-owned rules, `204 No Content`, GET exclusion, PUT returning `404`, and preservation of rule children and existing delivery history. RED: 6 tests failed with `405 Method Not Allowed`; compilation succeeded. GREEN: 6 focused tests passed; the complete backend suite passed with 78 tests.

### Human Review and Corrections

- Human review required replacing the unrelated weather-controller test-host anchor with `WebApplicationFactory<Program>`; the standard public partial `Program` declaration was added without changing startup behavior.
- Review identified the pagination response mismatch, which was corrected before proceeding beyond the protected GET endpoint.
- The test factory option was renamed from `ClientOptions` to `TestClientOptions` to remove compiler warning CS0108.
- EF Core multiple-collection query warnings encountered while loading keyword/channel collections were addressed with split-query behavior on the affected queries and test inspection.
- TDD test-harness issues (response stream reuse and an absent test namespace import) were corrected before considering the corresponding RED results valid.

### Final Checkpoint Validation

- **Focused tests:** Step 1E suite passed: 6 succeeded, 0 failed, 0 skipped.
- **Complete backend suite:** `dotnet test backend/Sonrisa.EventAlertSystem.slnx` passed: 78 succeeded, 0 failed, 0 skipped.
- **Solution build:** `dotnet build backend/Sonrisa.EventAlertSystem.slnx` succeeded.
- **Whitespace check:** `git diff --check` passed.
- **Warnings:** No warnings were reported by the final focused DELETE run, complete backend test run, or solution build.
- **Frontend:** Not changed or validated in this checkpoint.
- **Remaining Milestone 2 work:** Event submission/querying, matching-to-delivery orchestration, asynchronous delivery processing, and delivery status APIs remain incomplete.
- **Commit/push:** Not performed; this checkpoint is awaiting developer review.

Evidence summary: [`ai-history/04-milestone2-alert-rule-crud.md`](../ai-history/04-milestone2-alert-rule-crud.md). The existing Milestone 1 conversation export and summary were preserved. No original Milestone 2 transcript export was present in `ai-history`; the new file is a factual summary, not a reconstructed transcript.

## Milestone 2 Event and Notification Recording Slice

### Scope and Implementation

- **Scope:** Completed the backend Milestone 2 event submission/query, notification-delivery recording, and delivery-status query work. No frontend changes, live transports, retries, or processing-state restart recovery were added.
- **Event submission:** Added admin-only `POST /api/events` with strict field and explicit-offset timestamp validation, preservation of submitted text, UTC timestamp persistence, and the ADR-022 versioned canonical SHA-256 fingerprint.
- **Atomic matching:** Event and all deliveries for matched active rules and configured Email/Slack channels are saved in one transaction. Duplicate fingerprint submissions return the existing event with `200` and create no new deliveries; only the specific event fingerprint uniqueness violation is treated as a duplicate. Other SQLite uniqueness conflicts return `409`.
- **Recording worker:** Added a database polling `BackgroundService` that persists `Pending` then `Processing` then `Recorded`. No network calls are made; `AttemptCount` stays zero and `SentAtUtc` remains null.
- **Admin queries:** Added paginated event list and detail routes and paginated notification-delivery queries, with the accepted filters, stable ordering, explicit response DTOs, and admin authorization.

### TDD and Validation Evidence

- Event submission tests were added first. The initial focused run returned `404` for the absent route; after implementation, the focused submission suite passed **15/15**.
- A fixed ADR-022 digest vector and a SQLite trigger that forces delivery insertion to fail passed, confirming the expected fingerprint and that neither event nor delivery persists after a failed transaction. This focused run passed **17/17**.
- Event/delivery query and worker tests were added before those APIs and worker existed; the initial focused run was RED. After implementation and correction of a test fixture that violated the existing event/rule/channel unique index, the query/worker slice passed **15/15**.
- The combined new event, fingerprint, query, and worker tests passed **32/32**.
- The first complete-suite run exposed one timing failure with deliveries still `Pending` under parallel test load. The worker polling interval was changed to one second; the subsequent complete suite passed **110 succeeded, 0 failed, 0 skipped**.
- Final ADR-026 review added a deterministic file-backed SQLite integration test with separate connections and a command-interceptor barrier that holds both fingerprint lookups until both have executed. The concurrent submissions returned one `201` and one duplicate `200`, shared one event ID, and persisted exactly one Email and one Slack delivery. Focused result: **1 passed**.
- Exception review confirmed that only SQLite unique error `2067` naming `Events.Fingerprint` becomes a duplicate; other primary/unique conflicts return `409`, while a forced non-unique SQLite trigger failure remains an error and rolls back the event and deliveries.
- Final backend suite after concurrency coverage: **111 succeeded, 0 failed, 0 skipped**.
- `dotnet build backend/Sonrisa.EventAlertSystem.slnx --no-restore` succeeded.
- `git diff --check` passed.

### Limitations and Review Notes

- The test host clears external logging providers so Windows Event Log does not interfere with `WebApplicationFactory` diagnostics.
- Concurrent fingerprint deduplication is exercised with a deterministic barrier and separate file-backed SQLite connections; no timing-based race assertion is used.
- `Processing` recovery after restart, bounded retries, and live Email/Slack delivery remain Milestone 3 work under ADR-024.
- Frontend was not modified or validated. The initial implementation checkpoint was uncommitted; the final review and requested commit/push are a separate follow-up.

Evidence summary: [`ai-history/05-milestone2-event-notification.md`](../ai-history/05-milestone2-event-notification.md). This is a factual implementation summary, not a reconstructed transcript.
