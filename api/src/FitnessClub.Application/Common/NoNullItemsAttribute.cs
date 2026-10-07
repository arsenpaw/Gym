using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NoNullItemsAttribute() : ValidationAttribute("The {0} field cannot contain null items.")
{
    public override bool IsValid(object? value) => value is not IEnumerable items || items.Cast<object?>().All(item => item is not null);
}
