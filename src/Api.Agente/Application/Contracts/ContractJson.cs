using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Agente.Application.Contracts;

public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static T Deserialize<T>(string json)
    {
        var result = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException("Contrato nulo.");
        Validate(result);
        return result;
    }

    public static void Validate(object value)
    {
        Validator.ValidateObject(value, new ValidationContext(value), validateAllProperties: true);
        foreach (var property in value.GetType().GetProperties())
        {
            var child = property.GetValue(value);
            if (child is Uri uri && !uri.IsAbsoluteUri) throw new ValidationException("URI absoluta obrigatoria.");
            if (child is null || child is string || child.GetType().IsValueType || child is Uri) continue;
            if (child is IEnumerable children)
            {
                foreach (var item in children)
                {
                    if (item is null) throw new ValidationException("Colecao com item nulo.");
                    if (item is not string && !item.GetType().IsValueType) Validate(item);
                }
            }
            else Validate(child);
        }
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class PositivePagesAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is IEnumerable<long> pages && pages.All(page => page >= 1);
}
