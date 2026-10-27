using System.ComponentModel.DataAnnotations;

namespace Novaforge.Web.Services;

/// <summary>Runs DataAnnotations validation (including nested lists) and throws a 400-mapped exception.</summary>
public static class RequestValidator
{
    public static void Validate(object model)
    {
        var errors = new Dictionary<string, List<string>>();
        Collect(model, prefix: "", errors);

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }

    private static void Collect(object model, string prefix, Dictionary<string, List<string>> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        foreach (var result in results)
        {
            IEnumerable<string> members = result.MemberNames.Any() ? result.MemberNames : [""];
            foreach (var member in members)
            {
                var key = prefix + member;
                if (!errors.TryGetValue(key, out var list))
                {
                    errors[key] = list = [];
                }
                list.Add(result.ErrorMessage ?? "Invalid value.");
            }
        }

        // Validate items of List<T> properties (e.g. invoice lines).
        foreach (var property in model.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length > 0 || property.PropertyType == typeof(string))
            {
                continue;
            }
            if (property.GetValue(model) is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is { } item && !item.GetType().IsPrimitive)
                    {
                        Collect(item, $"{prefix}{property.Name}[{i}].", errors);
                    }
                }
            }
        }
    }
}
