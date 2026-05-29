### src/VideoAnalytics.Domain/Datasets/Dataset.cs
- Добавить обязательное поле `ErrorMessage (string?)`
- Добавить обязательное поле `Metadata (JsonDocument?)`
- Добавить обязательное поле `Version (string)`
- Добавить обязательное поле `PipelineRunId (string)`
- Удалить поле `Platform`
- Удалить поле `Category`
 - Добавить метод `TransitionTo(DatasetStatus newStatus, TimeProvider timeProvider, string? message = null)` - Валидировать переход через `DatasetStatusTransitions.IsAllowed` - При переходе в `Failed` — записывать `message` в `ErrorMessage` - При переходе `Failed → Pending` — очищать `ErrorMessage` - При переходе в `Ready` — проставлять `CompletedAt` - Обновлять `UpdatedAt` при каждом переходе

### src/VideoAnalytics.Domain/Datasets/DatasetStatusHistory.cs 
- Создать новую модель с полями: `Id (Guid)`, `DatasetId (Guid)`, `FromStatus (DatasetStatus)`, `ToStatus (DatasetStatus)`, `Message (string?)`, `OccurredAt (DateTimeOffset)`

### src/VideoAnalytics.Domain/Datasets/ReadinessResult.cs
- Добавить `ArgumentException.ThrowIfNullOrWhiteSpace(reason)` в метод `NotReady(string reason)`

### src/VideoAnalytics.Domain/Datasets/DatasetArtifact.cs
- Добавить поле `SizeBytes (long)`
- Добавить поле `RowCount (long)`
- Заменить `sealed record` с позиционными параметрами на `sealed class` со свойствами и приватным конструктором

### src/VideoAnalytics.Infrastructure/Persistence/AppDbContext.cs
- Удалить inline-конфигурацию всех сущностей из `OnModelCreating`
- Заменить тело `OnModelCreating` на `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`
- Добавить `DbSet<DatasetArtifact> DatasetArtifacts`
- Добавить `DbSet<DatasetDependency> DatasetDependencies`
- Добавить `DbSet<DatasetStatusHistory> DatasetStatusHistory`

### src/VideoAnalytics.Infrastructure/Persistence/Configurations/DatasetConfiguration.cs
- Создать класс `DatasetConfiguration : IEntityTypeConfiguration<Dataset>`
- Удалить маппинги полей `Platform` и `Category`
- Добавить маппинг `Version (string)`, `IsRequired`
- Добавить маппинг `PipelineRunId (string)`, `IsRequired`
- Добавить маппинг `ErrorMessage (string?)`, nullable
- Добавить маппинг `UpdatedAt (DateTime)`
- Добавить маппинг `CompletedAt (DateTime?)`, nullable
- Добавить маппинг `Metadata (JsonDocument?)` с `.HasColumnType("jsonb")`
- Добавить уникальный индекс по `(Name, Version)`

### src/VideoAnalytics.Infrastructure/Persistence/Configurations/DatasetStatusHistoryConfiguration.cs 
- Создать конфигурацию для новой модели - Добавить индекс по `DatasetId

### src/VideoAnalytics.Infrastructure/Persistence/Configurations/DatasetArtifactConfiguration.cs
- Создать класс `DatasetArtifactConfiguration : IEntityTypeConfiguration<DatasetArtifact>`
- Добавить уникальный индекс по `(DatasetId, S3Key)`

### src/VideoAnalytics.Infrastructure/Persistence/Configurations/DatasetDependencyConfiguration.cs
- Создать класс `DatasetDependencyConfiguration : IEntityTypeConfiguration<DatasetDependency>`

### src/VideoAnalytics.Infrastructure/Persistence/DatasetRepository.cs
- Добавить реализацию `ExistsAsync(string name, string version, CancellationToken cancellationToken)` через `AnyAsync` по полям `Name` и `Version`
- В `AddAsync` обернуть `SaveChangesAsync` в `try/catch` для `DbUpdateException`, при нарушении уникального индекса по `(Name, Version)` пробрасывать доменную ошибку

### src/VideoAnalytics.Application/Common/ValidationBehavior.cs
- Заменить синхронный `v.Validate(context)` на `await v.ValidateAsync(context, cancellationToken)` с соответствующим `await Task.WhenAll(...)` или последовательным `await` в цикле
- Удалить `.Where(f => f is not null)`

### src/VideoAnalytics.Application/Datasets/RegisterDataset/RegisterDatasetCommand.cs
- Удалить параметр `Platform` из `RegisterDatasetCommand`
- Удалить параметр `Category` из `RegisterDatasetCommand`
- Добавить обязательный параметр `Version (string)` в `RegisterDatasetCommand`
- Добавить обязательный параметр `PipelineRunId (string)` в `RegisterDatasetCommand`
- Добавить опциональный параметр `Metadata (JsonDocument?)` в `RegisterDatasetCommand`
- Добавить `Name (string)` в `RegisterDatasetResponse`
- Добавить `Version (string)` в `RegisterDatasetResponse`

### src/VideoAnalytics.Application/Datasets/RegisterDataset/RegisterDatasetHandler.cs 
- Заменить вызов `Dataset.Create(command.Name, command.Platform, command.Category, timeProvider)` на `Dataset.Create(command.Name, command.Version, command.PipelineRunId, command.Metadata, timeProvider)` 
- Убрать `dataset.Platform` из вызова `logger.LogInformation`, заменить на `dataset.Version` 
- Добавить `dataset.Name` и `dataset.Version` в возвращаемый `RegisterDatasetResponse`
- Перед вызовом `Dataset.Create()` добавить вызов `repository.ExistsAsync(command.Name, command.Version, cancellationToken)` 
- Если `ExistsAsync` возвращает `true` — возвращать 409 (бросать соответствующее исключение или результат конфликта)

### src/VideoAnalytics.Application/Interfaces/IDatasetRepository.cs
- Добавить метод `Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken)`

### src/VideoAnalytics.Application/Datasets/RegisterDataset/RegisterDatasetValidator.cs
- Удалить правило валидации для `Platform`
- Удалить правило валидации для `Category`
- Добавить правило для `Version`: `NotEmpty` и `MaximumLength(200)`
- Добавить правило для `PipelineRunId`: `NotEmpty`

### src/VideoAnalytics.Api/Endpoints/DatasetLifecycle/RegisterDatasetEndpoint.cs
- Удалить параметры `Platform` и `Category` из `RegisterDatasetRequest`
- Добавить обязательный параметр `Version (string)` в `RegisterDatasetRequest`
- Добавить обязательный параметр `PipelineRunId (string)` в `RegisterDatasetRequest`
- Добавить опциональный параметр `Metadata (JsonDocument?)` в `RegisterDatasetRequest`
- Заменить `new RegisterDatasetCommand(request.Name, request.Platform, request.Category)` на `new RegisterDatasetCommand(request.Name, request.Version, request.PipelineRunId, request.Metadata)`
- Добавить `.Produces(StatusCodes.Status409Conflict)` в конфигурацию эндпоинта

### src/VideoAnalytics.Api/Endpoints/EndpointExtensions.cs
- Удалить резолвинг через `Assembly.GetExecutingAssembly().GetTypes()` и `Activator.CreateInstance`
- Заменить на получение экземпляров через `app.ServiceProvider.GetServices<IEndpointGroup>()`

### src/VideoAnalytics.Api/Program.cs
- Удалить inline-регистрацию сервисов из `Program.cs`, заменить на вызовы `builder.Services.AddApplication()` и `builder.Services.AddInfrastructure(builder.Configuration)`
- Удалить дублирующийся `WriteTo.Console()` из bootstrap logger — оставить настройки логирования только через `ReadFrom.Configuration(builder.Configuration)`
- Добавить регистрацию всех реализаций `IEndpointGroup` в DI-контейнере через рефлексию по сборке

### src/VideoAnalytics.Application/DependencyInjection.cs
- Создать extension-метод `AddApplication(this IServiceCollection services)`
- Перенести `AddMediator()` с регистрацией `ValidationBehavior`
- Перенести `AddValidatorsFromAssemblyContaining<RegisterDatasetValidator>()`
- Перенести `AddSingleton(TimeProvider.System)`

### src/VideoAnalytics.Infrastructure/DependencyInjection.cs
- Создать extension-метод `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)`
- Перенести `AddDbContext<AppDbContext>`
- Перенести `AddScoped<IDatasetRepository, DatasetRepository>`