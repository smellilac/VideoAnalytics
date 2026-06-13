using FluentValidation;

namespace VideoAnalytics.Infrastructure.Persistence;

internal sealed class ClickHouseSettingsValidator : AbstractValidator<ClickHouseSettings>
{
    public ClickHouseSettingsValidator()
    {
        RuleFor(x => x.ConnectionString).NotEmpty();
    }
}