using FluentValidation;
using Microsoft.Extensions.Options;

namespace VideoAnalytics.Infrastructure;

internal sealed class FluentValidateOptions<TOptions>(IValidator<TOptions> validator)
    : IValidateOptions<TOptions> where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var result = validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => e.ErrorMessage));
    }
}