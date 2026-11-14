using Patina.Contracts;
using Patina.Export.Builds;
using Patina.Export.Presets;
using Patina.Library;
using Vixen.Core.Imaging;

namespace Patina.Export.Tests;

/// <summary>
///     The bake's own normal and occlusion beside the set's (#155): a set's <c>occlusion</c> is the bake multiplied
///     into the paint, a mixed occlusion, and its <c>normal</c> the combined one, so a mesh-maps export reads
///     <c>bakedOcclusion</c> and <c>bakedNormal</c>, which a stack's maps do not replace.
/// </summary>
public sealed class MeshMapsPresetTests : IDisposable {
    const int Side = 4;
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-mesh-maps-");

    static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => folder.Delete(true);

    static FloatImage Flat(params float[] lanes) {
        var samples = new float[Side * Side * lanes.Length];
        for (var at = 0; at < samples.Length; at++) {
            samples[at] = lanes[at % lanes.Length];
        }

        return new(Side, Side, lanes.Length, samples);
    }

    ExportSource Source(string tool, params (string Map, FloatImage Image)[] maps) {
        var made = Directory.CreateDirectory(Path.Combine(folder.FullName, tool)).FullName;
        List<BuiltMap> built = [
            ..maps.Select(m => MapPictures.Write(
                    made,
                    m.Map,
                    m.Image,
                    false,
                    m.Map == ExportMaps.Normal ? GreenAxis.Up : null
                )
            )
        ];
        return new(made, new(tool, built, [], []), "Body");
    }

    [Fact]
    public void A_bakes_normal_and_occlusion_stay_its_own_under_a_stack_that_paints_both() {
        var bake = Source(
            BakeKeys.Tool,
            (ExportMaps.Occlusion, Flat(0.25f)),
            (ExportMaps.Normal, Flat(0.5f, 0.5f, 1f)),
            (ExportMaps.Curvature, Flat(0.75f))
        );
        var stack = Source(
            BuildTools.LayerStack,
            (ExportMaps.Occlusion, Flat(0.125f)),
            (ExportMaps.Normal, Flat(0.25f, 0.5f, 1f))
        );

        var set = Assert.Single(ExportSteps.SetsOf([bake, stack]));

        float At(string map) => set.Maps[map].Image.Samples[0];
        Assert.Equal((0.125f, 0.25f), (At(ExportMaps.Occlusion), At(ExportMaps.Normal)));
        Assert.Equal((0.25f, 0.5f), (At(ExportMaps.BakedOcclusion), At(ExportMaps.BakedNormal)));
        Assert.Equal(GreenAxis.Up, set.Maps[ExportMaps.BakedNormal].Green);
        Assert.False(set.Maps.ContainsKey("bakedCurvature"));

        // A stack's own map is never taken for the bake's.
        Assert.DoesNotContain(ExportMaps.BakedOcclusion, Assert.Single(ExportSteps.SetsOf([stack])).Maps.Keys);
    }

    [Fact]
    public void The_mesh_maps_preset_writes_the_bakes_own_maps() {
        var preset = ExportPresetCatalog.Read(null).Find("mesh-maps")!;
        var bake = Source(
            BakeKeys.Tool,
            (ExportMaps.Occlusion, Flat(0.25f)),
            (ExportMaps.Normal, Flat(0.5f, 0.25f, 1f)),
            (ExportMaps.Curvature, Flat(0.75f))
        );
        var stack = Source(BuildTools.LayerStack, (ExportMaps.Occlusion, Flat(0.125f)));
        var made = Path.Combine(folder.FullName, "made");

        var manifest = ExportRun.Run(preset, ExportSteps.SetsOf([bake, stack]), made, null, Token);

        float First(string ending, int lane) {
            var file = Assert.Single(manifest.Files, f => f.Template.EndsWith(ending, StringComparison.Ordinal));
            var picture = MapPictures.ReadFile(Path.Combine(made, file.File));
            return picture.Samples[lane];
        }

        Assert.Equal(0.25f, First("_ambient_occlusion", 0), 0.003f);
        Assert.Equal(0.25f, First("_normal_base", 1), 0.003f);
        Assert.Equal(0.75f, First("_curvature", 0), 0.003f);
        // Thickness and the rest were not baked, so their files are not written.
        Assert.DoesNotContain(manifest.Files, static f => f.Template.EndsWith("_thickness", StringComparison.Ordinal));
    }
}
