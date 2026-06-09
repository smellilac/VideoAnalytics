namespace VideoAnalytics.Application.Datasets.ListDatasets;

using ErrorOr;
using Mediator;
using VideoAnalytics.Application.Interfaces;

public sealed class ListDatasetsHandler(IDatasetRepository repository)
    : IQueryHandler<ListDatasetsQuery, ErrorOr<ListDatasetsResponse>>
{
    public async ValueTask<ErrorOr<ListDatasetsResponse>> Handle(
        ListDatasetsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, total) = await repository.ListAsync(query.Status, query.Skip, query.Take, cancellationToken);

        var summaries = items
            .Select(d => new DatasetSummary(
                d.Id,
                d.Name,
                d.Version,
                d.PipelineRunId,
                d.Status.ToString(),
                d.CreatedAt,
                d.UpdatedAt,
                d.CompletedAt))
            .ToList();

        return new ListDatasetsResponse(summaries, total);
    }
}
