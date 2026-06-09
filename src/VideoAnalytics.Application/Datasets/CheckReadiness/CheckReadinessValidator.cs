namespace VideoAnalytics.Application.Datasets.CheckReadiness;

using FluentValidation;

public sealed class CheckReadinessValidator : AbstractValidator<CheckReadinessQuery>
{
    public CheckReadinessValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();
    }
}
