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

## Pending Decisions

- Event source integration
- Database technology
- Alert rule model
- Notification channel architecture
- Authentication and authorization
- Administration capabilities
- Error handling and retry strategy
