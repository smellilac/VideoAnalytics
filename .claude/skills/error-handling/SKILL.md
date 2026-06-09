---
name: error-handling
description: >
  Error handling strategy for .NET 10 applications. Covers the ErrorOr library,
  ProblemDetails (RFC 9457), global exception handling, FluentValidation, and
  structured error responses.
  Load this skill when implementing error handling, validation, or designing
  API error contracts, or when the user mentions "error handling", "ErrorOr",
  "ProblemDetails", "exception", "validation", "FluentValidation", "error response",
  "global exception handler", or "RFC 9457".
---

# Error Handling

## Core Principles

1. **Use ErrorOr for expected failures** — Don't throw exceptions for things like "order not found" or "validation failed". These are expected outcomes, not exceptional conditions. Return `ErrorOr<T>` from handlers.
2. **Reserve exceptions for unexpected failures** — Database connection lost, null reference bugs, network timeouts — these are truly exceptional and should propagate to the global handler.
3. **Every API error returns ProblemDetails** — RFC 9457 is the standard. Every error response has `type`, `title`, `status`, `detail`, and optionally `errors`.
4. **Validate at the boundary** — Validate incoming requests at the API layer, not deep inside business logic.

## Patterns

### Result Pattern (ErrorOr)

Use the **ErrorOr** library. It provides a ready-made `ErrorOr<T>` type that carries either a value or a list of errors. Do not write a custom `Result<T>` — it is unnecessary boilerplate and a reinvention of the wheel.

**NuGet:** `ErrorOr` (https://github.com/amantinband/error-or)

```csharp
using ErrorOr;

// Handler returns ErrorOr<T>
public async ValueTask<ErrorOr<RegisterDatasetResponse>> Handle(
    RegisterDatasetCommand command, CancellationToken cancellationToken)
{
    if (await repository.ExistsAsync(command.Name, command.Version, cancellationToken))
        return Error.Conflict(
            code: "Dataset.AlreadyExists",
            description: $"Dataset '{command.Name}' v'{command.Version}' already exists.");

    var dataset = Dataset.Create(/* ... */);
    await repository.AddAsync(dataset, cancellationToken);

    return new RegisterDatasetResponse(dataset.Id, dataset.Name, dataset.Version);
}
```

**Key features of ErrorOr:**

- `ErrorOr<T>` — generic type that returns either a value or an error
- Implicit conversions: return `T` or `Error` directly without wrappers
- `Error.NotFound()`, `Error.Conflict()`, `Error.Validation()`, `Error.Unauthorized()`, `Error.Forbidden()`, `Error.Failure()`, `Error.Unexpected()` — built-in factory methods for typed errors
- `result.IsError` — check whether an error is present
- `result.Errors` — list of errors (a single operation may return multiple)
- `result.Value` — the value on success

### ErrorOr to ProblemDetails Mapping

`Error` is mapped to an HTTP response via an extension that uses `Error.Type` (the built-in `ErrorType` enum from ErrorOr) to determine the status code.

```csharp
using ErrorOr;

public static class ErrorOrExtensions
{
    public static IResult ToHttpResult(this Error error) => error.Type switch
    {
        ErrorType.NotFound      => TypedResults.Problem(error.Description, statusCode: 404, title: error.Code),
        ErrorType.Conflict      => TypedResults.Problem(error.Description, statusCode: 409, title: error.Code),
        ErrorType.Validation    => TypedResults.Problem(error.Description, statusCode: 400, title: error.Code),
        ErrorType.Unauthorized  => TypedResults.Problem(error.Description, statusCode: 401, title: error.Code),
        ErrorType.Forbidden     => TypedResults.Problem(error.Description, statusCode: 403, title: error.Code),
        _                       => TypedResults.Problem(error.Description, statusCode: 500, title: error.Code)
    };

    // For cases when a handler returns multiple errors at once
    public static IResult ToHttpResult(this List<Error> errors)
    {
        if (errors.Count == 1)
            return errors[0].ToHttpResult();

        // All errors share the same type — use a common status code
        var firstError = errors[0];
        return TypedResults.Problem(
            title: "One or more errors occurred",
            statusCode: firstError.Type switch
            {
                ErrorType.Validation => 400,
                ErrorType.NotFound => 404,
                ErrorType.Conflict => 409,
                _ => 500
            },
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors.Select(e => new { e.Code, e.Description })
            });
    }
}

// Usage in endpoint
group.MapPost("/", async (CreateOrder.Command command, ISender sender, CancellationToken ct) =>
{
    var result = await sender.Send(command, ct);
    return result.Match<IResult>(
        value => TypedResults.Created($"/api/orders/{value.Id}", value),
        errors => errors.ToHttpResult());
});
```

**Alternative syntax** — use ErrorOr's built-in `MatchFirst()` when only the first error matters:

```csharp
return result.MatchFirst<IResult>(
    value => TypedResults.Created($"/api/orders/{value.Id}", value),
    error => error.ToHttpResult());
```

### Global Exception Handler

Catches unexpected exceptions and converts them to ProblemDetails. For the modern `IExceptionHandler` approach (preferred), see `knowledge/common-infrastructure.md`. The inline lambda below works for simple cases:

```csharp
// Program.cs
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            context.Request.Method, context.Request.Path);

        var problem = new ProblemDetails
        {
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        };

        // Don't leak details in production
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            problem.Detail = exception?.Message;
        }

        context.Response.StatusCode = problem.Status.Value;
        await context.Response.WriteAsJsonAsync(problem);
    });
});
```

### FluentValidation with Endpoint Filters

```csharp
// Validator
public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).NotEmpty();
            item.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

// Generic validation filter
public class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null)
            return await next(context);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return await next(context);

        var result = await validator.ValidateAsync(request);
        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}

// Registration
group.MapPost("/", CreateOrder)
    .AddEndpointFilter<ValidationFilter<CreateOrderRequest>>();
```

### Typed Error Results

ErrorOr ships with a built-in `Error` struct and an `ErrorType` enum, so there is no need to define a custom `abstract record Error` hierarchy. Use the factory methods provided by the library.

```csharp
using ErrorOr;

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

    public static Error DependenciesNotReady(string reason) =>
        Error.Validation(
            code: "Dataset.DependenciesNotReady",
            description: $"Cannot transition to Ready: {reason}");
}
```

**Usage in handlers:**

```csharp
public async ValueTask<ErrorOr<Dataset>> Handle(GetDatasetQuery query, CancellationToken ct)
{
    var dataset = await repository.GetByIdAsync(query.Id, ct);
    if (dataset is null)
        return DatasetErrors.NotFound(query.Id);

    return dataset;
}
```

**Conventions:**

- Group domain-specific errors in static factory classes (e.g., `DatasetErrors`, `OrderErrors`)
- Use dotted codes that describe the entity and the failure: `Dataset.NotFound`, `Order.PaymentFailed`
- Pick the `Error.*` factory that matches the semantics — the `ErrorType` drives HTTP mapping (see `ErrorOr to ProblemDetails Mapping`)
- Available factories: `Error.NotFound`, `Error.Conflict`, `Error.Validation`, `Error.Unauthorized`, `Error.Forbidden`, `Error.Failure`, `Error.Unexpected`

**HTTP mapping** is centralised in `ErrorOrExtensions.ToHttpResult()` (see previous section) — do not duplicate switch statements in each endpoint.

## Anti-patterns

### Don't Throw Exceptions for Flow Control

```csharp
// BAD — exceptions for expected outcomes
public Order GetOrder(Guid id)
{
    var order = db.Orders.Find(id)
        ?? throw new NotFoundException($"Order {id} not found");
    return order;
}

// GOOD — ErrorOr
public ErrorOr<Order> GetOrder(Guid id)
{
    var order = db.Orders.Find(id);
    return order is not null
        ? order
        : Error.NotFound(code: "Order.NotFound", description: $"Order {id} not found.");
}
```

### Don't Return Raw Error Strings from APIs

```csharp
// BAD — inconsistent error format
return Results.BadRequest("Something went wrong");
return Results.BadRequest(new { error = "Invalid input" });

// GOOD — always ProblemDetails
return TypedResults.Problem(title: "Invalid input", statusCode: 400);
return TypedResults.ValidationProblem(validationResult.ToDictionary());
```

### Don't Catch and Swallow Exceptions

```csharp
// BAD — silently swallowing
try { await ProcessOrder(order); }
catch (Exception) { /* ignore */ }

// GOOD — log and handle appropriately
try { await ProcessOrder(order); }
catch (PaymentException ex)
{
    logger.LogWarning(ex, "Payment failed for order {OrderId}", order.Id);
    return Error.Failure(code: "Order.PaymentFailed", description: "Payment processing failed.");
}
```

### Don't Write a Custom `Result<T>` Type

```csharp
// BAD — reinventing the wheel
public class Result<T> { public bool IsSuccess { get; } public List<string> Errors { get; } /* ... */ }

// GOOD — use ErrorOr
public ErrorOr<T> /* ... */
```

ErrorOr already provides `ErrorOr<T>`, `Error`, `ErrorType`, implicit conversions, `Match()`/`MatchFirst()`, and a structured error contract. Writing a custom `Result<T>` adds maintenance cost without functional gain.

### Don't Mix Custom `Result<T>` and `ErrorOr<T>` in the Same Codebase

Pick one and stay consistent across all layers. This skill mandates `ErrorOr<T>` — do not introduce a parallel custom `Result<T>` anywhere in the project.

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Expected business failure | Return ErrorOr<T> with appropriate Error.* |
| Input validation | FluentValidation with endpoint filter |
| Unexpected crash | Global exception handler → ProblemDetails |
| API error format | RFC 9457 ProblemDetails — always |
| Validation in handler | Return Error.Validation(), don't throw |
| External service failure | Catch specific exception, return Error.Failure() |
| Logging errors | Structured logging with correlation ID |
