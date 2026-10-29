using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Api.Agente.UnitTests.Foundation;

public class ProviderLedgerTests
{
    [Fact]
    public async Task FailedPreCallLedgerNeverInvokesProvider()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1")
            .AddInterceptors(new UnavailableLedger()).Options;
        await using var database = new AgentDbContext(options);
        var store = new ProviderCallAttemptStore(database, TimeProvider.System);
        var invoked = false;
        var request = new ProviderCallRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ProviderOperation.ChatCompletion, "Fake", "fake-analysis", 1, AttemptReason.Initial);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.ExecuteRecordedAsync(request, (_, _) =>
        {
            invoked = true;
            return Task.FromResult(new ProviderResponse<string>("synthetic", new UsageMeasurement(null, null, null, null)));
        }));
        Assert.False(invoked);
    }

    private sealed class UnavailableLedger : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Ledger indisponivel.");
    }
}
