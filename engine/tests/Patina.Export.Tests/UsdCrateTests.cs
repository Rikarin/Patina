using CsCheck;
using Patina.Contracts;
using Patina.Export.Materials;
using System.IO.Compression;

namespace Patina.Export.Tests;

/// <summary>
///     USD's binary form (<c>.usdc</c>, #157) read without USD's library. <c>fixtures/brick.usdc</c> is
///     <c>fixtures/brick.usda</c> as usd-core 26.11 writes it (<c>Sdf.Layer.Export</c>), so the two must bring in the
///     same material: a token, an asset, a float inline, a colour held at an offset, vectors of small whole numbers
///     inline, and connections as lists of paths. <c>MaterialToolTests</c> does the same for every shipped preset's
///     material, written binary by USD itself where its Python is there.
/// </summary>
public sealed class UsdCrateTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-crate-");

    public void Dispose() => folder.Delete(true);

    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void A_binary_layer_brings_in_what_its_text_does() {
        var text = Assert.Single(UsdaReader.Read(Fixture("brick.usda")));

        var binary = Assert.Single(UsdaReader.Read(Fixture("brick.usdc")));

        Assert.Equal("Brick", binary.Name);
        Assert.Empty(binary.Warnings);
        Assert.Equal(Describe(text), Describe(binary));
        var slots = binary.Slots.ToDictionary(static s => s.Usage, StringComparer.Ordinal);
        Assert.Equal([0.5f, 0.25f, 0.125f, 1f], slots["emissive"].Value);
        Assert.Equal([0.7f, 0.7f, 0.7f, 1f], slots["opacity"].Value);
        Assert.True(slots["roughness"].Inverted);
        Assert.False(slots["normal"].GreenUp);
        Assert.EndsWith(Path.Combine("textures", "normal.png"), slots["normal"].Texture, StringComparison.Ordinal);
    }

    /// <summary>A binary layer named <c>.usd</c>, or inside a package, is read the same way.</summary>
    [Fact]
    public void A_binary_layer_reads_as_usd_and_in_a_package() {
        var usd = Path.Combine(folder.FullName, "brick.usd");
        File.Copy(Fixture("brick.usdc"), usd);
        var package = Path.Combine(folder.FullName, "brick.usdz");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create)) {
            zip.CreateEntryFromFile(Fixture("brick.usdc"), "brick.usdc", CompressionLevel.NoCompression);
        }

        var expected = Describe(Assert.Single(UsdaReader.Read(Fixture("brick.usda"))), Fixture(string.Empty));

        Assert.Equal(expected, Describe(Assert.Single(UsdaReader.Read(usd)), folder.FullName));
        var unpacked = Path.Combine(folder.FullName, "unpacked");
        Assert.Equal(expected, Describe(Assert.Single(UsdzReader.Read(package, unpacked)), unpacked));
    }

    /// <summary>A file of neither form, or a crate older than USD's compressed tables, is refused in words.</summary>
    [Fact]
    public void A_file_that_is_no_usd_layer_or_too_old_is_refused() {
        var junk = Path.Combine(folder.FullName, "junk.usd");
        File.WriteAllText(junk, "not a layer");
        var old = Path.Combine(folder.FullName, "old.usdc");
        var bytes = File.ReadAllBytes(Fixture("brick.usdc"));
        bytes[9] = 3;
        File.WriteAllBytes(old, bytes);

        Assert.Contains("is not a USD layer", Assert.Throws<InvalidDataException>(() => UsdaReader.Read(junk)).Message);
        Assert.Contains("version 0.3", Assert.Throws<InvalidDataException>(() => UsdaReader.Read(old)).Message);
    }

    /// <summary>
    ///     A damaged file, cut short or with bytes changed, is read or refused with a reason, never anything else: the
    ///     Server reads these, so a file must not throw what it does not expect or read past its end.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void A_damaged_binary_layer_is_read_or_refused() {
        var whole = File.ReadAllBytes(Fixture("brick.usdc"));
        Gen.Select(Gen.Int[16, whole.Length], Gen.Int[0, whole.Length - 1], Gen.Byte)
            .Sample((length, at, value) => {
                    var bytes = whole[..length];
                    if (at < length) {
                        bytes[at] = value;
                    }

                    var damaged = Path.Combine(folder.FullName, $"{Guid.NewGuid():N}.usdc");
                    File.WriteAllBytes(damaged, bytes);
                    try {
                        UsdaReader.Read(damaged);
                    } catch (InvalidDataException) {
                        // Refused with a reason, as a person is told.
                    }
                }
            );
    }

    /// <summary>Each slot of a material, its picture relative to <paramref name="root" />.</summary>
    static string Describe(ImportedMaterial material, string root = "") =>
        string.Join(
            "\n",
            material.Slots.OrderBy(static s => s.Usage, StringComparer.Ordinal)
                .Select(s =>
                    $"{s.Usage} {Relative(s.Texture, root)} {s.Lane} {s.Inverted} {s.GreenUp} "
                    + string.Join(',', s.Value)
                )
        );

    static string? Relative(string? file, string root) =>
        file is null || root.Length == 0 ? file : Path.GetRelativePath(root, file);
}
