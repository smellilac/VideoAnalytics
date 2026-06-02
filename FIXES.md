### src/VideoAnalytics.Domain/Outbox/OutboxMessage.cs
- Добавить поле `Error (string?)`, nullable
- Добавить поле `RetryCount (int)`, default 0
- Добавить метод `MarkFailed(DateTimeOffset now, string error)` — инкрементирует `RetryCount`, записывает `Error`

### src/VideoAnalytics.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs
- Добавить маппинг `Error (string?)`, nullable
- Добавить маппинг `RetryCount (int)`, default 0
- Добавить индекс по `CreatedAt`

### src/VideoAnalytics.Domain/Outbox/OutboxMessageTypes.cs
- Создать статический класс `OutboxMessageTypes` с константами `DatasetStatusChanged (string)` и `DatasetReady (string)`

### src/VideoAnalytics.Application/Datasets/UpdateStatus/UpdateDatasetStatusHandler.cs
- Заменить строковые литералы типов сообщений при создании `OutboxMessage` на константы из `OutboxMessageTypes`

### src/VideoAnalytics.Infrastructure/Kafka/OutboxPublisher.cs
- Заменить строковые литералы в `switch` по типу сообщения на константы из `OutboxMessageTypes`
- В `catch` блоке вызывать `message.MarkFailed(now, ex.Message)` вместо только логирования
- После достижения максимального `RetryCount` прекращать попытки обработки сообщения
- Добавить `case "dataset.ready"` в `DispatchAsync` — десериализация в соответствующий payload record и вызов `publisher.PublishDatasetReadyAsync`
- Заменить чтение батча `outbox_messages WHERE processed_at IS NULL` на запрос с `SELECT FOR UPDATE SKIP LOCKED` через `FromSqlRaw` или Dapper для защиты от конкурентной обработки при горизонтальном масштабировании