namespace VideoAnalytics.Application.Datasets.ResetDataset;

using FluentValidation;

public sealed class ResetDatasetValidator : AbstractValidator<ResetDatasetCommand>
{
    public ResetDatasetValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();
    }
}
