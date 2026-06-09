namespace VideoAnalytics.Application.Datasets.RegisterDataset;

using FluentValidation;

public sealed class RegisterDatasetValidator : AbstractValidator<RegisterDatasetCommand>
{
    public RegisterDatasetValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PipelineRunId)
            .NotEmpty();
    }
}
