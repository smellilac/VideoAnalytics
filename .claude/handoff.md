# Handoff — 2026-06-11

## Branch
`master`

## Project Status: Dataset Lifecycle API — COMPLETE. Reporting API — NOT STARTED.

---

## Completed Features (merged to master)

| PR | Feature | Notes |
|----|---------|-------|
| #1 | RegisterDataset | ErrorOr result pattern, duplicate guard via `ExistsAsync` + race-condition catch |
| #2 | UpdateDataset + RegisterArtifact | Outbox pattern for status events, idempotent artifact upsert via `ON CONFLICT DO NOTHING` |
| #3 | CheckReadiness | Recursive CTE via Dapper, dependency graph traversal |
| #4 | AddDependencies | Composite PK `(DatasetId, DependsOnDatasetId)` |
| #5 | ListDatasets | Paginated listing endpoint |
| #6 | ResetDataset | `FAILED → PENDING` transition, clears `ErrorMessage`, preserves history and artifacts |
| #7 | Health checks (partial) | PostgreSQL + Redis + Kafka probes only; MinIO and ClickHouse excluded intentionally |
| #8 + #10 | Kafka EventPublisher | `Confluent.Kafka` producer; config registration fixed in #10 |
| #9 + #11 | MinIO ArtifactStorage | HEAD-check for S3 artifact existence; client registration fixed in #11 |
| #12 | Redis CacheService | Cache-aside with tag-based invalidation via `InvalidateAsync(datasetId)` |

All 7 Dataset Lifecycle endpoints are live:
- `POST /datasets` — RegisterDataset
- `PUT /datasets/{id}/status` — UpdateDatasetStatus
- `POST /datasets/{id}/artifacts` — RegisterArtifact
- `GET /datasets/{id}/readiness` — CheckReadiness
- `POST /datasets/{id}/dependencies` — AddDependency
- `GET /datasets` — ListDatasets
- `POST /datasets/{id}/reset` — ResetDataset

---

## Open Feature Branches

All named feature branches (`feature-*`, `fix-*`) are **stale** — they were squash-merged into master. No active work branches exist. Next work starts from `master`.

---

## Remaining Work Before Reporting API is Complete

### 1. EF Core Migrations — BLOCKED (nothing exists yet)
`src/VideoAnalytics.Infrastructure/Persistence/Migrations/` contains only `.gitkeep`. No migrations have been generated or applied.

```bash
dotnet ef migrations add InitialCreate \
  --project src/VideoAnalytics.Infrastructure \
  --startup-project src/VideoAnalytics.Api
```

### 2. IReportRepository + ClickHouse Infrastructure — NOT STARTED
- `src/VideoAnalytics.Application/Interfaces/IReportRepository.cs` — missing entirely
- `src/VideoAnalytics.Infrastructure/` — no ClickHouse folder, no `ClickHouseReportRepository.cs`
- Needs: `ClickHouse.Client` package, connection settings, 5 query methods

### 3. Reporting Application Handlers — NOT STARTED
All 5 handler directories contain only `.gitkeep`:
- `Application/Reporting/GetEngagementReport/`
- `Application/Reporting/GetDailyTrends/`
- `Application/Reporting/GetPlatformComparison/`
- `Application/Reporting/GetCategoryDistribution/`
- `Application/Reporting/GetDatasetSummary/`

Each needs: `*Query.cs`, `*Handler.cs`, `*Validator.cs`

### 4. Reporting API Endpoints — NOT STARTED
`src/VideoAnalytics.Api/Endpoints/Reporting/` contains only `.gitkeep`. Needs 5 endpoint classes.

### 5. DatasetReadinessGuard Endpoint Filter — NOT STARTED
`src/VideoAnalytics.Api/Filters/` contains only `.gitkeep`. This filter blocks all Reporting endpoints when the target dataset is not READY → `503 Service Unavailable`. Must be applied to all 5 reporting endpoints.

### 6. Kafka Consumer (PipelineEventConsumer) — NOT STARTED
`src/VideoAnalytics.Infrastructure/Kafka/` has producer and outbox publisher, but no consumer. Needs `PipelineEventConsumer.cs` as `BackgroundService`, topic: `pipeline.dataset.events`, at-least-once semantics, offset commit after DB write.

### 7. Tests — NOT STARTED for Reporting and Domain
- `tests/VideoAnalytics.Tests/Reporting/` — empty (`.gitkeep`)
- `tests/VideoAnalytics.Tests/Domain/` — empty (`.gitkeep`); needs `DatasetStatusTransitionsTests.cs`

---

## Architecture Notes

- **Outbox**: `OutboxPublisher` (BackgroundService) polls every 5s, batch 20, dispatches via `IEventPublisher`. Handles `dataset.status.changes`. `dataset.ready` invalidation is called directly from `UpdateDatasetStatusHandler` after the Kafka publish — no separate consumer needed.
- **ErrorOr 2.0.0** — no `ErrorType.Forbidden`; default `_` branch in `ToHttpResult()` maps to 500.
- **Reporting sub-190ms p95 requirement** — Redis cache keys are defined in CLAUDE.md; TTLs: 5min (engagement), 10min (trends), ∞ until explicit invalidation (summary).
- **Readiness Guard** — every Reporting endpoint must pass through `CheckReadinessHandler` before hitting ClickHouse. This is not optional.
- **Tests cannot run in current environment** — Docker unavailable in WSL; Testcontainers requires PostgreSQL + Redis containers.
