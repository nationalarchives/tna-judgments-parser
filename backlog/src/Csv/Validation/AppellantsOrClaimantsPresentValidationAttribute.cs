using System;
using System.ComponentModel.DataAnnotations;

namespace Backlog.Csv;

/// <summary>
///     Custom validation attribute to ensure one of appellants or claimants are provided
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AppellantsOrClaimantsPresentValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is not CsvLine line)
        {
            throw new InvalidOperationException(
                $"{nameof(AppellantsOrClaimantsPresentValidationAttribute)} can only be used on a {nameof(CsvLine)}");
        }

        // Check that exactly one of claimants or appellants is provided
        var hasClaimants = !string.IsNullOrWhiteSpace(line.claimants);
        var hasAppellants = !string.IsNullOrWhiteSpace(line.appellants);

        return (hasClaimants, hasAppellants) switch
        {
            { hasClaimants: true, hasAppellants: true } => new ValidationResult(
                $"Id {line.id} - Cannot have both claimants and appellants. Please provide only one."),
            { hasClaimants: false, hasAppellants: false } => new ValidationResult(
                $"Id {line.id} - Must have either claimants or appellants. At least one is required."),
            _ => ValidationResult.Success
        };
    }
}
