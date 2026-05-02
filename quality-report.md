# Quality Report

## Scope

- Final quality audit of the roadmap/planning frontend changes
- Verification of backend standards for Correlation ID, friendly error messaging, slow alert logging, and database retry/initialization
- Clean Code review focused on duplication, reuse, payload consistency, and cyclomatic hotspots
- Minimum regression coverage review for the most recent roadmap fixes

## Standards Checked

- Clean Code and reuse of shared rules
- Cyclomatic complexity and local maintainability hotspots
- Unit test coverage on newly introduced business rules
- Robust logs with Correlation ID propagation
- Friendly user-facing error messages carrying Correlation ID
- Database connection retry and startup resilience
- Slow request alerting
- Safe handling of invalid persistence payloads

## Checks Performed

### Backend

- Reviewed correlation middleware and exception middleware to confirm request/response propagation of `X-Correlation-ID`
- Reviewed slow request MediatR behavior for warning-level observability on long-running handlers
- Reviewed SQL Server configuration and startup initialization for retry and timeout behavior
- Reviewed API startup pipeline ordering to confirm Correlation ID is established before exception handling

Files reviewed:

- `backend/src/ProductHub.API/Middleware/CorrelationIdMiddleware.cs`
- `backend/src/ProductHub.API/Middleware/ExceptionHandlingMiddleware.cs`
- `backend/src/ProductHub.Application/Common/Behaviors/SlowRequestBehavior.cs`
- `backend/src/ProductHub.Infrastructure/DependencyInjection.cs`
- `backend/src/ProductHub.API/Program.cs`

### Frontend

- Reviewed centralized HTTP error handling and slow request alert flow
- Reviewed roadmap store payload construction for create, update, and status patch flows
- Reviewed roadmap demand modal submit logic for item-type-specific sanitization
- Reviewed planning and hierarchy views for duplicated promised-date derivation rules
- Reviewed current frontend test coverage availability for roadmap-specific logic

Files reviewed:

- `frontend/app/composables/useApi.ts`
- `frontend/app/stores/roadmap.ts`
- `frontend/app/components/roadmap/DemandFormModal.vue`
- `frontend/app/pages/roadmap.vue`
- `frontend/app/components/roadmap/RoadmapHierarchyPage.vue`

## Findings And Corrections Applied

### 1. Payload sanitization was duplicated and inconsistent across layers

Problem:

- The modal already sanitized part of the `Demand` payload, but the store still rebuilt payloads independently in `createDemand`, `updateDemand`, and `patchDemandStatus`.
- That duplication increased cyclomatic complexity and reopened the risk of sending invalid `customers`, `issueLinks`, or `promisedDate` values depending on the entry point.

Correction:

- Introduced a shared helper for roadmap mutation payload rules.
- Centralized these invariants:
	- `Demand` must not keep customers
	- backlog `Demand` must not send promised date
	- `Roadmap` must not send issue links
	- common create/update/status-patch payload shapes are built in one place
- Updated both the store and the modal to reuse the same sanitization logic.

Files changed:

- `frontend/app/utils/roadmapDemandPayload.ts`
- `frontend/app/stores/roadmap.ts`
- `frontend/app/components/roadmap/DemandFormModal.vue`

Quality impact:

- Better reuse
- Lower risk of frontend/backend payload drift
- Reduced local complexity in the store

### 2. Promised-date derivation logic was duplicated between Planning and Hierarchy

Problem:

- The quarter fallback and latest promised-date derivation rules existed in more than one view.
- That made future divergence likely and weakened maintainability.

Correction:

- Extracted the shared promised-date derivation logic into a reusable utility.
- Planning and hierarchy now consume the same helper for quarter-end fallback and “latest child date” derivation.

Files changed:

- `frontend/app/utils/roadmapPromisedDate.ts`
- `frontend/app/pages/roadmap.vue`
- `frontend/app/components/roadmap/RoadmapHierarchyPage.vue`

Quality impact:

- Stronger rule consistency across screens
- Lower duplication
- Easier unit testing of date derivation behavior

### 3. Unit-test gap on the newly introduced frontend rules

Problem:

- The workspace had no frontend unit tests covering roadmap payload sanitization or promised-date derivation.

Correction:

- Added focused unit tests for:
	- backlog demand detection
	- customer sanitization by item type
	- roadmap issue-link suppression
	- promised-date normalization
	- create/update/status payload building
	- quarter fallback and latest promised-date derivation

Files changed:

- `frontend/app/utils/roadmapDemandPayload.test.ts`

Quality impact:

- The most fragile roadmap rules are now isolated in pure helpers with direct coverage

## Standards Status

### Correlation ID and user messaging

- Verified as consistent.
- Backend middleware returns friendly problem responses with `correlationId`.
- Frontend API composable logs the correlation id and shows it to the user in the toast message.

### Slow alert

- Verified as consistent.
- Backend keeps warning-level slow request logging in the pipeline.
- Frontend keeps a user-facing slow-request warning and structured client log entry.

### Database connection handling and retry

- Verified as partially covered and acceptable for the current architecture.
- SQL Server uses retry-on-failure and command timeout through shared constants.
- Startup initialization retries database creation/schema update when a persistent SQL connection is configured.

### Reuse and Clean Code

- Improved during this pass.
- Payload construction and promised-date rules are now centralized instead of duplicated.

## Tests Added

### Frontend unit tests

- Added coverage for roadmap payload and promised-date helper rules.

File:

- `frontend/app/utils/roadmapDemandPayload.test.ts`

## Validation Result

### Editor diagnostics

- No errors reported in the files changed during this pass:
	- `frontend/app/utils/roadmapDemandPayload.ts`
	- `frontend/app/utils/roadmapPromisedDate.ts`
	- `frontend/app/utils/roadmapDemandPayload.test.ts`
	- `frontend/app/stores/roadmap.ts`
	- `frontend/app/components/roadmap/DemandFormModal.vue`
	- `frontend/app/pages/roadmap.vue`
	- `frontend/app/components/roadmap/RoadmapHierarchyPage.vue`

### Executable validation

- Frontend focused unit test execution was attempted, but the local Node runtime is too old for the installed Vitest version and fails on the `??=` operator before running tests.
- Backend test execution was attempted, but the environment does not have a .NET SDK installed.

Environment blockers observed:

- Node runtime incompatible with current Vitest CLI syntax
- Missing .NET SDK in the current machine/session

## Residual Risks / Follow-up

- `frontend/app/pages/roadmap.vue` remains the main cyclomatic complexity hotspot even after the recent cleanup. It is functionally improved, but still large enough to justify extraction into smaller composables/components.
- Frontend executable test coverage is still narrow and currently blocked by the local Node runtime version.
- Backend executable validation could not be completed in this environment because the .NET SDK is unavailable.
- Database startup still relies on `EnsureCreatedAsync`; for long-term persistent environments, migrations remain the recommended next step.

## Final Status

- This quality pass found and corrected concrete inconsistencies in frontend payload handling and duplicated date-derivation rules.
- Correlation ID propagation, friendly error handling, slow alert logging, and database retry behavior remain aligned with the project standards based on code inspection.
- The codebase is in a better state for reuse and maintainability than before this pass, but full executable validation is still pending environment readiness.