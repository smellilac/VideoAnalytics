# VideoAnalytics.Backend — Web API

## Project Context

Backend-сервис на .NET — интеграционный слой платформы, обрабатывающей до 200 000 коротких видеороликов в сутки. Сервис **не обрабатывает видео сам** — это делают Spark и Airflow. Сервис делает три вещи:

1. **Dataset Lifecycle API** (внутренний) — управляет жизненным циклом датасетов: статусы, артефакты, граф зависимостей, readiness check
2. **Reporting API** (внешний) — отвечает на аналитические запросы клиентских дашбордов с требованием **sub-190ms p95 latency**
3. **Kafka-интеграция** — принимает события от Airflow/Spark, публикует события о готовности датасетов

Это **один деплоймент**: два контроллера делят процесс, конфигурацию, PostgreSQL, Kafka, Redis.

## Architecture Decision: Clean Architecture

**Зафиксировано.** `/scaffold` и все генерации кода используют эту структуру. Не менять без явного решения команды.

**Почему CA, а не VSA или DDD:**
- VSA отклонена: сквозная логика (конечный автомат, readiness guard, dependency check) не укладывается в «один файл на фичу»
- DDD отклонён: домен не достаточно богат — нет bounded context'ов, нет сложных агрегатов; добавит overhead без выгоды
- CA: средняя доменная сложность с реальными инвариантами, 5+ внешних интеграций требуют изоляции, проект живёт 2–3+ лет

### Структура проекта

```
src/
  VideoAnalytics.Domain/
    Datasets/
      Dataset.cs                      # Агрегат: поля + TransitionTo()
      DatasetStatus.cs                # enum: Pending, InProgress, Ready, Failed
      DatasetStatusHistory.cs         # Модель истории переходов
      DatasetArtifact.cs              # sealed class (не record), SizeBytes + RowCount
      DatasetDependency.cs
      DatasetStatusTransitions.cs     # Таблица допустимых переходов — ТОЛЬКО здесь
      ReadinessResult.cs
	  DatasetReadinessIssue.cs
    Common/
      IDomainEvent.cs

  VideoAnalytics.Application/
    Datasets/
      RegisterDataset/
        RegisterDatasetCommand.cs
        RegisterDatasetHandler.cs
      UpdateStatus/
        UpdateDatasetStatusCommand.cs
        UpdateDatasetStatusHandler.cs  # Вызывает Dataset.TransitionTo() + dependency check
      RegisterArtifact/
        RegisterArtifactCommand.cs
        RegisterArtifactHandler.cs     # Idempotent логика
      CheckReadiness/
        CheckReadinessQuery.cs
        CheckReadinessHandler.cs       # Рекурсивный CTE через IDatasetRepository
      ResetDataset/
        ResetDatasetCommand.cs
        ResetDatasetHandler.cs
    Reporting/
      GetEngagementReport/
        GetEngagementReportQuery.cs
        GetEngagementReportHandler.cs  # Readiness guard внутри, затем ClickHouse
      GetDailyTrends/
        GetDailyTrendsQuery.cs
        GetDailyTrendsHandler.cs
      GetPlatformComparison/
        GetPlatformComparisonQuery.cs
        GetPlatformComparisonHandler.cs
      GetCategoryDistribution/
        GetCategoryDistributionQuery.cs
        GetCategoryDistributionHandler.cs
      GetDatasetSummary/
        GetDatasetSummaryQuery.cs
        GetDatasetSummaryHandler.cs
    Common/
      ValidationBehavior.cs           # Pipeline: async ValidateAsync + Task.WhenAll
      DatasetErrors.cs                # Static factory: Error.NotFound(), Error.Conflict(), Error.Validation()
    Interfaces/
      IDatasetRepository.cs            # PostgreSQL контракт
      IReportRepository.cs             # ClickHouse контракт
      ICacheService.cs                 # Redis контракт
      IEventPublisher.cs               # Kafka producer контракт
      IArtifactStorage.cs              # MinIO HEAD-check контракт
    DependencyInjection.cs             # AddApplication(): Mediator, validators, TimeProvider

  VideoAnalytics.Infrastructure/
    Persistence/
      AppDbContext.cs                  # EF Core + PostgreSQL; конфигурация через ApplyConfigurationsFromAssembly
      DatasetRepository.cs            # Реализует IDatasetRepository, Dapper для рекурсивных CTE
      ClickHouseReportRepository.cs   # Реализует IReportRepository
      Configurations/
        DatasetConfiguration.cs
        DatasetArtifactConfiguration.cs
        DatasetDependencyConfiguration.cs
        DatasetStatusHistoryConfiguration.cs
      Migrations/
    Kafka/
      PipelineEventConsumer.cs        # BackgroundService, at-least-once, commit после записи в БД
      DatasetReadyProducer.cs
    Cache/
      RedisCacheService.cs            # Cache-aside + tag-based invalidation по dataset_id
    Storage/
      MinioArtifactStorage.cs         # HEAD-запрос для проверки существования артефактов
    DependencyInjection.cs            # AddInfrastructure(): DbContext, repositories

  VideoAnalytics.Api/
    Endpoints/
      DatasetLifecycle/
        RegisterDatasetEndpoint.cs
        UpdateDatasetStatusEndpoint.cs
        RegisterArtifactEndpoint.cs
        GetReadinessEndpoint.cs
        AddDependencyEndpoint.cs
        ListDatasetsEndpoint.cs
        ResetDatasetEndpoint.cs
      Reporting/
        EngagementReportEndpoint.cs
        DailyTrendsEndpoint.cs
        PlatformComparisonEndpoint.cs
        CategoryDistributionEndpoint.cs
        DatasetSummaryEndpoint.cs
    Infrastructure/
      ValidationExceptionHandler.cs   # FluentValidation → 400 (только uncaught exceptions)
      ErrorOrExtensions.cs            # Error.ToHttpResult() — маппинг ErrorType → ProblemDetails
    Program.cs

tests/
  VideoAnalytics.Tests/
    Datasets/
      RegisterDatasetTests.cs
      UpdateDatasetStatusTests.cs
      RegisterArtifactTests.cs        # Idempotency сценарии
      CheckReadinessTests.cs          # Граф зависимостей, циклы
    Reporting/
      EngagementReportTests.cs
    Domain/
      DatasetStatusTransitionsTests.cs
    Fixtures/
      ApiFixture.cs                   # WebApplicationFactory + Testcontainers (PostgreSQL + Redis)
```

## Tech Stack

- **.NET 10** / C# 14
- **ASP.NET Core Minimal APIs** — `IEndpointGroup` per feature с `app.MapEndpoints()` auto-discovery
- **Entity Framework Core** — PostgreSQL (метаданные датасетов). Конфигурация через `IEntityTypeConfiguration<T>` в `Persistence/Configurations/`. Dapper — для рекурсивных CTE (readiness check)
- **ClickHouse** — аналитические запросы (Reporting API). Клиент: `ClickHouse.Client`
- **Redis** — кэш с cache-aside паттерном. TTL: 5 мин для engagement, 10 мин для трендов, до инвалидации для summary
- **Kafka** — `Confluent.Kafka`. Consumer: at-least-once, offset commit после записи в БД
- **MinIO / S3** — хранение Parquet-артефактов. HEAD-check при переходе в READY
- **Mediator** (source-generated) — command/query dispatch. `Mediator.SourceGenerator` живёт в **Application** проекте
- **FluentValidation** — валидация запросов; `ValidateAsync` (не `Validate`) в `ValidationBehavior`
- **ErrorOr** — Result pattern implementation. Handlers возвращают `ErrorOr<T>` вместо `throw` для бизнес-ошибок. См. skill `error-handling`
- **Polly** — circuit breaker на ClickHouse и Redis клиентах
- **Serilog** — структурированное логирование
- **Prometheus** — метрики (через `prometheus-net.AspNetCore`)
- **xUnit v3** + **Testcontainers** — тесты

## Domain Entities

### Dataset

```csharp
public sealed class Dataset
{
    public Guid Id { get; }
    public string Name { get; }            // уникально в паре с Version
    public string Version { get; }         // уникально в паре с Name
    public string PipelineRunId { get; }
    public DatasetStatus Status { get; }
    public string? ErrorMessage { get; }   // заполняется при переходе в Failed, очищается при reset
    public JsonDocument? Metadata { get; } // jsonb в PostgreSQL
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public DateTimeOffset? CompletedAt { get; } // проставляется при переходе в Ready

    public static Dataset Create(string name, string version, string pipelineRunId,
                                 JsonDocument? metadata, TimeProvider timeProvider);
    public void TransitionTo(DatasetStatus newStatus, TimeProvider timeProvider, string? message = null);
}
```

**Поля `Platform` и `Category` удалены.** Не добавлять их обратно.

### DatasetArtifact

```csharp
public sealed class DatasetArtifact   // class, не record
{
    public Guid Id { get; }
    public Guid DatasetId { get; }
    public string S3Key { get; }
    public string ArtifactType { get; }
    public long SizeBytes { get; }
    public long RowCount { get; }
    public DateTimeOffset RegisteredAt { get; }
}
```

Уникальный индекс: `(DatasetId, S3Key)`.

### DatasetStatusHistory

```csharp
public sealed class DatasetStatusHistory
{
    public Guid Id { get; }
    public Guid DatasetId { get; }
    public DatasetStatus FromStatus { get; }
    public DatasetStatus ToStatus { get; }
    public string? Message { get; }
    public DateTimeOffset OccurredAt { get; }
}
```

Таблица: `dataset_status_history`. Индекс по `DatasetId`.

### DatasetDependency

```csharp
public sealed record DatasetDependency(Guid DatasetId, Guid DependsOnDatasetId);
```

Составной PK: `(DatasetId, DependsOnDatasetId)`.

## ClickHouse Schema

### video_engagement_metrics

```sql
CREATE TABLE video_engagement_metrics
(
    dataset_id      UUID,                    -- lineage: Dataset.Id (PostgreSQL) прогона который записал строку.
                                              -- Не выбирается ни одним reporting-запросом — только для ops/debug.
    video_id        String,
    platform        LowCardinality(String),  -- 'tiktok', 'instagram', etc.
    recorded_at     DateTime,
    views           UInt64,
    likes           UInt64,
    comments        UInt64,
    shares          UInt64,
    engagement_rate Float64,
    category        LowCardinality(String),
    tags            Array(String)
)
ENGINE = MergeTree()
PARTITION BY toYYYYMM(recorded_at)
ORDER BY (platform, recorded_at, video_id);
```

`platform` и `category` здесь — атрибуты конкретного видео в ClickHouse, не связаны с полями `Platform`/`Category`, удалёнными из `Dataset` (PostgreSQL, см. Anti-patterns). Совпадение имён случайно — не реагировать как на нарушение anti-pattern.



## Application Interfaces

### IDatasetRepository

```csharp
public interface IDatasetRepository
{
    Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken);
    Task AddAsync(Dataset dataset, CancellationToken cancellationToken);
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(Dataset dataset, CancellationToken cancellationToken);
    Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken);
	Task<IReadOnlyList<DatasetReadinessIssue>> CheckRangeReadinessAsync(string relevantName, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
}
```

`ExistsAsync` используется в `RegisterDatasetHandler` перед созданием — при дублировании `(Name, Version)` handler возвращает `Error.Conflict("Dataset.AlreadyExists", ...)` который маппится в `409 Conflict`. `AddAsync` дополнительно оборачивает `SaveChangesAsync` в `catch (DbUpdateException)` при `PostgresException { SqlState: "23505" }` как защитный слой race conditions.

## DI Registration Pattern

```csharp
// Program.cs
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Регистрация IEndpointGroup через рефлексию — DI, не Activator.CreateInstance
var endpointTypes = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.IsAssignableTo(typeof(IEndpointGroup)));
foreach (var type in endpointTypes)
    builder.Services.AddSingleton(typeof(IEndpointGroup), type);
```

`EndpointExtensions.MapEndpoints()` резолвит группы через `app.ServiceProvider.GetServices<IEndpointGroup>()`.
Новый `IEndpointGroup` — добавить класс в `Endpoints/`, DI-регистрация произойдёт автоматически.

## Domain Rules (никогда не нарушать)

### Конечный автомат статусов

Таблица допустимых переходов живёт **только** в `DatasetStatusTransitions.cs`. Выполнение перехода — **только** через `Dataset.TransitionTo()`. Нигде больше нет ни правил, ни вызовов прямой записи в `Status`.

```
PENDING → IN_PROGRESS → READY
                      → FAILED
FAILED  → PENDING  (только через /reset endpoint)
READY   → (терминальный, переходов нет)
```

Недопустимый переход → `Dataset.TransitionTo()` бросает `InvalidOperationException` (domain-уровень). Handler в Application слое перехватывает его и возвращает `Error.Validation("Dataset.InvalidTransition", ...)` → `422 Unprocessable Entity`. Исключение не пробрасывается наружу из Application.

### Переход в READY — обязательные проверки

Перед тем как записать `CompletedAt` и опубликовать `dataset.ready`:
1. Все зависимости датасета имеют статус READY (рекурсивно)
2. Все зарегистрированные артефакты существуют в S3 (HEAD-запрос через `IArtifactStorage`)

Если хотя бы одно условие не выполнено — переход отклоняется с `422`.

### Readiness guard

Reporting API гарантирует клиенту точное соответствие запрошенному периоду: для запроса `(platform, date_from, date_to)` каждый календарный день в диапазоне должен иметь соответствующий `Dataset` в статусе `Ready`. Если хотя бы один день не `Ready` (любой статус кроме `Ready`, включая полное отсутствие записи) — guard возвращает `503` с явным указанием каждого проблемного дня. Сервис никогда не отдаёт частичные данные молча: урезанный временной ряд из-за "данные сейчас обновляются" и урезанный ряд из-за "данных за этот день и не было" — для клиента визуально неотличимы, но требуют разной реакции и не должны путаться.

Предпосылка: "один день — один `Dataset`". `Version` для датасетов, читаемых Reporting API, — дата в ISO-формате (`'2024-01-14'`), что допускает `BETWEEN` как сравнение строк.

Resolve + bulk-check выполняется одним set-based SQL-запросом, не циклом по дням:

```sql
SELECT version, status, error_message
FROM datasets
WHERE name = @relevantName
  AND version BETWEEN @date_from AND @date_to
  AND status != 'Ready'
```

Пустой результат → запрос пропускается в ClickHouse. Непустой → `503`, `issues` формируется из вернувшихся строк (одна строка = один проблемный день).

`@relevantName` — параметр, своё значение для каждого "содержательного" reporting-эндпоинта:

| Endpoint | relevantName |
| --- | --- |
| GetEngagementReport | engagement_metrics |
| GetDailyTrends | hashtag_trends |
| GetPlatformComparison | TBD — сверить с реальным графом зависимостей |
| GetCategoryDistribution | TBD — сверить с реальным графом зависимостей |
| GetDatasetSummary | guard в этой форме не нужен — использует существующий CheckReadinessQuery(datasetId) |

Формат `503`-ответа:

```json
{
  "status": 503,
  "title": "Data not ready for requested period",
  "issues": [
    { "date": "2024-01-14", "reason": "Dataset is Failed: <error_message>" },
    { "date": "2024-01-15", "reason": "Dataset is InProgress" }
  ]
}
```

`WHERE status != 'Ready'` корректен сам по себе только при выполнении инварианта: если upstream-датасет ушёл из `Ready` (ретроактивный `/reset`), все downstream-датасеты немедленно перестают быть `Ready` тоже — см. "DatasetReadinessGuard — каскадный reset" ниже. До реализации каскада guard может пропустить случай "downstream формально `Ready`, но один из его upstream сейчас пересчитывается".

### Idempotency артефактов

`RegisterArtifactHandler` использует `ON CONFLICT (dataset_id, s3_key) DO NOTHING`. Race condition исключён на уровне БД, не в коде.

### Переход Failed → Pending (reset)
- `ErrorMessage` очищается при сбросе (обрабатывается внутри `Dataset.TransitionTo()`)
- Артефакты не удаляются — Spark детерминирован и перезапишет те же S3-ключи при повторном прогоне
- История переходов не теряется — пишется в `dataset_status_history`

### Reset — каскадный эффект на downstream-граф

`/reset` датасета каскадно переводит весь downstream-граф (транзитивное замыкание, не только прямые зависимости) тоже в `Pending` — не только сам датасет. Без этого `WHERE status != 'Ready'` в readiness guard может пропустить случай, когда формально `Ready` датасет ссылается на данные, чей upstream сейчас пересчитывается после ретроактивного `/reset`.

Это применимо и когда сбрасываемый датасет сам был `Ready` — то есть для downstream-графа `/reset` выполняет переход `Ready → Pending`, которого нет в таблице FSM выше. Механизм для этого перехода пока не выбран:
- либо добавить `Ready → Pending` в `_allowed` напрямую — меняет общую FSM-семантику для всех путей, включая обычный `UpdateDatasetStatusCommand` от Kafka-consumer;
- либо отдельный привилегированный `Dataset.ForceResetFromReady()`, минующий `DatasetStatusTransitions.IsAllowed` — `/reset` явная ops-операция со своей семантикой, не должна расширять граф переходов которым пользуется Kafka-поток.

Сохранение каскада — одной транзакцией: список `(dataset, history, outboxMessage)` для всех затронутых датасетов. Частичный каскад (часть downstream сброшена, часть осталась `Ready`) хуже отсутствия каскада вовсе.

Каждый каскадный переход публикует `dataset.status.changes` как обычный переход — если Airflow слушает этот топик, каскад одновременно триггерит пересчёт downstream Spark-джобов, не только обновляет статус для guard'а.

### dataset_status_history
Каждый переход статуса обязательно пишется в таблицу `dataset_status_history` с полями
`from_status`, `to_status`, `occurred_at`, `message`. Это единственное место хранения
истории переходов и причин падений. Очистка этой таблицы запрещена.

### Уникальность датасета

Пара `(Name, Version)` уникальна на уровне БД (уникальный индекс в `DatasetConfiguration`).
`RegisterDatasetHandler` проверяет через `ExistsAsync` перед созданием и возвращает `Error.Conflict("Dataset.AlreadyExists", ...)`. Endpoint маппит результат через `ErrorOrExtensions.ToHttpResult()` → `409 Conflict`.

## Error Handling

Бизнес-ошибки — **ErrorOr library** (https://github.com/amantinband/error-or). 
Handlers возвращают `ErrorOr<T>` или `ErrorOr<Success>`. 
Исключения — только для инфраструктурных сбоев (БД недоступна, сеть упала и т.д.).

| Ситуация | Подход | HTTP |
|---|---|---|
| Дубликат (Name, Version) | `Error.Conflict("Dataset.AlreadyExists", ...)` | 409 |
| Датасет не найден | `Error.NotFound("Dataset.NotFound", ...)` | 404 |
| Невалидный переход статуса | `Error.Validation("Dataset.InvalidTransition", ...)` | 422 |
| Зависимости не готовы | `Error.Validation("Dataset.DependenciesNotReady", ...)` | 422 |
| Артефакт отсутствует в S3 | `Error.Validation("Dataset.ArtifactMissing", ...)` | 422 |
| Валидация запроса | FluentValidation → `ValidationFilter` → `ValidationProblem` | 400 |
| Неожиданная ошибка / БД недоступна | Exception → global handler → `ProblemDetails` | 500 |
| Данные за период не готовы (readiness guard) | `DatasetErrors.DataNotReady(issues)` → `Error.Custom` | 503 |

Domain-specific errors сгруппированы в `Application/Common/DatasetErrors.cs` 
(static factory methods).

Endpoints маппят результат через `result.Match()` или `result.MatchFirst()`, 
ошибки в HTTP — через `ErrorOrExtensions.ToHttpResult()` (см. `Api/Infrastructure/`).

Детали реализации: см. skill `error-handling` в `.claude/skills/error-handling/SKILL.md`.

## Kafka Topics

| Топик | Направление | Назначение |
|---|---|---|
| `pipeline.dataset.events` | Входящий | События от Spark/Airflow о прогрессе |
| `dataset.status.changes` | Исходящий | Любое изменение статуса датасета |
| `dataset.ready` | Исходящий | Датасет перешёл в READY (триггер инвалидации кэша) |

Kafka consumer (`PipelineEventConsumer`) — `BackgroundService`. Commit offset **после** успешной записи в БД. Обработчик идемпотентен — одно и то же событие дважды не ломает состояние.

## Redis Cache Keys

| Ключ | TTL | Инвалидация |
|---|---|---|
| `report:engagement:{platform}:{date_range}:{limit}` | 5 мин | По TTL |
| `report:trends:daily:{platform}:{category}:{range}` | 10 мин | По TTL |
| `report:summary:{dataset_id}` | ∞ | Явная инвалидация после перехода датасета в READY |

Инвалидация: вызывается напрямую через `ICacheService.InvalidateAsync(datasetId)` внутри `UpdateDatasetStatusHandler` сразу после публикации события `dataset.ready` в Kafka. Отдельный consumer для инвалидации не нужен — сервис сам публикует это событие.

## Prometheus Metrics

| Метрика | Тип | Labels |
|---|---|---|
| `dataset_status_transitions_total` | Counter | `from_status`, `to_status` |
| `reporting_query_duration_seconds` | Histogram | `endpoint` |
| `redis_cache_hits_total` | Counter | `endpoint` |
| `redis_cache_misses_total` | Counter | `endpoint` |
| `kafka_consumer_lag` | Gauge | `topic` |
| `artifact_idempotency_conflicts_total` | Counter | — |

## Coding Standards

- **C# 14** — primary constructors, collection expressions, `field` keyword, records, pattern matching
- **File-scoped namespaces** — всегда
- **`var`** — для очевидных типов; явный тип когда неочевидно из контекста
- **Naming** — PascalCase для public, `_camelCase` для private fields, суффикс `Async` для async методов
- **No regions** — никогда
- **Комментарии** — только «почему», никогда «что»
- **`TimeProvider`** — инжектировать вместо `DateTime.Now`/`DateTime.UtcNow`

## Anti-patterns (никогда не генерировать)

- Логику переходов статусов **вне** `DatasetStatusTransitions.cs` (правила) и `Dataset.TransitionTo()` (вызов)
- Прямую запись в `Dataset.Status` в обход `TransitionTo()`
- Поля `Platform` или `Category` в `Dataset` — они удалены из модели навсегда
- Запрос к ClickHouse **без** предварительного readiness check
- Commit Kafka offset **до** записи в БД
- `new HttpClient()` — использовать `IHttpClientFactory`
- `async void` — всегда `Task`
- `.Result` или `.Wait()` — await
- `Results.Ok()` — использовать `TypedResults.Ok()`
- Возврат domain entities из endpoints — всегда маппить в response DTO
- Repository abstraction поверх EF Core — использовать DbContext напрямую (кроме `IDatasetRepository` и `IReportRepository` — они изолируют PostgreSQL и ClickHouse)
- In-memory database в тестах — Testcontainers
- `catch (Exception e)` — ловить конкретные типы, глобальный handler — для остального
- String interpolation в логах — structured logging templates
- Синхронный `v.Validate()` в `ValidationBehavior` — только `await v.ValidateAsync()`
- `Activator.CreateInstance` для `IEndpointGroup` — регистрировать через DI, резолвить через `GetServices<IEndpointGroup>()`
- Inline-регистрацию сервисов в `Program.cs` — использовать `AddApplication()` и `AddInfrastructure()`
- `throw ConflictException` из Application слоя — возвращать `Error.Conflict()` через `ErrorOr<T>`
- `throw InvalidOperationException` наружу из handlers — перехватывать в handler и возвращать `Error.Validation()` через `ErrorOr<T>`
- Кастомный `Result<T>` тип — использовать `ErrorOr<T>` из библиотеки ErrorOr
- `Result.Failure(...)` / `Result.Success(...)` — устаревший API кастомного `Result<T>`. Использовать неявное преобразование `ErrorOr<T>`: `return value;` для успеха, `return Error.X(...);` для ошибки
- Response-классы reporting-хэндлеров не содержат `IsDataReady`/discriminated-union для readiness — readiness-503 всегда идёт через `Error.Custom`/`ErrorOr.Errors`-ветку, не как часть успешного значения

## Commands

```bash
# Build
dotnet build

# Run (development)
dotnet run --project src/VideoAnalytics.Api

# Run tests
dotnet test

# Add EF migration
dotnet ef migrations add [Name] \
  --project src/VideoAnalytics.Infrastructure \
  --startup-project src/VideoAnalytics.Api

# Apply migrations
dotnet ef database update \
  --project src/VideoAnalytics.Infrastructure \
  --startup-project src/VideoAnalytics.Api

# Format check
dotnet format --verify-no-changes
```

## Workflow

- **Plan first** — для любой задачи в 3+ шага входить в режим планирования, итерировать план до готовности перед кодом
- **Verify before done** — после изменений: `dotnet build`, `dotnet test`, `get_diagnostics` через MCP
- **Stop and re-plan** — если реализация пошла не туда, СТОП и пересмотр плана. Не продавливать сломанный подход
- **Fix bugs autonomously** — при баг-репорте: логи → ошибки → тесты → исправление без ручного сопровождения
- **Learn from corrections** — после каждой правки зафиксировать паттерн в MEMORY.md

## MCP Tools

```bash
# Установка один раз глобально
dotnet tool install -g CWM.RoslynNavigator
claude mcp add --scope user cwm-roslyn-navigator -- cwm-roslyn-navigator --solution /mnt/c/Users/User/Projects/VideoAnalytics
```

- **Перед изменением типа** — `find_symbol` для локации, `get_public_api` для понимания поверхности
- **Перед добавлением ссылки** — `find_references` для понимания существующего использования
- **Понять архитектуру** — `get_project_graph` для графа зависимостей проектов
- **Найти реализации** — `find_implementations` вместо grep для интерфейсов
- **Проверить ошибки** — `get_diagnostics` после изменений

## Skills

- `clean-architecture` — структура слоёв, dependency inversion
- `minimal-api` — endpoint routing, TypedResults, OpenAPI metadata
- `ef-core` — DbContext patterns, query optimization, migrations
- `testing` — xUnit v3, WebApplicationFactory, Testcontainers
- `error-handling` — ErrorOr library, ProblemDetails (local override in `.claude/skills/error-handling/SKILL.md`)
- `logging` — Serilog, OpenTelemetry
- `configuration` — Options pattern, secrets management
- `dependency-injection` — Service registration patterns
- `workflow-mastery` — parallel worktrees, verification loops, subagent patterns
- `self-correction-loop` — capture corrections as permanent rules in MEMORY.md
- `wrap-up-ritual` — structured session handoff to `.claude/handoff.md`
- `context-discipline` — token budget management, MCP-first navigation
