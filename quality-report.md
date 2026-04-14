# Quality Report

## Scope

- Roadmap list and Kanban workflow refinements
- Bulk quarter movement and backlog planning behavior
- Cross-project demand dependency support
- API/database resilience and observability alignment with project standards
- Clean Code, cyclomatic complexity hotspots, reuse, and minimum regression coverage

## Checks Performed

- Clean Code review of modified backend and frontend files
- Consistency review against project standards for Correlation ID, slow alert, friendly user messages, retry, and component reuse
- Review of recent roadmap flows: reprioritization, backlog grouping, bulk movement, and dependency linking
- Static validation using workspace diagnostics on modified files
- Backend test execution feasibility check in terminal

## Corrections Applied

### Backend

#### Dependency model between projects

- Added an explicit persistence model for demand dependencies instead of embedding ad-hoc references.
- Dependency DTOs now expose both sides of the relation:
	incoming: `dependedOnBy`
	outgoing: `dependsOn`
- Added a dedicated query endpoint for dependency options so the form can bind demands from any project without duplicating query logic in the UI.

Files:

- [backend/src/ProductHub.Domain/Roadmap/RoadmapDemandDependency.cs](backend/src/ProductHub.Domain/Roadmap/RoadmapDemandDependency.cs)
- [backend/src/ProductHub.Infrastructure/Persistence/Configurations/RoadmapDemandDependencyConfiguration.cs](backend/src/ProductHub.Infrastructure/Persistence/Configurations/RoadmapDemandDependencyConfiguration.cs)
- [backend/src/ProductHub.Infrastructure/Repositories/RoadmapDemandRepository.cs](backend/src/ProductHub.Infrastructure/Repositories/RoadmapDemandRepository.cs)
- [backend/src/ProductHub.Application/Roadmap/DTOs/RoadmapDtos.cs](backend/src/ProductHub.Application/Roadmap/DTOs/RoadmapDtos.cs)
- [backend/src/ProductHub.Application/Roadmap/DTOs/RoadmapDemandDtoMapper.cs](backend/src/ProductHub.Application/Roadmap/DTOs/RoadmapDemandDtoMapper.cs)
- [backend/src/ProductHub.Application/Roadmap/Queries/GetDependencyOptions/GetDependencyOptionsQueryHandler.cs](backend/src/ProductHub.Application/Roadmap/Queries/GetDependencyOptions/GetDependencyOptionsQueryHandler.cs)
- [backend/src/ProductHub.API/Controllers/RoadmapController.cs](backend/src/ProductHub.API/Controllers/RoadmapController.cs)

#### Project immutability consistency

- Identified and corrected a behavioral inconsistency: the edit flow allowed changing the project in the UI, but the backend domain/update flow did not support moving a demand between projects.
- The backend now fails explicitly with validation when `ProjectId` is changed during update instead of silently ignoring the value.

Files:

- [backend/src/ProductHub.Application/Roadmap/Commands/UpdateDemand/UpdateRoadmapDemandCommandHandler.cs](backend/src/ProductHub.Application/Roadmap/Commands/UpdateDemand/UpdateRoadmapDemandCommandHandler.cs)

#### Database retry and timeout standardization

- SQL retry count, delay, and command timeout remain standardized through shared constants.
- API startup database initialization still retries when a persistent database is configured, reducing transient startup failures.

Files reviewed:

- [backend/src/ProductHub.Infrastructure/DependencyInjection.cs](backend/src/ProductHub.Infrastructure/DependencyInjection.cs)
- [backend/src/ProductHub.API/Program.cs](backend/src/ProductHub.API/Program.cs)

#### Logging and user-facing error flow

- Correlation ID propagation, friendly error messages, and problem responses remain aligned with middleware standards.
- Slow request behavior remains active in the MediatR pipeline.

Files reviewed:

- [backend/src/ProductHub.API/Middleware/CorrelationIdMiddleware.cs](backend/src/ProductHub.API/Middleware/CorrelationIdMiddleware.cs)
- [backend/src/ProductHub.API/Middleware/ExceptionHandlingMiddleware.cs](backend/src/ProductHub.API/Middleware/ExceptionHandlingMiddleware.cs)
- [backend/src/ProductHub.Application/Common/Behaviors/SlowRequestBehavior.cs](backend/src/ProductHub.Application/Common/Behaviors/SlowRequestBehavior.cs)

### Frontend

#### Reuse and state consistency

- API access remains centralized in a single composable/store flow.
- Dependency options are loaded once and kept synchronized locally after create, update, delete, and reorder flows.
- Bulk movement and list reprioritization preserve the user scroll position.

Files:

- [frontend/app/composables/useApi.ts](frontend/app/composables/useApi.ts)
- [frontend/app/stores/roadmap.ts](frontend/app/stores/roadmap.ts)

#### Roadmap UX quality adjustments

- Bulk movement now supports quarter-to-backlog and backlog-to-quarter consistently.
- Backlog separator rendering is explicitly refreshed after quarter moves to avoid stale visual grouping.
- The temporary `Selecionar visíveis` and `Limpar visíveis` shortcuts were removed to keep selection behavior simpler and less error-prone.
- Demand dependencies are visible in both list and Kanban views, showing the two sides of the relation.
- The demand form now supports selecting dependencies across projects while keeping project immutable during edit.

Files:

- [frontend/app/pages/roadmap.vue](frontend/app/pages/roadmap.vue)
- [frontend/app/components/roadmap/DemandCard.vue](frontend/app/components/roadmap/DemandCard.vue)
- [frontend/app/components/roadmap/DemandFormModal.vue](frontend/app/components/roadmap/DemandFormModal.vue)
- [frontend/app/types/roadmap.ts](frontend/app/types/roadmap.ts)

### Seed / Mock Data

- Added Retaguarda mock demands and seeded cross-project dependency examples.
- The seed now demonstrates one-to-many dependency visualization by linking multiple Cross demands to the same Retaguarda demand.

File:

- [backend/src/ProductHub.Infrastructure/Persistence/Seed/RoadmapSeeder.cs](backend/src/ProductHub.Infrastructure/Persistence/Seed/RoadmapSeeder.cs)

## Tests Added

### Domain tests

- Customer normalization removes duplicates, trims whitespace, and ignores blanks.
- Updating with null customers clears the collection safely.

File:

- [backend/tests/ProductHub.Domain.Tests/Roadmap/RoadmapDemandTests.cs](backend/tests/ProductHub.Domain.Tests/Roadmap/RoadmapDemandTests.cs)

### Infrastructure tests

- Saving and reloading a demand with empty customers preserves an empty collection without failures.
- Dependency replacement now has repository coverage to ensure stale links are removed and only the latest dependency set persists.

Files:

- [backend/tests/ProductHub.Infrastructure.Tests/Configurations/RoadmapDemandConfigurationTests.cs](backend/tests/ProductHub.Infrastructure.Tests/Configurations/RoadmapDemandConfigurationTests.cs)
- [backend/tests/ProductHub.Infrastructure.Tests/Repositories/RoadmapDemandRepositoryTests.cs](backend/tests/ProductHub.Infrastructure.Tests/Repositories/RoadmapDemandRepositoryTests.cs)

### Application tests

- Updating a demand with a different `ProjectId` is now covered and must fail explicitly with validation.

File:

- [backend/tests/ProductHub.Application.Tests/Roadmap/UpdateRoadmapDemandCommandHandlerTests.cs](backend/tests/ProductHub.Application.Tests/Roadmap/UpdateRoadmapDemandCommandHandlerTests.cs)

## Validation Result

- Workspace diagnostics on modified files: no reported errors after the final adjustments.
- Terminal-based .NET discovery did not return usable output in this environment, so automated backend test execution could not be confirmed or run from here.

## Residual Risks / Follow-up

- [frontend/app/pages/roadmap.vue](frontend/app/pages/roadmap.vue) remains the primary cyclomatic complexity hotspot. Functionally consistent, but it should be split into smaller composables/components for long-term maintainability.
- There is still no dedicated frontend unit coverage for roadmap-specific flows such as bulk movement, dependency selection, and backlog separator behavior.
- Dependency validation currently prevents duplicates and self-reference, but it does not yet detect longer dependency cycles across multiple demands.
- Database initialization still uses `EnsureCreatedAsync`; once persistent SQL schema evolution becomes relevant, startup should move to migrations.

## Final Status

- Inconsistencies identified during this quality pass were corrected.
- The current implementation is aligned with the project standards for friendly user messaging with Correlation ID, slow alert logging, retry-aware persistence startup, explicit validation, component reuse, and minimum regression coverage around the recent roadmap changes.