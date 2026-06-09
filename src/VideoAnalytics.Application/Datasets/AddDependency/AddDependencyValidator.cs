namespace VideoAnalytics.Application.Datasets.AddDependency;

using FluentValidation;

public sealed class AddDependencyValidator : AbstractValidator<AddDependencyCommand>
{
    public AddDependencyValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();
        RuleFor(x => x.DependsOnDatasetId)
            .NotEmpty()
            .NotEqual(x => x.DatasetId)
            .WithMessage("A dataset cannot depend on itself.");
    }
}
