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
        if (!string.IsNullOrWhiteSpace(line.MainSubcategory) && string.IsNullOrWhiteSpace(line.MainCategory))
        {
            return new ValidationResult(
                $"Id {line.id} - main_subcategory '{line.MainSubcategory}' cannot exist without main_category being defined");
        }

        // Check if sec_subcategory exists without sec_category
        if (!string.IsNullOrWhiteSpace(line.SecSubcategory) && string.IsNullOrWhiteSpace(line.SecCategory))
        {
            return new ValidationResult(
                $"Id {line.id} - sec_subcategory '{line.SecSubcategory}' cannot exist without sec_category being defined");
        }

        return ValidationResult.Success;
    }
}
