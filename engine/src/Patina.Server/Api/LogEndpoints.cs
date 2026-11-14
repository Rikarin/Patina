using Microsoft.AspNetCore.Http.HttpResults;
using Patina.Contracts;

namespace Patina.Server.Api;

/// <summary>What the Server said lately, for a client's log pane (#192). Any client reads it; none writes it.</summary>
static class LogEndpoints {
    public static void MapLogApi(this IEndpointRouteBuilder api) =>
        api.MapGet("/log", Read)
            .WithName("GetServerLog")
            .WithTags("System")
            .WithSummary(
                "The lines the Server logged after a number, the oldest first: Patina's own from information up, the "
                + "framework's from warnings up. Only the latest thousand are kept; ask after the page's next number "
                + "to follow along."
            );

    static Ok<ServerLogPage> Read(LogRing ring, long after = 0) => TypedResults.Ok(ring.After(Math.Max(0, after)));
}
