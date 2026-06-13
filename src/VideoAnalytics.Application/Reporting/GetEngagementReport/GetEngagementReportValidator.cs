namespace VideoAnalytics.Application.Reporting.GetEngagementReport;

using FluentValidation;

public sealed class GetEngagementReportValidator : AbstractValidator<GetEngagementReportQuery>
{
    public GetEngagementReportValidator()
    {
        RuleFor(x => x.Platform)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DateFrom)
            .NotEmpty();

        RuleFor(x => x.DateTo)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .WithMessage("DateTo must be on or after DateFrom.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 500);
        
        RuleFor(x => x)
            .Must(x => x.DateTo.DayNumber - x.DateFrom.DayNumber <= 31)
            .WithMessage("Date range must not exceed 31 days.")
            .OverridePropertyName("DateRange");
    }
}