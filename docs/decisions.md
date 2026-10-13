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
