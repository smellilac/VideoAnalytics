# MEMORY — VideoAnalytics.Backend

Project-specific patterns and corrections learned from working sessions.

---

## ErrorOr library (v2.0.0)

- `ErrorOr 2.0.0` does **not** have `ErrorType.Forbidden` — the enum only has: `Failure`, `Unexpected`, `Validation`, `Conflict`, `NotFound`, `Unauthorized`. Remove `Forbidden` from any switch.
- `ErrorType.Validation` maps to **422 Unprocessable Entity** in this project (not 400 — see CLAUDE.md error table).
- Implicit return: `return value;` for success, `return Error.X(...);` for failure. No wrappers needed.
- `ErrorOr<Success>` for void commands — return `new Success()`.

## Layering

- Application layer **cannot** reference EF Core (`DbUpdateException`, `PostgresException`). The repository converts those to `InvalidOperationException` which the handler catches.
- `DatasetErrors.cs` lives in `Application/Datasets/Common/` (not `Application/Common/`) — namespace is `VideoAnalytics.Application.Datasets.Common`.

## Dataset.TransitionTo

- Signature is `TransitionTo(DatasetStatus newStatus, DateTimeOffset now, string? message = null)` — accepts `DateTimeOffset`, not `TimeProvider`. Get `now = timeProvider.GetUtcNow()` once in the handler and pass to both `TransitionTo` and `DatasetStatusHistory.Create`.

## Outbox

- `OutboxPublisher` is a `BackgroundService` (singleton) — always use `IServiceScopeFactory` to resolve scoped services (`AppDbContext`, `IEventPublisher`) inside it.
- Only `dataset.status.changes` goes through the outbox. `dataset.ready` is still published directly.
- `outbox_messages` table needs an EF migration — it will not be created automatically.

## Infrastructure

- `BackgroundService` requires `Microsoft.Extensions.Hosting.Abstractions` — not included transitively in the Infrastructure project (`Microsoft.NET.Sdk`). Must be added explicitly.
