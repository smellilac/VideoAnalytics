namespace VideoAnalytics.Application.Datasets.UpdateStatus;

using FluentValidation;

public sealed class UpdateDatasetStatusValidator : AbstractValidator<UpdateDatasetStatusCommand>
{
    public UpdateDatasetStatusValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
