using Confluent.Kafka;
using FluentValidation;

namespace VideoAnalytics.Infrastructure.Kafka;

internal sealed class KafkaSettingsValidator : AbstractValidator<KafkaSettings>
{
    public KafkaSettingsValidator()
    {
        RuleFor(x => x.BootstrapServers).NotEmpty();
        RuleFor(x => x.StatusChangedTopic).NotEmpty();
        RuleFor(x => x.DatasetReadyTopic).NotEmpty();

        RuleFor(x => x.MessageSendMaxRetries).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RetryBackoffMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MessageTimeoutMs).GreaterThan(0);

        RuleFor(x => x.CircuitBreakerMinimumThroughput).GreaterThan(0);
        RuleFor(x => x.CircuitBreakerSamplingDurationSeconds).GreaterThan(0);
        RuleFor(x => x.CircuitBreakerBreakDurationSeconds).GreaterThan(0);

        RuleFor(x => x.BatchSize).GreaterThan(0);
        RuleFor(x => x.LingerMs).GreaterThanOrEqualTo(0);

        RuleFor(x => x.PipelineEventsTopic).NotEmpty();
        RuleFor(x => x.PipelineEventsDlqTopic).NotEmpty();
        RuleFor(x => x.ConsumerGroupId).NotEmpty();

        RuleFor(x => x.SessionTimeoutMs).GreaterThan(0);
        RuleFor(x => x.HeartbeatIntervalMs).GreaterThan(0);
        RuleFor(x => x.MaxPollIntervalMs).GreaterThan(0);

        // The lower bound of 1 closes the edge case "0 attempts = silent skip without processing."
        // The upper bound of 10 is a guard rail against a typo (30 instead of 3), which in production
        // would have meant blocking the partition for ~17 minutes instead of ~7 seconds (Section 4.2).
        RuleFor(x => x.ConsumerMaxRetries).InclusiveBetween(1, 10);
        RuleFor(x => x.ConsumerRetryBaseDelayMs).GreaterThan(0);

        // Cross-field SASL validation is something that DataAnnotations wouldn't express purely
        When(x => !string.IsNullOrEmpty(x.SaslUsername), () =>
        {
            RuleFor(x => x.SaslPassword)
                .NotEmpty()
                .WithMessage("Kafka:SaslPassword is required when SaslUsername is set.");

            RuleFor(x => x.SaslMechanism)
                .Must(BeValidEnum<SaslMechanism>)
                .WithMessage(x => $"Kafka:SaslMechanism '{x.SaslMechanism}' is not a valid SaslMechanism.");

            RuleFor(x => x.SecurityProtocol)
                .Must(BeValidEnum<SecurityProtocol>)
                .WithMessage(x => $"Kafka:SecurityProtocol '{x.SecurityProtocol}' is not a valid SecurityProtocol.");
        });
    }

    private static bool BeValidEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out _);
}