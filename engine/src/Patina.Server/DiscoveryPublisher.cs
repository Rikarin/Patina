using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Patina.Contracts;
using Patina.Library;

namespace Patina.Server;

/// <summary>
///     Lists this Server in the per-user discovery file once it listens, and removes it on the way out. A library that
///     already has a running Server stops this one with exit code 1: one Server per library per machine (plan §8).
/// </summary>
public sealed class DiscoveryPublisher(
    ServerIdentity identity,
    IOptions<ServerOptions> options,
    IServer server,
    IHostApplicationLifetime lifetime,
    ILogger<DiscoveryPublisher> logger
) : IHostedService {
    readonly InstanceRegistry registry = new(options.Value.DiscoveryFile ?? InstanceRegistry.DefaultPath);
    CancellationTokenRegistration started;
    Task published = Task.CompletedTask;
    bool listed;

    public Task StartAsync(CancellationToken cancellationToken) {
        if (identity is { Library: not null, Token: not null }) {
            started = lifetime.ApplicationStarted.Register(() => published = PublishAsync());
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        await started.DisposeAsync();
        await published;
        if (listed) {
            await registry.UnregisterAsync(Environment.ProcessId, CancellationToken.None);
        }
    }

    async Task PublishAsync() {
        // No address means no real listener (the in-memory test server), so there is nothing to discover.
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (address is null) {
            return;
        }

        var instance = new ServerInstance(
            identity.Library!,
            Environment.ProcessId,
            new Uri(address).Port,
            identity.Token!,
            ProductInfo.Version
        );
        // Not cancelled by a stop: StopAsync waits for this, and then knows whether there is an entry to remove.
        try {
            if (await registry.RegisterAsync(instance, CancellationToken.None) is { } owner) {
                ServerLog.LibraryTaken(logger, owner.Library, owner.ProcessId);
                Refuse();
                return;
            }

            listed = true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            ServerLog.DiscoveryFailed(logger, e.Message);
            Refuse();
        }
    }

    /// <summary>Without the discovery file nothing guarantees one Server per library, so this one stops.</summary>
    void Refuse() {
        Environment.ExitCode = 1;
        lifetime.StopApplication();
    }
}
