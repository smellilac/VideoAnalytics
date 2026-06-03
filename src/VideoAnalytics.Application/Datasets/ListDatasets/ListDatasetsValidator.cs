namespace VideoAnalytics.Application.Datasets.ListDatasets;

using FluentValidation;

public sealed class ListDatasetsValidator : AbstractValidator<ListDatasetsQuery>
{
    public ListDatasetsValidator()
    {
        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100);
    }
}
