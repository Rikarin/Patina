using Patina.Ipc;
using Patina.Server.Api;

namespace Patina.Server.Tests;

/// <summary>
///     A request to the GPU engine that fails goes out with a code, so the app says it in its words rather than
///     showing the engine's or the system's sentence (#198).
/// </summary>
public sealed class EngineRefusalTests {
    [Fact]
    public void A_failed_request_to_the_engine_carries_a_code() {
        Assert.Equal(
            [Refusals.RendererMissing, Refusals.EngineRefused, Refusals.EngineUnavailable, Refusals.EngineUnavailable],
            new Exception[] {
                new RendererMissingException(), new IpcException("No graph."), new TimeoutException(),
                new IOException("The pipe is broken.")
            }.Select(static failure => (string?)Refusals.OfEngine(failure, 503).ProblemDetails.Extensions["code"])
        );
    }

    [Fact]
    public void A_refusal_of_the_engine_keeps_its_status_and_its_sentence_as_the_reason() {
        var problem = Refusals.OfEngine(new IpcException("Open a mesh first."), 422).ProblemDetails;

        Assert.Equal(422, problem.Status);
        Assert.Equal(
            "Open a mesh first.",
            Assert.IsType<IReadOnlyDictionary<string, string>>(problem.Extensions["values"], false)["reason"]
        );
    }
}
