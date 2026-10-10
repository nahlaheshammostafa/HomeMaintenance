namespace HomeMaintenance.Application.Common.Extensions;

using FluentValidation.Results;

public static class ValidationExtensions
{
    /// <summary>
    /// Converts a FluentValidation ValidationResult into a dictionary grouped by property name,
    /// matching the format required by Error.Validation.
    /// </summary>
    public static Dictionary<string, string[]> ToDictionary(this ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );
    }
}
