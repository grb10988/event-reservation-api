using EventReservation.Application.DependencyInjection;
using EventReservation.Infrastructure.DependencyInjection;
using EventReservation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Infrastructure.Integration.Scenarios;

// Wires the REAL application pipeline (Dispatcher, every reflection-
// registered command handler, the real pipeline behaviors) against the
// REAL repositories, pointed at the test Postgres container - for
// scenarios that need to prove race resolves correctly through the
// actual handler chain, not just a single repository method.
public abstract class ScenarioTestBase : IntegrationTestBase
{
    protected FakeTimeProvider Clock { get; private set; } = null!;
    protected IServiceProvider Services { get; private set; } = null!;

    [TestInitialize]
    public void ScenarioSetup()
    {
        Clock = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IDbConnectionFactory>(ConnectionFactory);

        Services = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void ScenarioCleanup()
    {
        (Services as IDisposable)?.Dispose();
    }
}