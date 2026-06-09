namespace VideoAnalytics.Application.Datasets.RegisterArtifact;

using FluentValidation;
using VideoAnalytics.Domain.Datasets;

public sealed class RegisterArtifactValidator : AbstractValidator<RegisterArtifactCommand>
{
    public RegisterArtifactValidator()
    {
        RuleFor(x => x.DatasetId).NotEmpty();

        RuleFor(x => x.S3Key)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.ArtifactType)
            .NotEmpty()
            .Must(ArtifactFormats.IsValid)
            .WithMessage($"ArtifactType must be one of: {ArtifactFormats.AllowedValues}.");

        RuleFor(x => x.SizeBytes).GreaterThan(0);

        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
    }
}
