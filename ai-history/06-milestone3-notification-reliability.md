# Milestone 3: Notification Delivery Reliability

## Scope

Implemented backend notification-delivery reliability under accepted ADR-016, ADR-017, ADR-023, and ADR-024. No accepted ADR was changed. No frontend, live SMTP or Slack transport, external network call, credential, message broker, or new package dependency was introduced.

## Implementation

- Added `INotificationDeliveryRecorder` as the minimal operation boundary for deterministic failure simulation. Its production default is a no-op recording adapter; successful simulation persists `Recorded` and does not mark the delivery `Sent`.
- Delivery processing now atomically claims due pending rows, increments the persisted attempt count at claim time, and conditionally finalizes the same attempt. This prevents multiple workers from claiming the same pending row and preserves existing delivery-row idempotency.
- Unclassified delivery exceptions and explicitly transient `NotificationDeliveryException` failures retry with persisted exponential backoff: one second after attempt one, two seconds after attempt two, bounded to 30 seconds. Three is the maximum total attempt count. Exhaustion marks the row `Failed`; an explicitly permanent exception marks it `Failed` immediately. Both terminal paths persist the error and clear the next-attempt time.
- A `Processing` row stale for more than one minute is recovered. Below the attempt limit it returns to `Pending`; at the limit it becomes `Failed`. No schema change was required because attempt count, next-attempt time, last error, and update time already exist.
- Added focused tests for recording success, transient failure and retry, retry exhaustion, immediate permanent failure, restart recovery, and concurrent worker safety.

## TDD and Validation

- The initial new-test run failed to compile because the recorder abstraction was not implemented yet (5 missing-type errors), establishing the expected red state for the new injection boundary.
- After implementation and one nullable timestamp compile correction, the worker focused tests passed **6/6**.
- A requested five-run repetition initially failed on `ConcurrentWorkers_ClaimDeliveryOnlyOnce`, leaving the row `Processing` at attempt one. The cause was test teardown: it stopped the second worker immediately after releasing the recorder, potentially canceling the worker's final `Recorded` update. The test now waits for persisted `Recorded` before shutdown; no timeout was increased.
- After this correction, the complete worker test class passed **five consecutive runs**, each **6 succeeded, 0 failed, 0 skipped**.
- Full backend test suite after the correction: **116 succeeded, 0 failed, 0 skipped**.
- Solution build after the correction: succeeded.
- `git diff --check`: passed.

## Remaining Risks

- Restart recovery has a one-minute staleness threshold, plus up to one polling interval of delay, based on persisted `UpdatedAtUtc`; no explicit lease or heartbeat was added.
- Exactly-once external delivery is not guaranteed by ADR-016. This implementation remains recording-only and has no external side effects.
- Unclassified exceptions are assumed retryable; future adapters must classify permanent failures explicitly.
- Frontend behavior was not changed or validated.

No approval-dependent decisions were made. The one-minute recovery threshold and the recording-only test seam are implementation details within the accepted ADR boundaries.

Readable original-session transcript: [Milestone 3 notification reliability conversation](transcripts/05-milestone3-notification-reliability.md). The transcript records tool commands and completion statuses; selected detailed validation outputs available in the active conversation are included in its appendix.