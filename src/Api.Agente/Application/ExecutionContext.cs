using Api.Agente.Domain;

namespace Api.Agente.Application;

public sealed class ExecutionContext(TimeProvider clock, IConfiguration configuration)
{
    public SafeConfigurationSnapshot CreateConfigurationSnapshot()
    {
        var options = configuration.GetSection("Processing").Get<Infrastructure.Configuration.ProcessingOptions>()
            ?? new Infrastructure.Configuration.ProcessingOptions();
        var validation = new Infrastructure.Configuration.ProcessingOptionsValidator(configuration).Validate(null, options);
        if (validation.Failed) throw new InvalidOperationException("Perfil operacional invalido.");
        return new SafeConfigurationSnapshot(Guid.NewGuid(), clock.GetUtcNow().ToUniversalTime(), options.Model!,
            options.GlobalDeadlineSeconds!.Value, options.ProviderTimeoutSeconds!.Value,
            options.MaximumProviderCalls!.Value, options.TokenBudget!.Value,
            options.MaximumTransientAttempts, options.MaximumSemanticRepairs);
    }

    public Execution CreateExecution()
    {
        var operatorId = configuration["ExecutionContext:OperatorId"] ?? "";
        var contextId = configuration["ExecutionContext:ContextId"] ?? "";
        Execution.RequireLocalAlias(operatorId);
        Execution.RequireLocalAlias(contextId);
        if (configuration["ExecutionContext:IdentityAssurance"] != "unverified_local_configuration")
            throw new InvalidOperationException("Identificacao local nao autenticada deve ser explicita.");
        return new Execution(Guid.NewGuid(), Guid.NewGuid(), operatorId, contextId, clock.GetUtcNow());
    }
}

public sealed record SafeConfigurationSnapshot(Guid Id, DateTimeOffset CreatedAt, string Model,
    double GlobalDeadlineSeconds, double ProviderTimeoutSeconds, int MaximumProviderCalls,
    int TokenBudget, int MaximumTransientAttempts, int MaximumSemanticRepairs);
