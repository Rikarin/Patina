using Patina.Export.Presets;

namespace Patina.Export.Tests;

/// <summary>A hand-written preset edited in the app keeps its comments above its fields and its textures (#155).</summary>
public sealed class PresetCommentsTests : IDisposable {
    readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("patina-preset-comments-");

    public void Dispose() => folder.Delete(true);

    const string HandWritten = """
                               # Our studio's mobile preset.
                               # Ask Ana before changing the normal.
                               version: 1
                               id: mobile
                               name: Mobile
                               # The phones read green down.
                               normal: y-down
                               textures:
                                 # Colour first, alpha unused.
                                 - file: "{set}_albedo"
                                   format: png8
                                   # This one is lost: it is inside a texture.
                                   space: srgb
                                   channels: [baseColor.r, baseColor.g, baseColor.b]
                                 # Packed for the shader.
                                 - file: '{set}_mask'
                                   format: png8
                                   space: linear
                                   channels: [occlusion.r, roughness.r, metalness.r]
                               """;

    [Fact]
    public void An_edit_keeps_the_comments_above_fields_and_textures() {
        File.WriteAllText(Path.Combine(folder.FullName, "mobile.yaml"), HandWritten);
        var document = ExportPresetWriter.DocumentOf(ExportPresetReader.Read(HandWritten, "mobile")) with {
            Name = "Mobile, smaller"
        };

        var text = File.ReadAllText(ExportPresetWriter.Save(folder.FullName, document));

        Assert.StartsWith("# Our studio's mobile preset.\n# Ask Ana before changing the normal.\nversion: 1\n", text);
        Assert.Contains("# The phones read green down.\nnormal: y-down\n", text);
        Assert.Contains("  # Colour first, alpha unused.\n  - file: \"{set}_albedo\"\n", text);
        Assert.Contains("  # Packed for the shader.\n  - file: \"{set}_mask\"\n", text);
        Assert.DoesNotContain("This one is lost", text);
        Assert.Equal("Mobile, smaller", ExportPresetReader.Read(text, "mobile").Name);
    }

    [Fact]
    public void A_rename_keeps_the_comments_of_the_file_it_renames() {
        File.WriteAllText(Path.Combine(folder.FullName, "mobile.yaml"), HandWritten);
        var document = ExportPresetWriter.DocumentOf(ExportPresetReader.Read(HandWritten, "mobile")) with {
            Id = "phone"
        };

        var text = File.ReadAllText(ExportPresetWriter.Save(folder.FullName, document, "mobile"));

        Assert.StartsWith("# Our studio's mobile preset.", text);
        Assert.False(File.Exists(Path.Combine(folder.FullName, "mobile.yaml")));
    }

    [Fact]
    public void A_new_preset_starts_with_the_writers_own_line() {
        var document = ExportPresetWriter.DocumentOf(ExportPresetReader.Read(HandWritten, "mobile"));

        var text = File.ReadAllText(ExportPresetWriter.Save(folder.FullName, document));

        Assert.StartsWith("# An export preset", text);
        Assert.True(PresetComments.Of("version: 1\n").Empty);
    }
}
