using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     A texture set's own settings, each change one whole-document step: the resolution, the UV set and how height
///     becomes a normal (ADR 0020).
/// </summary>
static class StackSetEdits {
    /// <summary>The highest coordinate set a model can have: Assimp keeps eight.</summary>
    const int MostUvSets = 8;

    /// <summary>Runs a set's settings command, if this is one.</summary>
    /// <exception cref="StackEditRefusedException">A value is not one a set can have.</exception>
    public static bool Run(StackEditor editor, StackCommand command, Action<IAuthoringCommand> run) {
        switch (command) {
            case StackSetHeightNormal height:
                run(editor.Replaced(Changed(editor, new(HeightNormal: height.Method)), "Height to normal"));
                return true;
            case StackSetSettings settings:
                run(editor.Replaced(Changed(editor, settings), "Texture set settings"));
                return true;
            default:
                return false;
        }
    }

    /// <summary>The document with the set's settings changed as asked, and everything left out as it was.</summary>
    static LayerStackAsset Changed(StackEditor editor, StackSetSettings settings) {
        var document = editor.Document;
        var width = settings.Width ?? document.BaseWidth;
        var height = settings.Height ?? document.BaseHeight;
        // Only a side asked for is held to the list: a stack made at another size keeps it through other changes.
        if ((settings.Width is { } wide && !TextureSizes.Sides.Contains(wide))
            || (settings.Height is { } high && !TextureSizes.Sides.Contains(high))) {
            throw new StackEditRefusedException(
                $"{width}×{height} is no size a texture set has. Each side is one of "
                + $"{string.Join(", ", TextureSizes.Sides)}."
            );
        }

        var set = editor.Set;
        var uvSet = settings.UvSet ?? set.UvSet;
        if (uvSet is < 0 or >= MostUvSets) {
            throw new StackEditRefusedException(
                $"UV set {uvSet + 1} is not one a mesh can have: they run from 1 to {MostUvSets}."
            );
        }

        var changed = set with {
            UvSet = uvSet, HeightNormal = settings.HeightNormal is { } method ? Method(method) : set.HeightNormal
        };
        return document with {
            BaseWidth = width,
            BaseHeight = height,
            Sets = [..document.Sets.Select(held => held == set ? changed : held)]
        };
    }

    static LayerHeightNormal Method(string method) =>
        Enum.TryParse<LayerHeightNormal>(method, false, out var chosen) && Enum.IsDefined(chosen)
            ? chosen
            : throw new StackEditRefusedException(
                $"'{method}' is no way to turn height into a normal. "
                + $"Use {string.Join(" or ", Enum.GetNames<LayerHeightNormal>())}."
            );
}
