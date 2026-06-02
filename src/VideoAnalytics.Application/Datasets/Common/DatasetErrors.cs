namespace VideoAnalytics.Application.Datasets.Common;

using ErrorOr;
using VideoAnalytics.Domain.Datasets;

public static class DatasetErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound(
            code: "Dataset.NotFound",
            description: $"Dataset with ID {id} was not found.");

    public static Error AlreadyExists(string name, string version) =>
        Error.Conflict(
            code: "Dataset.AlreadyExists",
            description: $"Dataset '{name}' version '{version}' already exists.");

    public static Error InvalidTransition(DatasetStatus from, DatasetStatus to) =>
        Error.Validation(
            code: "Dataset.InvalidTransition",
            description: $"Transition from {from} to {to} is not allowed.");

    public static Error DependenciesNotReady(string? reason) =>
        Error.Validation(
            code: "Dataset.DependenciesNotReady",
            description: $"Cannot transition to Ready: {reason}");

    public static Error ArtifactMissing(IReadOnlyList<string> s3Keys) =>
        Error.Validation(
            code: "Dataset.ArtifactMissing",
            description: $"Artifacts not found in S3: {string.Join(", ", s3Keys.Select(k => $"'{k}'"))}");
}
