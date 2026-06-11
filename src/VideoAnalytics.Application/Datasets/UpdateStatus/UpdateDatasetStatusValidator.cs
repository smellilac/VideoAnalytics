using System.Text.Json;

namespace VideoAnalytics.Application.Datasets.UpdateStatus;

using FluentValidation;

public sealed class UpdateDatasetStatusValidator : AbstractValidator<UpdateDatasetStatusCommand>
{
    public UpdateDatasetStatusValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.Metadata)
            .Must(m => m is null || m.RootElement.ValueKind == JsonValueKind.Object)
            .WithMessage("Metadata must be a JSON object.");
    }
}
