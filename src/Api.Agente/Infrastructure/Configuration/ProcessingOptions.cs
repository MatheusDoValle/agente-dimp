using Microsoft.Extensions.Options;

namespace Api.Agente.Infrastructure.Configuration;

public sealed class ProcessingOptions
{
    public double? GlobalDeadlineSeconds { get; set; }
    public double? ProviderTimeoutSeconds { get; set; }
    public int? MaximumProviderCalls { get; set; }
    public int? TokenBudget { get; set; }
    public int MaximumTransientAttempts { get; set; } = 3;
    public int MaximumSemanticRepairs { get; set; } = 1;
    public string? Model { get; set; }
}

public sealed class ProcessingOptionsValidator(IConfiguration configuration) : IValidateOptions<ProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, ProcessingOptions options)
    {
        var enabled = configuration.GetValue<bool>("DevelopmentProfile:ExternalCallsEnabled");
        var errors = new List<string>();
        if (options.GlobalDeadlineSeconds is not { } deadline || !double.IsFinite(deadline) || deadline <= 0 || deadline > TimeSpan.MaxValue.TotalSeconds)
            errors.Add("Deadline global finito e positivo obrigatorio.");
        if (options.ProviderTimeoutSeconds is not { } timeout || !double.IsFinite(timeout) || timeout <= 0
            || timeout > TimeSpan.MaxValue.TotalSeconds || timeout > options.GlobalDeadlineSeconds)
            errors.Add("Timeout finito deve caber no deadline.");
        if (options.MaximumProviderCalls is null or < 0 || enabled && options.MaximumProviderCalls == 0)
            errors.Add("Budget de chamadas invalido.");
        if (options.TokenBudget is null or < 0 || enabled && options.TokenBudget == 0)
            errors.Add("Budget de tokens invalido.");
        if (options.MaximumTransientAttempts is < 1 or > 3 || options.MaximumSemanticRepairs is < 0 or > 1)
            errors.Add("Tentativas excedem a politica de 3 tentativas e 1 reparo.");
        if (string.IsNullOrWhiteSpace(options.Model) || !System.Text.RegularExpressions.Regex.IsMatch(options.Model, "^[A-Za-z0-9][A-Za-z0-9._:/-]{0,127}$")
            || options.Model.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
            errors.Add("Identidade tecnica de modelo obrigatoria; nunca usar credencial.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class TemporaryStorageOptions
{
    public string RootPath { get; set; } = "";
}
