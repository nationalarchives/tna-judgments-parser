using System;
using System.ComponentModel.DataAnnotations;

namespace Backlog.Csv;

/// <summary>
///     Custom validation attribute to ensure subcategories can only exist if their parent category is defined
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CategoryValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is not CsvLine line)
        {
            throw new InvalidOperationException(
                $"{nameof(CategoryValidationAttribute)} can only be used on a {nameof(CsvLine)}");
        }

        // Check if main_subcategory exists without main_category
        if (!string.IsNullOrWhiteSpace(line.main_subcategory) && string.IsNullOrWhiteSpace(line.main_category))
        {
            return new ValidationResult(
                $"Id {line.id} - main_subcategory '{line.main_subcategory}' cannot exist without main_category being defined");
        }

        // Check if sec_subcategory exists without sec_category
        if (!string.IsNullOrWhiteSpace(line.sec_subcategory) && string.IsNullOrWhiteSpace(line.sec_category))
        {
            return new ValidationResult(
                $"Id {line.id} - sec_subcategory '{line.sec_subcategory}' cannot exist without sec_category being defined");
        }

        return ValidationResult.Success;
    }
}
