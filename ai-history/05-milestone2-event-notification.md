# AI Session 05 – Milestone 2 Event and Notification Recording

## 1. Objective and Scope

This summary records completion of the remaining backend Milestone 2 vertical slice after the alert-rule CRUD checkpoint. The implementation followed accepted ADR-019 through ADR-026, particularly ADR-021 through ADR-026. No ADR was changed.

The scope included event submission and deduplication, matching and atomic notification-delivery creation, asynchronous recording-mode processing, admin event/delivery queries, and focused integration tests. Frontend work, live Email/Slack transports, retry handling, and recovery of deliveries left in `Processing` were excluded.

## 2. Implementation

- Added `EventFingerprint` implementing ADR-022: NFC text normalization, Unicode whitespace collapsing, invariant uppercase, UTF-8 byte lengths in big-endian order, the version prefix, whole UTC Unix seconds, SHA-256, and lowercase hexadecimal output.
- Added admin-only `POST /api/events`. Request validation checks field limits, required content, and an explicit-offset ISO-8601 occurrence timestamp. Original event text is persisted unchanged and the occurrence timestamp is stored as UTC.
- Existing matching behavior is reused. Only enabled, non-deleted matching rules contribute deliveries, and only configured Email and Slack channels produce delivery rows.
- Event and delivery rows are persisted in one explicit database transaction. Existing fingerprint matches return the stored event with `200` and `isDuplicate: true`. A race on the fingerprint unique index is handled specifically; unrelated uniqueness violations return `409`.
- Added admin-only paginated event list/detail and notification-delivery list routes. They use DTOs, validate filters and pagination, and implement the accepted filters and stable descending order. Event detail includes delivery DTOs.
- Added a hosted recording worker that polls persisted Pending rows and saves `Processing` and then `Recorded` as separate transitions. It makes no external calls and does not set `SentAtUtc` or increment `AttemptCount`.
- The integration test factory clears logging providers to keep the Windows Event Log implementation from interfering with host error reporting in tests.

## 3. TDD and Validation Evidence

- POST event integration tests were added before the route. The focused RED run observed `404` for the absent endpoint; the first implemented focused run passed **15 tests**.
- Added an independent fixed fingerprint vector and a SQLite trigger-based delivery-write failure test. The latter verified that a failed delivery insert leaves neither an event nor a delivery row. Focused result: **17 passed**.
- Added query and recording-worker tests before implementation; the initial run was RED. After implementation, fixing a duplicate event/rule/channel test fixture, and isolating test-host logging, the focused slice passed **15 tests**.
- Combined new event, fingerprint, query, and worker tests: **32 passed**.
- First full-suite execution had one intermittent assertion where two deliveries remained Pending after the test timeout. The worker poll interval was set to one second. Full backend suite then passed: **110 succeeded, 0 failed, 0 skipped**.
- Full solution build succeeded with `dotnet build backend/Sonrisa.EventAlertSystem.slnx --no-restore`.
- `git diff --check` passed.

## 4. Final ADR-026 Concurrency Review

- Added a deterministic HTTP integration test using separate file-backed SQLite connections and an EF Core command-interceptor barrier. Both requests complete the initial fingerprint lookup before either proceeds to persistence, forcing the unique-index race without coordinating via sleep delays.
- The responses were one `201 Created` and one duplicate `200 OK`, with the same event ID. SQLite contained one event and exactly one Email and one Slack delivery.
- Reviewed exception classification: only SQLite extended unique error `2067` whose message identifies `Events.Fingerprint` is treated as duplicate. SQLite primary-key/other unique violations return `409`; unrelated database errors are not caught as duplicates. The trigger-based non-unique failure test confirms rollback and error propagation.
- No production code correction was needed after the concurrent integration test passed.
- Final backend suite after adding the race test: **111 succeeded, 0 failed, 0 skipped**. The solution build succeeded.

## 5. Limitations and Provenance

- The simultaneous duplicate HTTP race is covered using a deterministic database-command barrier; it does not depend on sleep-based race timing.
- Restart recovery for `Processing`, retry policy, and actual Email/Slack transports remain deferred to Milestone 3 as required by ADR-024.
- No frontend files were changed. The original implementation checkpoint was uncommitted; final review and the requested commit/push are tracked separately.
- A readable transcript was later recovered from the local VS Code session archive: [Milestone 2 event and notification conversation](transcripts/04-milestone2-event-and-notification.md). It preserves all three prompts and visible Copilot responses, along with tool names/arguments and completion status. Detailed tool result payloads are absent from that archive and require manual VS Code export for complete raw-output fidelity.