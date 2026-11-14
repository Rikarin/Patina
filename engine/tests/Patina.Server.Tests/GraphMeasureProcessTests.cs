using Patina.Contracts;
using System.Net;
using Vixen.Core.Imaging;

namespace Patina.Server.Tests;

/// <summary>
///     What a graph measures about itself: each node's GPU time (#182's timing badges) and a picture's histogram
///     counted on the GPU at full size (#172), through the real Server and Worker.
/// </summary>
public sealed partial class GraphProcessTests {
    [Fact]
    public async Task Each_node_that_ran_has_a_gpu_time_and_a_cached_one_keeps_its_last() {
        using var deadline = Deadline(120);
        var (_, state, socket, first) = await OpenSampleAsync(GraphTesting.Sample(512), deadline.Token);
        using (socket) {
            // The noise, the blur, the HSL and the invert each ran; times are a ranking, so only their sign is sure.
            Assert.Superset(new HashSet<int> { 1, 2, 3, 5 }, first.Times.Select(static time => time.Node).ToHashSet());
            Assert.All(first.Times, static time => Assert.InRange(time.Milliseconds, 0, 60_000));
            Assert.True(first.Times.Sum(static time => time.Milliseconds) > 0);

            var edit = await http.EditAsync(state.Id, deadline.Token, new GraphSetValue(2, "Hue", [0.25f]));
            var slid = await EvaluatedAsync(socket, edit.Revision, deadline.Token);

            // Only what ran is timed again; the noise and the blur came from the result cache and keep their times.
            Assert.Equal([2, 3], slid.Times.Select(static time => time.Node).Order());
            var held = (await http.StateAsync(state.Id, deadline.Token)).Times.ToDictionary(
                static time => time.Node,
                static time => time.Milliseconds
            );
            Assert.Equal(first.Times.Single(static time => time.Node == 1).Milliseconds, held[1]);
            Assert.Equal(slid.Times.Single(static time => time.Node == 2).Milliseconds, held[2]);
        }
    }

    [Fact]
    public async Task A_histogram_counts_the_full_size_picture_per_channel() {
        using var deadline = Deadline(120);
        var (_, state, socket, _) = await OpenSampleAsync(GraphTesting.Sample(512), deadline.Token);
        using (socket) {
            var histogram = await HistogramAsync(state.Id, new(2, "Out"), deadline.Token);

            Assert.Equal((2, "Out", 512, 512), (histogram.Node, histogram.Port, histogram.Width, histogram.Height));
            IReadOnlyList<float>[] lanes = [
                histogram.Red, histogram.Green, histogram.Blue, histogram.Alpha, histogram.Luma
            ];
            Assert.All(lanes, static shares => Assert.Equal(256, shares.Count));
            Assert.All(lanes, static shares => Assert.Equal(1, shares.Sum(), 0.0001));

            // Against the full-size PNG, counted here: a byte rounds a value, so a value can move one bin, and the
            // distributions agree to within that. The sample's picture is a grey, which shows on all three.
            var picture = await FullAsync(state.Id, new(2, "Out"), deadline.Token);
            for (var lane = 0; lane < 3; lane++) {
                var gap = Gap(lanes[lane], Shares(picture, lane));
                Assert.True(gap < 0.02, $"Lane {lane}'s distribution is {gap} from the picture's");
            }

            // The instrument: a lane read as nought, or as one, would be a single bin, where the noise spreads.
            Assert.All(lanes[..3], static shares => Assert.True(shares.Max() < 0.5f, $"A bin holds {shares.Max()}"));
            Assert.Equal(1, histogram.Alpha[255], 0.0001);

            using var missing = await http.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/histogram",
                new(2, "Nothing"),
                ContractsJsonContext.Default.GraphImageRequest,
                deadline.Token
            );
            Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        }
    }

    async Task<GraphHistogram> HistogramAsync(string id, GraphImageRequest request, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/graphs/{id}/histogram",
            request,
            ContractsJsonContext.Default.GraphImageRequest,
            token
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphHistogram, token))!;
    }

    async Task<Bitmap> FullAsync(string id, GraphImageRequest request, CancellationToken token) {
        using var response = await http.PostAsJsonAsync(
            $"/api/v1/graphs/{id}/images",
            request,
            ContractsJsonContext.Default.GraphImageRequest,
            token
        );
        response.EnsureSuccessStatusCode();
        var image = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphImage, token))!;
        return PngCodec.Decode(await http.GetByteArrayAsync($"/api/v1/previews/{image.Hash}", token));
    }

    /// <summary>One channel of a picture's bytes as shares of 256 bins; a byte is its own bin.</summary>
    static float[] Shares(Bitmap picture, int lane) {
        var shares = new float[256];
        var texels = picture.Width * picture.Height;
        for (var at = 0; at < texels; at++) {
            shares[picture.Pixels[at * 4 + lane]] += 1f / texels;
        }

        return shares;
    }

    /// <summary>The largest gap between two distributions' running sums.</summary>
    static double Gap(IReadOnlyList<float> first, IReadOnlyList<float> second) {
        double a = 0, b = 0, gap = 0;
        for (var bin = 0; bin < 256; bin++) {
            a += first[bin];
            b += second[bin];
            gap = Math.Max(gap, Math.Abs(a - b));
        }

        return gap;
    }
}
