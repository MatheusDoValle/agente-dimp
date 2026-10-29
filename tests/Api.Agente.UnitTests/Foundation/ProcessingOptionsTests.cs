using Api.Agente.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Agente.UnitTests.Foundation;

public class ProcessingOptionsTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonFiniteOrNonPositiveDeadlinesAreRejected(double deadline)
    {
        var options = ValidOffline();
        options.GlobalDeadlineSeconds = deadline;
        Assert.True(Validator(false).Validate(null, options).Failed);
    }

    [Fact]
    public void ExternalCallsRequirePositiveBudgetsAndBoundedRetries()
    {
        var options = ValidOffline();
        Assert.True(Validator(false).Validate(null, options).Succeeded);
        Assert.True(Validator(true).Validate(null, options).Failed);
        options.MaximumProviderCalls = 4;
        options.TokenBudget = 1000;
        Assert.True(Validator(true).Validate(null, options).Succeeded);
        options.MaximumTransientAttempts = 4;
        Assert.True(Validator(true).Validate(null, options).Failed);
        options.MaximumTransientAttempts = 3;
        options.MaximumSemanticRepairs = 2;
        Assert.True(Validator(true).Validate(null, options).Failed);
    }

    private static ProcessingOptions ValidOffline() => new()
    {
        GlobalDeadlineSeconds = 120, ProviderTimeoutSeconds = 30,
        MaximumProviderCalls = 0, TokenBudget = 0, Model = "fake-analysis"
    };
    private static ProcessingOptionsValidator Validator(bool enabled) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentProfile:ExternalCallsEnabled"] = enabled.ToString() }).Build());
}
