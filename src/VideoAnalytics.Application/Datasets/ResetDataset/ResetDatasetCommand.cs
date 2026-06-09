namespace VideoAnalytics.Application.Datasets.ResetDataset;

using ErrorOr;
using Mediator;

public sealed record ResetDatasetCommand(Guid DatasetId) : ICommand<ErrorOr<Success>>;
