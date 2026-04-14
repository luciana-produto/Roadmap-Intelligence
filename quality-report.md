# Quality Report

## Scope

- Roadmap demand customer model migration (`string` -> `string[]`)
- Roadmap Kanban and list UX adjustments
- API/database resilience and observability alignment with project standards

## Checks Performed

- Clean Code review of modified backend and frontend files
- Consistency review against project standards for Correlation ID, slow alert, friendly messages, retry, and component reuse
- Static validation using workspace diagnostics on modified files
- Backend test execution attempt

## Corrections Applied

### Backend

#### Customer persistence and null safety

- `Customers` remains normalized as a collection in the domain layer.
- EF Core value conversion/comparison was corrected to avoid build/runtime failures with expression trees and null inputs.
- Empty or missing customers now persist safely without failing create demand.

Files:

- [backend/src/ProductHub.Domain/Roadmap/RoadmapDemand.cs](backend/src/ProductHub.Domain/Roadmap/RoadmapDemand.cs)
- [backend/src/ProductHub.Infrastructure/Persistence/Configurations/RoadmapDemandConfiguration.cs](backend/src/ProductHub.Infrastructure/Persistence/Configurations/RoadmapDemandConfiguration.cs)

#### Database retry and timeout standardization

- SQL retry count, delay and command timeout now use shared constants instead of hardcoded values.
- API startup database initialization now retries when a persistent database is configured, reducing transient startup failures.

Files:

- [backend/src/ProductHub.Infrastructure/DependencyInjection.cs](backend/src/ProductHub.Infrastructure/DependencyInjection.cs)
- [backend/src/ProductHub.API/Program.cs](backend/src/ProductHub.API/Program.cs)

#### Logging and user-facing error flow

- Correlation ID, friendly error messages, and problem response handling remain aligned with middleware standards.
- Slow request behavior remains active in MediatR pipeline.

Files reviewed:

- [backend/src/ProductHub.API/Middleware/CorrelationIdMiddleware.cs](backend/src/ProductHub.API/Middleware/CorrelationIdMiddleware.cs)
- [backend/src/ProductHub.API/Middleware/ExceptionHandlingMiddleware.cs](backend/src/ProductHub.API/Middleware/ExceptionHandlingMiddleware.cs)
- [backend/src/ProductHub.Application/Common/Behaviors/SlowRequestBehavior.cs](backend/src/ProductHub.Application/Common/Behaviors/SlowRequestBehavior.cs)

### Frontend

#### Reuse and consistency

- API access continues centralized in a single composable.
- Added structured logging in `useApi` with correlation-aware context for slow requests and HTTP failures.
- Friendly messages with correlation ID remain intact in the toast layer.

Files:

- [frontend/app/composables/useApi.ts](frontend/app/composables/useApi.ts)
- [frontend/app/composables/useLogger.ts](frontend/app/composables/useLogger.ts)

#### Roadmap UX quality adjustments

- Kanban filter action no longer creates an extra row for `Limpar filtros`.
- Status notes use a compact visual indicator plus tooltip in Kanban and list views.
- List column order now follows the requested business order.
- Classification badge moved to the top-right area of the Kanban card.

Files:

- [frontend/app/pages/roadmap.vue](frontend/app/pages/roadmap.vue)
- [frontend/app/components/roadmap/DemandCard.vue](frontend/app/components/roadmap/DemandCard.vue)
- [frontend/app/components/roadmap/DemandFormModal.vue](frontend/app/components/roadmap/DemandFormModal.vue)

## Tests Added

### Domain tests

- Customer normalization removes duplicates, trims whitespace, and ignores blanks.
- Updating with null customers clears the collection safely.

File:

- [backend/tests/ProductHub.Domain.Tests/Roadmap/RoadmapDemandTests.cs](backend/tests/ProductHub.Domain.Tests/Roadmap/RoadmapDemandTests.cs)

### Infrastructure tests

- Saving and reloading a demand with empty customers preserves an empty collection without failures.

File:

- [backend/tests/ProductHub.Infrastructure.Tests/Configurations/RoadmapDemandConfigurationTests.cs](backend/tests/ProductHub.Infrastructure.Tests/Configurations/RoadmapDemandConfigurationTests.cs)

## Validation Result

- Workspace diagnostics on modified files: no reported errors after final adjustments.
- Backend automated test execution could not be run in this environment because no .NET SDK is installed.

## Residual Risks / Follow-up

- `frontend/app/pages/roadmap.vue` is still a high-complexity page and remains the main cyclomatic complexity hotspot. Functionally consistent, but a future refactor should extract list-table state and Kanban filters into composables/components.
- Frontend roadmap-specific unit tests are still absent. Recommended next step is to cover the roadmap store/service boundary and key filter helpers.
- Database initialization currently uses `EnsureCreatedAsync`; when SQL schema evolution starts, this should move to migrations-based startup/update flow.

## Final Status

- Quality inconsistencies identified during this pass were corrected.
- The code now better aligns with the project standards for clean code, retry handling, Correlation ID propagation, friendly user messaging, slow alert observability, and targeted unit coverage.