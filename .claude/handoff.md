# Handoff — 2026-06-02

## Branch
`feature-RegisterDataset`

## Completed This Session

### Migration: custom Result<T> → ErrorOr library
- Added `ErrorOr 2.0.0` to `Directory.Packages.props` + `Application.csproj`
- Deleted `Application/Common/Result.cs` and `Application/Common/Errors.cs`
- Created `Application/Datasets/Common/DatasetErrors.cs` — static factory methods using `Error.NotFound/Conflict/Validation`
- Updated `RegisterDatasetCommand` and `RegisterDatasetHandler` — now return `ErrorOr<RegisterDatasetResponse>`
- Updated `UpdateDatasetStatusCommand` and `UpdateDatasetStatusHandler` — now return `ErrorOr<Success>`
- Rewrote `Api/Infrastructure/ResultExtensions.cs` — `ErrorType` switch, `ErrorType.Validation` → 422
- Updated both endpoints to use `result.MatchFirst<IResult>(...)` instead of `result.IsSuccess`
- Note: `ErrorOr 2.0.0` does NOT have `ErrorType.Forbidden` — removed from switch

### Bug fixes (from FIXES.md)
- **RegisterDatasetHandler**: Added `try/catch (InvalidOperationException)` around `AddAsync` — returns `DatasetErrors.AlreadyExists` on race-condition unique constraint violation
- **Dataset.TransitionTo**: Changed signature from `TimeProvider` to `DateTimeOffset now` — single consistent timestamp for `UpdatedAt` and `CompletedAt`
- **UpdateDatasetStatusHandler**:
  - Single `now = timeProvider.GetUtcNow()` passed to both `TransitionTo` and `DatasetStatusHistory.Create`
  - Parallel `Task.WhenAll` for artifact existence checks, collects **all** missing keys
  - Removed direct `PublishStatusChangedAsync` call — replaced by outbox
- **DatasetErrors.ArtifactMissing**: Changed to `IReadOnlyList<string>`, description lists all missing keys
- **UpdateDatasetStatusEndpoint**: Added `.Produces(StatusCodes.Status409Conflict)`

### Transactional Outbox
- New `Domain/Outbox/OutboxMessage.cs` entity with `MarkProcessed(DateTimeOffset)`
- New `Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs` — `outbox_messages` table, partial index on `processed_at IS NULL`
- `AppDbContext` gains `DbSet<OutboxMessage>`
- `IDatasetRepository.SaveTransitionAsync` now accepts `OutboxMessage` — written atomically with dataset + history
- New `Infrastructure/Kafka/OutboxPublisher.cs` — `BackgroundService`, polls every 5s, batch 20, dispatches via `IEventPublisher`, marks processed after publish
- Added `Microsoft.Extensions.Hosting.Abstractions 10.0.0` to Infrastructure
- Registered `AddHostedService<OutboxPublisher>()` in DI

## Pending

- [ ] **EF migration** needed: `dotnet ef migrations add AddOutboxMessages --project src/VideoAnalytics.Infrastructure --startup-project src/VideoAnalytics.Api`
- [ ] `CheckReadinessAsync` in `DatasetRepository` — still throws `NotImplementedException` (Dapper recursive CTE, separate feature)
- [ ] Remaining features from CLAUDE.md: `RegisterArtifact`, `CheckReadiness`, `ResetDataset`, all Reporting endpoints
- [ ] Kafka wiring: replace `NullEventPublisher` with real `Confluent.Kafka` producer
- [ ] Tests can't run (Docker not available in current WSL environment) — tests require Testcontainers + PostgreSQL + Redis

## Architecture Notes
- `ErrorOr 2.0.0` has no `ErrorType.Forbidden` — default `_` branch covers it (500)
- Outbox currently only handles `dataset.status.changes`; `dataset.ready` is still published directly (by design per FIXES.md scope)
- `OutboxPublisher` uses scoped services via `IServiceScopeFactory` since it runs as singleton background service
