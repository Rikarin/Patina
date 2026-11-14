using Patina.Contracts;
using System.Net;

namespace Patina.Server.Tests;

/// <summary>
///     The 2D View's readout at the picture's own precision (#184): one texel as the graph stored it, through the real
///     Server and Worker, where the preview's PNG holds eight bits a lane.
/// </summary>
public sealed partial class GraphProcessTests {
    [Fact]
    public async Task A_sample_reads_one_texel_as_stored_and_the_png_rounds_it() {
        using var deadline = Deadline(120);
        var (_, state, socket, _) = await OpenSampleAsync(GraphTesting.Sample(512), deadline.Token);
        using (socket) {
            var picture = await FullAsync(state.Id, new(2, "Out"), deadline.Token);
            var finer = 0;
            foreach (var (u, v) in new[] { (0.1, 0.2), (0.5, 0.5), (0.93, 0.71) }) {
                var texel = await SampleAsync(state.Id, new(2, "Out", u, v), deadline.Token);
                Assert.Equal((2, "Out", 512, 512), (texel.Node, texel.Port, texel.Width, texel.Height));
                Assert.Equal(((int)(u * 512), (int)(v * 512)), (texel.X, texel.Y));
                Assert.Contains(texel.Values.Count, (int[])[1, 2, 4]);
                for (var lane = 0; lane < Math.Min(texel.Values.Count, 3); lane++) {
                    var stored = picture.Pixels[(texel.Y * picture.Width + texel.X) * 4 + lane] / 255d;
                    Assert.True(
                        Math.Abs(texel.Values[lane] - stored) <= 1d / 255d,
                        $"Lane {lane} at {u}, {v} is {texel.Values[lane]} and the PNG holds {stored}"
                    );
                    var bytes = texel.Values[lane] * 255d;
                    finer += Math.Abs(bytes - Math.Round(bytes)) > 1e-3 ? 1 : 0;
                }
            }

            // A float picture says more than eight bits can; an eight-bit one says exactly what the PNG does.
            var format = (await SampleAsync(state.Id, new(2, "Out", 0.5, 0.5), deadline.Token)).Format;
            var floating = format.Contains("Float", StringComparison.Ordinal);
            Assert.True(floating ? finer > 0 : finer == 0, $"{format} gave {finer} lanes finer than a byte");

            using var missing = await http.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/sample",
                new(2, "Nothing", 0.5, 0.5),
                ContractsJsonContext.Default.GraphSampleRequest,
                deadline.Token
            );
            Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        }
    }

    async Task<GraphSample> SampleAsync(string id, GraphSampleRequest request, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/graphs/{id}/sample",
            request,
            ContractsJsonContext.Default.GraphSampleRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphSample, token))!;
    }
}
