# Architecture Decision Log

This document records technical decisions, their rationale, considered alternatives, and potential consequences.

## ADR-001: Technology Stack

**Status:** Accepted

**Decision:** Use ASP.NET Core Web API for the backend and Angular for the frontend.

**Rationale:**
- Existing development experience with both technologies
- Strong typing and mature development ecosystems
- Suitable for modular API-based applications
- Reduced implementation overhead within the available timeframe

**Alternatives:** Node.js backend, React frontend.

**Consequences:**
- Faster initial development using familiar technologies
- Separate frontend and backend build processes

---

## ADR-002: Angular Application Structure

**Status:** Accepted for MVP

**Decision:** Start with a single Angular application rather than a multi-application workspace.

**Rationale:**
- Only one application is currently required for the MVP
- User and administration views can initially share the same application
- Avoid premature architectural complexity
- Follow the YAGNI principle

**Alternatives:**
- Multi-application Angular workspace
- Nx monorepo

**Consequences:**
- Simpler initial setup
- Future restructuring may be necessary if additional applications are introduced

---

## ADR-003: Incremental Development

**Status:** Accepted

**Decision:** Develop the solution incrementally, using separate Git commits for major milestones.

**Rationale:**
- Makes the development process traceable
- Supports easier review of implementation decisions
- Allows AI-generated changes to be evaluated before acceptance

**Consequences:**
- More structured development history
- Additional documentation effort

---

## Remaining Implementation Details

The main architectural decisions have been accepted. The following implementation details will be defined during the next planning session:

- API endpoints and request/response DTOs.
- Database entities, relationships, and constraints.
- Exact category and keyword matching behavior.
- Notification delivery statuses, retry limits, and duplicate handling.
- Email and Slack adapter configuration.
- Angular components and navigation.
- Automated test scenarios and acceptance criteria.

---

## ADR-004: Event Ingestion Strategy

**Status:** Accepted

**Decision:** Use manual event submission through the Web API for the MVP.

**Rationale:**
- No external event provider is specified.
- Avoids dependencies on third-party APIs and credentials.
- Enables deterministic end-to-end testing.
- Keeps event ingestion independent of future providers.

**Trade-off:** The MVP does not automatically detect real-world events.

---

## ADR-005: Alert Rules

**Status:** Accepted

**Decision:** Support event category and keyword matching in the MVP.

**Rationale:** These conditions are understandable, testable, and sufficient to demonstrate configurable alerting.

**Deferred:** Market thresholds, advanced expressions, and AI-based importance scoring.

---

## ADR-006: Authentication and Authorization

**Status:** Accepted

**Decision:** Use predefined demo identities and roles for local demonstration only.

**Rationale:** A production identity solution would require additional time and infrastructure.

**Risk:** Demo identity selection is not secure authentication.

**Restriction:** The application must not be publicly deployed with this approach.

---

## ADR-007: Notification Channels

**Status:** Accepted

**Decision:** Introduce a shared notification channel abstraction with email and Slack implementations.

Use recording adapters when real delivery credentials are unavailable.

**Rationale:** Enables isolated testing and future extension.

**Trade-off:** Recorded notifications are not proof of actual delivery.

---

## ADR-008: Delivery Processing

**Status:** Accepted

**Decision:** Persist notification attempts and process them asynchronously using an ASP.NET Core BackgroundService.

Use bounded retries and track delivery status.

**Rationale:** Separates API responsiveness from external provider availability.

**Trade-off:** Retry handling and duplicate prevention require additional validation.

---

## ADR-009: User and Administration Views

**Status:** Accepted

**Decision:** Use a single Angular application.

Users can manage alert rules. The administration view displays events and notification delivery status.

**Rationale:** Avoids unnecessary multi-application infrastructure.

---

## ADR-010: Persistence

**Status:** Accepted

**Decision:** Use SQLite with Entity Framework Core for the MVP.

**Rationale:** Simple local setup, relational persistence, and no separate database server.

**Trade-off:** The deployment and concurrency requirements of a larger production system may require another database.

---

## ADR-011: MVP Deployment Scope

**Status:** Accepted

**Decision:** Target local demonstration rather than production deployment.

**Rationale:** Authentication, infrastructure, retention policies, and delivery credentials are not fully specified.

**Consequence:** Production readiness remains explicitly out of scope.

---

## ADR-012: Alert Matching Semantics

**Status:** Accepted

**Decision:** Use case-insensitive category and keyword matching. Category and keyword conditions use AND logic, while multiple keywords use OR logic.

An empty category matches any category. An empty keyword list imposes no keyword restriction. Disabled or logically deleted rules never match.

**Rationale:** Simple, predictable, and testable matching behavior is sufficient for the MVP.

**Deferred:** Advanced rule expressions and AI-based relevance scoring.

---

## ADR-013: Duplicate Event Handling

**Status:** Accepted

**Decision:** Identify duplicate events using a fingerprint generated from the normalized event category, title, description, and occurrence time.

If an identical event is submitted again, the API returns the existing event with a duplicate indicator.

Duplicate submissions must not create additional notification deliveries.

Enforce event fingerprint uniqueness and delivery uniqueness at the database level.

**Rationale:** Prevents repeated event processing and unnecessary duplicate notifications while keeping the MVP implementation simple.

**Limitation:** Two genuinely distinct events with identical fingerprint fields may be treated as duplicates.

---

## ADR-014: Logical Rule Deletion

**Status:** Accepted

**Decision:** Use logical deletion for alert rules. Deleted rules must not participate in future event matching or generate new notifications.

Preserve existing notification delivery records and their relationship to deleted rules.

**Rationale:** Maintains notification history and avoids accidental loss of audit information.

---

## ADR-015: Demo Identity Handling

**Status:** Accepted

**Decision:** Use predefined `demo-user` and `demo-admin` identities for local demonstration.

The frontend sends the selected identity through the `X-Demo-Identity` HTTP header. The backend validates the identity and enforces the corresponding demo role.

**Rationale:** Enables demonstration of user and administration workflows without implementing a production authentication system.

**Restriction:** This mechanism is not secure authentication and must not be used in a publicly deployed application.

---

## ADR-016: Notification Retry Policy

**Status:** Accepted

**Decision:** Allow a maximum of three notification delivery attempts, including the initial attempt.

Retry transient failures with bounded backoff. Treat permanent failures as terminal and mark deliveries as `Failed`.

Persist delivery attempts and recover interrupted processing after application restart.

**Rationale:** Provides basic resilience without introducing a message broker or complex distributed processing.

**Limitation:** Exactly-once external delivery is not guaranteed.

---

## ADR-017: Email and Slack Delivery

**Status:** Accepted

**Decision:** Use SMTP for email delivery and Slack Incoming Webhooks for Slack notifications.

Use recording mode by default for local demonstrations. Distinguish simulated delivery (`Recorded`) from successful live sending (`Sent`).

When live delivery is configured, missing credentials must produce an explicit configuration error rather than silently falling back to recording mode.

**Rationale:** Supports both requested notification channels while allowing development and testing without external credentials.

---

## ADR-018: Database Migration Strategy

**Status:** Accepted

**Decision:** Manage the SQLite database schema using Entity Framework Core migrations.

Commit migration files to Git and apply migrations explicitly during local setup rather than automatically at application startup.

**Rationale:** Keeps schema changes version-controlled, reviewable, and reproducible.

---

## ADR-019: Milestone 2 API Scope

**Status:** Accepted

**Decision:** Milestone 2 will provide the minimal proposed API operations needed for alert-rule create, read, update, and logical delete; event submission and querying; notification delivery status querying; and demo identity lookup.

Keep these API operations suitable for consumption by the later Angular frontend. Do not expose Entity Framework entities directly; use explicit request and response DTOs.

**Rationale:** Delivers the approved backend vertical slice while establishing the API surface needed by the planned user and administration workflows.

**Scope boundary:** This decision does not add live notification delivery, production authentication, or Angular implementation to Milestone 2.

---

## ADR-020: API Validation and Demo Identity Authorization

**Status:** Accepted

**Decision:** Validate `X-Demo-Identity` on the backend for protected operations. Only `demo-admin` may submit events. Demo users may create, read, update, and logically delete their own alert rules. Use explicit request and response DTOs, validate incoming requests, and return appropriate HTTP error responses for invalid requests and unauthorized demo identities or roles.

This mechanism is for local demonstration only. Do not introduce production authentication or authorization infrastructure.

**Rationale:** Enforces the approved local-demo user/admin boundary server-side and gives the Angular frontend a stable, validated API contract.

**Restriction:** Demo identity selection is not secure authentication and must not be used for public deployment, consistent with ADR-006, ADR-011, and ADR-015.

---

## ADR-021: Event Fingerprint Algorithm

**Status:** Accepted

**Decision:** Generate event fingerprints deterministically using SHA-256 over normalized category, title, description, and occurrence timestamp values, following ADR-013. Persist the resulting fingerprint and rely on the existing database uniqueness constraint to prevent duplicate event creation and additional deliveries.

The canonicalization and byte encoding for this approved algorithm are specified in ADR-022.

**Rationale:** A cryptographic digest provides a fixed-size key for the existing unique fingerprint constraint while keeping event identity derived from the fields approved in ADR-013.

---

## ADR-022: Event Fingerprint Canonicalization

**Status:** Accepted

**Decision:** Canonicalize the fingerprint inputs as follows:

1. Reject null category, title, description, or occurrence timestamp values as invalid input. Category and title are required and must remain non-empty after normalization. Description is required as a value but may be the empty string. Normalize each text value to Unicode NFC.
2. For each text value, trim leading and trailing Unicode whitespace and replace every internal run of Unicode whitespace with one ASCII space (`U+0020`).
3. Convert each normalized text value to invariant uppercase using the application's .NET runtime, so casing differences do not produce different fingerprints.
4. Require an occurrence timestamp that includes an explicit UTC offset and convert it to UTC. Truncate subsecond values, without rounding, to the whole-second boundary at or before the UTC instant. Represent that instant as signed seconds since the Unix epoch.
5. Construct the byte sequence in this exact order: the ASCII bytes for `sonrisa-event-v1` followed by one zero byte; category, title, and description, in that order, each encoded as UTF-8 and preceded by its byte length as an unsigned 32-bit big-endian integer; then the UTC Unix-second value as a signed 64-bit big-endian integer. Hash the complete byte sequence with SHA-256 and encode the 32-byte digest as 64 lowercase hexadecimal characters.

**Rationale:** NFC and explicit whitespace/casing rules make text equivalence predictable; length-prefixed fields avoid delimiter ambiguity; truncated UTC Unix seconds provide a culture-independent, whole-second timestamp representation; and the version prefix permits a future canonicalization change to be distinguished.

**Approval:** The developer reviewed and approved this canonicalization and byte-level encoding.

---

## ADR-023: Milestone 2 Recording Channels

**Status:** Accepted

**Decision:** Support Email and Slack notification channels through recording-mode adapters in Milestone 2. These adapters must not make external network calls. A rule with no configured notification channels produces no notification deliveries.

Recording a delivery represents simulated processing, not successful external delivery. Live SMTP and Slack Incoming Webhook transports remain Milestone 3 work, consistent with ADR-017.

**Rationale:** Enables a deterministic local end-to-end workflow without external credentials or network dependencies while honoring the configured channels on each rule.

---

## ADR-024: Milestone 2 Delivery Worker Boundary

**Status:** Accepted

**Decision:** In Milestone 2, process persisted notification deliveries asynchronously using the approved background-worker approach. The Milestone 2 status flow is `Pending` to `Processing` to `Recorded` for recording-mode delivery.

Bounded retries and live SMTP/Slack transports are deferred to Milestone 3. Recovery of deliveries left in `Processing` after an application restart is also deferred to Milestone 3. Do not add a message broker or separate service for this workflow.

**Rationale:** Establishes the database-backed asynchronous delivery vertical slice without pulling the later resilience and live-transport work into Milestone 2.

---

## ADR-025: Milestone 2 HTTP API Contract

**Status:** Accepted

**Decision:** Expose these nine JSON API operations using explicit DTOs, not EF entities:

- `GET /api/demo-identities` — public; return `id`, `displayName`, and `role` (`200`).
- `GET /api/alert-rules` — `demo-user`; return only their non-deleted rules (`200`).
- `POST /api/alert-rules` — `demo-user`; create an owned rule (`201`).
- `PUT /api/alert-rules/{id}` — `demo-user`; full replacement of editable fields (`200`).
- `DELETE /api/alert-rules/{id}` — `demo-user`; logical deletion (`204`).
- `POST /api/events` — `demo-admin`; new event (`201`), duplicate (`200`).
- `GET /api/events` and `GET /api/events/{id}` — `demo-admin`; event list and detail (`200`).
- `GET /api/notification-deliveries` — `demo-admin`; delivery status list (`200`).

Rule write DTOs contain `name` (required, max 120), `category` (nullable, max 100), `keywords` (string array, max 100 per item), `channels` (`Email` or `Slack`), and `isEnabled`. On create, omitted arrays default to empty and omitted `isEnabled` to true; PUT replaces all editable fields. Rule responses contain `id`, `name`, `category`, `keywords`, `channels`, `isEnabled`, `createdAtUtc`, and `updatedAtUtc`. The identity header supplies ownership; clients cannot set `UserId`.

Event create DTOs contain required `category` (max 100), `title` (max 300), `description` (max 4000; empty permitted), and `occurredAt` (ISO-8601 timestamp with an explicit offset). Preserve submitted event text; convert the timestamp to UTC for persistence and use ADR-022 only for fingerprint calculation. Event responses contain `id`, `category`, `title`, `description`, `occurredAtUtc`, and `createdAtUtc`; submission responses also contain `isDuplicate`. Event detail includes its delivery DTOs. Delivery DTOs expose `id`, `eventId`, `alertRuleId`, `channel`, `status`, `attemptCount`, `createdAtUtc`, `updatedAtUtc`, and `sentAtUtc`.

Category values are free text, not an enum. Null, empty, or whitespace-only rule category means unrestricted. Trim rule keywords; reject null or blank entries and duplicates case-insensitively after trimming. Empty keyword and channel arrays are valid. Channel values must be distinct `Email` or `Slack` values. Enforce the configured field limits at the API boundary.

Resolve `X-Demo-Identity` case-insensitively against the server-side registry and derive the role there. Missing or unknown identities return `401`; a valid identity with an unauthorized role returns `403`; missing or non-owned resources return `404`; invalid input returns ASP.NET Core `ValidationProblemDetails` with `400`; other uniqueness conflicts return `409`. `demo-user` manages only their own rules. `demo-admin` submits and queries events and queries delivery statuses; admin does not manage alert rules. Demo identity lookup does not require the header.

List queries use 1-based `page` (default 1) and `pageSize` (default 20, maximum 100), returning `items`, `page`, `pageSize`, and `totalCount`. Event filters are `category`, `fromUtc` (inclusive), and `toUtc` (exclusive); order by `occurredAtUtc` descending then `id` descending. Delivery filters are `eventId`, `status`, and `channel`; order by `createdAtUtc` descending then `id` descending. Invalid pagination or filters return `400`.

API integration tests use `WebApplicationFactory`.

**Rationale:** Defines the minimal backend surface needed by the approved Milestone 2 workflow and the later Angular client.

---

## ADR-026: Atomic Event and Delivery Creation

**Status:** Accepted

**Decision:** Persist a newly submitted event and all matching notification delivery rows atomically. A new event returns `201` with `isDuplicate: false`. A duplicate submission creates no additional deliveries and returns the existing event with `isDuplicate: true` and `200`.

When concurrent submissions race on the unique event fingerprint, handle only the specific fingerprint uniqueness violation as a duplicate: reload the existing event and return the duplicate response. Do not classify generic database exceptions as duplicates; propagate or handle other persistence failures as errors.

**Rationale:** Preserves a consistent event-to-delivery queue and makes concurrent idempotency follow ADR-013 and the fingerprint algorithm in ADR-021/ADR-022.

