# AI Session 02 - Implementation Planning

## Tool
GitHub Copilot Agent Mode

## Prompt
You are acting as a senior .NET and Angular engineer working under my technical direction.

# Project
Sonrisa Event Alert System

# Context
We have completed the initial AI-assisted requirements analysis and architectural review.

The architectural decisions ADR-001 through ADR-011 have been reviewed and accepted.

Before proposing anything, inspect:

- README.md
- docs/plan.md
- docs/decisions.md
- docs/validation-log.md
- The existing backend and frontend project structures
Treat the accepted architectural decisions as constraints. Do not silently change them.

# Your Task
Create a detailed, incremental implementation plan for the approved MVP.

Cover the following:

1. **Domain model**

- AlertRule, Event, NotificationDelivery, and predefined demo identities
- Entity relationships and important constraints
- Duplicate event and notification handling
2. **Backend design**

- Recommended project structure
- API endpoints and request/response DTOs
- Alert matching service
- Notification channel abstraction
- Email, Slack, and recording adapters
- BackgroundService for asynchronous delivery
- Entity Framework Core with SQLite
3. **Alert matching semantics**

- Category matching
- Keyword matching
- Case sensitivity
- Empty keywords
- Disabled rules
- Multiple matching rules
- Multiple notification channels
4. **Notification processing**

- Delivery statuses
- Recording mode versus actual delivery
- Retry limits and failure handling
- Duplicate prevention
- Behavior when external credentials are unavailable
5. **Angular frontend**

- Minimal user interface for managing alert rules
- Minimal administration interface for submitting and viewing events
- Notification delivery status display
- Simple navigation and demo identity selection
6. **Testing strategy**

- Rule matching tests
- Validation tests
- Duplicate handling tests
- Notification adapter tests
- Delivery status transition tests
- Relevant API tests
7. **Implementation milestones**
For each milestone, specify:

- Goal
- Files or components to create or modify
- Dependencies
- Acceptance criteria
- Build or test commands
- Suggested Git commit message
8. **Critical review**

- Identify unnecessary complexity
- Highlight unresolved implementation details
- Recommend pragmatic shortcuts
- Clearly identify what should be deferred

# Constraints

- ASP.NET Core Web API and Angular
- Modular monolith
- Manual event ingestion
- Category and keyword matching
- Entity Framework Core with SQLite
- Extensible email and Slack channels
- Database-backed asynchronous notification processing
- Predefined demo identities for local demonstration only
- No production deployment
- Limited implementation time
- Prefer a working, tested vertical slice over a large incomplete implementation

# Important
Do NOT modify any files.
Do NOT generate application code.
Do NOT install packages.
Do NOT run builds, tests, or migrations.
Do NOT begin implementation.

Present the implementation plan in a structured format.

Explicitly identify any decisions requiring my approval.

Wait for my review before proceeding.

## AI Response
File: 02-implementation-plan.json

## Developer Review

I reviewed the AI-generated implementation plan against the approved MVP scope and architectural decisions.

The proposed five-milestone approach is appropriate for the available development time. I agree with prioritizing a working, tested backend event-to-notification workflow before implementing the Angular interface.

I reviewed and approved all seven implementation-level decisions, covering alert matching, duplicate detection, rule deletion, demo identities, retry behavior, notification transports, and database migrations.

The AI-generated plan is accepted as the basis for implementation, subject to incremental code review, automated testing, and validation at each milestone.

No application code was modified during this planning session.

## Accepted Recommendations

The following implementation decisions were reviewed and approved by the developer:

1. **Alert matching:** Case-insensitive category and keyword matching. AND between category and keywords, OR between multiple keywords. Empty conditions are unconstrained, and disabled rules never match.

2. **Duplicate events:** Identify duplicates using a fingerprint based on normalized category, title, description, and occurrence time. Return the existing event without creating additional notifications.

3. **Rule deletion:** Use logical deletion to preserve notification history. Deleted rules must not generate new notifications.

4. **Demo identities:** Use predefined `demo-user` and `demo-admin` identities, selected through the `X-Demo-Identity` HTTP header. Enforce demo roles on the backend. This is not production authentication.

5. **Retry policy:** Allow a maximum of three delivery attempts, including the initial attempt. Retry transient failures with bounded backoff, handle permanent failures, and recover interrupted processing.

6. **Notification channels:** Use SMTP for email and Slack Incoming Webhooks for Slack. Default to recording mode, distinguish `Recorded` from `Sent`, and never silently fall back to recording when live delivery is configured.

7. **Database migrations:** Use Entity Framework Core migrations, commit migration files to Git, and apply migrations explicitly rather than automatically at application startup.

## Rejected or Modified Recommendations

No major architectural recommendations were rejected during this planning session.

## Follow-up Actions

1. Save the complete AI-generated implementation plan as evidence.
2. Update the architecture decision log with the seven approved implementation details.
3. Implement the MVP incrementally, following the approved milestones.
4. Review and test AI-generated code before each Git commit.
5. Document validation results and known limitations.
