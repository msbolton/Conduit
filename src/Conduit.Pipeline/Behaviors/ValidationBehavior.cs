using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Conduit.Core.Behaviors;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Pipeline behavior that validates messages using data annotations
/// </summary>
public class ValidationBehavior : IPipelineBehavior
{
    private readonly ILogger<ValidationBehavior> _logger;
    private readonly ValidationBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the ValidationBehavior class
    /// </summary>
    public ValidationBehavior(ILogger<ValidationBehavior> logger, ValidationBehaviorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ValidationBehaviorOptions();
    }

    /// <summary>
    /// Executes validation before proceeding with the pipeline
    /// </summary>
    public async Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        if (context.Input == null)
        {
            if (_options.AllowNullMessages)
            {
                return await next.ProceedAsync(context);
            }

            var error = "Message cannot be null for validation";
            _logger.LogError(error);
            throw new ValidationException(error);
        }

        var validationContext = new ValidationContext(context.Input);
        var validationResults = new List<ValidationResult>();

        // Perform validation
        var isValid = Validator.TryValidateObject(context.Input, validationContext, validationResults, _options.ValidateAllProperties);

        // Custom validation if provided
        if (isValid && _options.CustomValidators.Count > 0)
        {
            foreach (var customValidator in _options.CustomValidators)
            {
                var customResult = await customValidator(context.Input, context);
                if (customResult != null)
                {
                    validationResults.AddRange(customResult);
                    isValid = false;
                }
            }
        }

        if (!isValid)
        {
            var errors = validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error").ToList();
            var errorMessage = $"Validation failed for {context.Input.GetType().Name}: {string.Join(", ", errors)}";

            _logger.LogWarning("Validation failed for message {MessageType}: {ValidationErrors}",
                context.Input.GetType().Name, string.Join("; ", errors));

            // Store validation errors in context
            context.SetProperty("ValidationErrors", errors);

            if (_options.ThrowOnValidationFailure)
            {
                throw new ValidationException(errorMessage);
            }

            // Set validation failure flag for other behaviors to check
            context.SetProperty("ValidationFailed", true);

            if (_options.ReturnErrorsOnFailure)
            {
                return new ValidationErrorResult
                {
                    IsValid = false,
                    Errors = errors,
                    MessageType = context.Input.GetType().Name
                };
            }
        }
        else
        {
            _logger.LogDebug("Validation passed for message {MessageType}", context.Input.GetType().Name);
            context.SetProperty("ValidationPassed", true);
        }

        return await next.ProceedAsync(context);
    }
}

/// <summary>
/// Configuration options for the validation behavior
/// </summary>
public class ValidationBehaviorOptions
{
    /// <summary>
    /// Whether to validate all properties or stop at first error
    /// </summary>
    public bool ValidateAllProperties { get; set; } = true;

    /// <summary>
    /// Whether to throw an exception on validation failure
    /// </summary>
    public bool ThrowOnValidationFailure { get; set; } = true;

    /// <summary>
    /// Whether to allow null messages to pass through
    /// </summary>
    public bool AllowNullMessages { get; set; } = false;

    /// <summary>
    /// Whether to return validation errors as the result on failure
    /// </summary>
    public bool ReturnErrorsOnFailure { get; set; } = false;

    /// <summary>
    /// Custom validation functions
    /// </summary>
    public List<Func<object, PipelineContext, Task<List<ValidationResult>?>>> CustomValidators { get; set; } = new();

    /// <summary>
    /// Adds a custom validator function
    /// </summary>
    public ValidationBehaviorOptions AddCustomValidator(Func<object, PipelineContext, Task<List<ValidationResult>?>> validator)
    {
        CustomValidators.Add(validator);
        return this;
    }

    /// <summary>
    /// Adds a synchronous custom validator function
    /// </summary>
    public ValidationBehaviorOptions AddCustomValidator(Func<object, PipelineContext, List<ValidationResult>?> validator)
    {
        CustomValidators.Add((message, context) => Task.FromResult(validator(message, context)));
        return this;
    }
}

/// <summary>
/// Result returned when validation fails and ReturnErrorsOnFailure is true
/// </summary>
public class ValidationErrorResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation error messages
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Type of the message that failed validation
    /// </summary>
    public string MessageType { get; set; } = string.Empty;
}