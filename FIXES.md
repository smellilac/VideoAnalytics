### src/VideoAnalytics.Application/Datasets/RegisterDataset/RegisterDatasetHandler.cs
- Обернуть `repository.AddAsync` в `try/catch` на `DbUpdateException`, при нарушении уникального индекса возвращать `DatasetErrors.AlreadyExists(command.Name, command.Version)`

