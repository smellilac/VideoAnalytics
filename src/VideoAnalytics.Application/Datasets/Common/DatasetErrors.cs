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

    public static Error InvalidStatus(Guid id, DatasetStatus current, DatasetStatus expected) =>
        Error.Validation(
            code: "Dataset.InvalidStatus",
            description: $"Dataset {id} is in {current} status; expected {expected}.");

    public static Error InvalidStatus(Guid id, DatasetStatus current) =>
        Error.Validation(
            code: "Dataset.InvalidStatus",
            description: $"Dataset {id} has invalid status {current} for this operation.");

    public static Error DependencyTargetNotFound(Guid dependsOnDatasetId) =>
        Error.NotFound(
            code: "Dataset.DependencyTargetNotFound",
            description: $"Dependency target dataset with ID {dependsOnDatasetId} was not found.");

    public static Error DependencyAlreadyExists(Guid datasetId, Guid dependsOnDatasetId) =>
        Error.Conflict(
            code: "Dataset.DependencyAlreadyExists",
            description: $"Dataset {datasetId} already depends on dataset {dependsOnDatasetId}.");
}
