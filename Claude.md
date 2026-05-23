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
      Dataset.cs                      # Модель
      DatasetStatus.cs                # enum: Pending, InProgress, Ready, Failed
      DatasetArtifact.cs
      DatasetDependency.cs
      DatasetStatusTransitions.cs     # Конечный автомат — ТОЛЬКО здесь
      ReadinessResult.cs
    Common/
      IDomainEvent.cs

  VideoAnalytics.Application/
    Datasets/
      RegisterDataset/
        RegisterDatasetCommand.cs
        RegisterDatasetHandler.cs
      UpdateStatus/
        UpdateDatasetStatusCommand.cs
        UpdateDatasetStatusHandler.cs  # Валидирует переход + dependency check
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
    Interfaces/
      IDatasetRepository.cs            # PostgreSQL контракт
      IReportRepository.cs             # ClickHouse контракт
      ICacheService.cs                 # Redis контракт
      IEventPublisher.cs               # Kafka producer контракт
      IArtifactStorage.cs              # MinIO HEAD-check контракт

  VideoAnalytics.Infrastructure/
    Persistence/
      AppDbContext.cs                  # EF Core + PostgreSQL
      DatasetRepository.cs            # Реализует IDatasetRepository, Dapper для рекурсивных CTE
      ClickHouseReportRepository.cs   # Реализует IReportRepository
      Migrations/
    Kafka/
      PipelineEventConsumer.cs        # BackgroundService, at-least-once, commit после записи в БД
      DatasetReadyProducer.cs
    Cache/
      RedisCacheService.cs            # Cache-aside + tag-based invalidation по dataset_id
    Storage/
      MinioArtifactStorage.cs         # HEAD-запрос для проверки существования артефактов

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
    Filters/
      DatasetReadinessGuard.cs        # Endpoint filter: блокирует Reporting если датасет не READY
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
- **Entity Framework Core** — PostgreSQL (метаданные датасетов). Dapper — для рекурсивных CTE (readiness check)
- **ClickHouse** — аналитические запросы (Reporting API). Клиент: `ClickHouse.Client`
- **Redis** — кэш с cache-aside паттерном. TTL: 5 мин для engagement, 10 мин для трендов, до инвалидации для summary
- **Kafka** — `Confluent.Kafka`. Consumer: at-least-once, offset commit после записи в БД
- **MinIO / S3** — хранение Parquet-артефактов. HEAD-check при переходе в READY
- **Mediator** (source-generated) — command/query dispatch
- **FluentValidation** — валидация запросов
- **Polly** — circuit breaker на ClickHouse и Redis клиентах
- **Serilog** — структурированное логирование
- **Prometheus** — метрики (через `prometheus-net.AspNetCore`)
- **xUnit v3** + **Testcontainers** — тесты

## Domain Rules (никогда не нарушать)

### Конечный автомат статусов

Живёт **только** в `DatasetStatusTransitions.cs` в Domain. Нигде больше нет логики переходов.

```
PENDING → IN_PROGRESS → READY
                      → FAILED
FAILED  → PENDING  (только через /reset endpoint)
READY   → (терминальный, переходов нет)
```

Недопустимый переход → `422 Unprocessable Entity`.

### Переход в READY — обязательные проверки

Перед тем как записать `completed_at` и опубликовать `dataset.ready`:
1. Все зависимости датасета имеют статус READY (рекурсивно)
2. Все зарегистрированные артефакты существуют в S3 (HEAD-запрос через `IArtifactStorage`)

Если хотя бы одно условие не выполнено — переход отклоняется с `422`.

### Readiness Guard

**Каждый** Reporting endpoint перед запросом к ClickHouse вызывает `CheckReadinessHandler`. Если `IsReady == false` → `503 Service Unavailable` с описанием причины. Клиент никогда не получает частичные данные молча.

### Idempotency артефактов

`RegisterArtifactHandler` использует `ON CONFLICT (dataset_id, s3_key) DO NOTHING`. Race condition исключён на уровне БД, не в коде.

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

- Логику переходов статусов **вне** `DatasetStatusTransitions.cs`
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
- `error-handling` — Result pattern, ProblemDetails
- `logging` — Serilog, OpenTelemetry
- `configuration` — Options pattern, secrets management
- `dependency-injection` — Service registration patterns
- `workflow-mastery` — parallel worktrees, verification loops, subagent patterns
- `self-correction-loop` — capture corrections as permanent rules in MEMORY.md
- `wrap-up-ritual` — structured session handoff to `.claude/handoff.md`
- `context-discipline` — token budget management, MCP-first navigation
