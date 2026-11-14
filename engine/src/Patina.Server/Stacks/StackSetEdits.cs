using Patina.Contracts;
using Vixen.Authoring;
using Vixen.Texturing.Layers;
using Vixen.Texturing.Painting;

namespace Patina.Server.Stacks;

/// <summary>
///     A texture set's own settings, each change one whole-document step: the resolution, the UV set, how height
///     becomes a normal (ADR 0020) and which channels it has.
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
            case StackAddChannel add:
                run(AddChannel(editor, add.Usage));
                return true;
            case StackRemoveChannel remove:
                run(RemoveChannel(editor, remove.Usage));
                return true;
            case StackSetChannelFormat format:
                run(ChannelFormat(editor, format.Usage, format.Format));
                return true;
            default:
                return false;
        }
    }

    /// <summary>The set with one more channel, which starts as what leaves its material alone.</summary>
    static IAuthoringCommand AddChannel(StackEditor editor, string usage) {
        if (!StackDocuments.Vocabulary.Usages.Any(term => term.Value == usage)) {
            throw new StackEditRefusedException($"'{usage}' is not a channel.");
        }

        var chosen = editor.Set;
        if (chosen.Channels.Exists(channel => channel.Usage == usage)) {
            throw new StackEditRefusedException($"This document has {StackDocuments.ChannelName(usage)} already.");
        }

        ChannelAsset fresh = new() { Usage = usage, Default = StackEditor.DefaultOf(usage) };
        var added = chosen with { Channels = [..chosen.Channels, fresh] };
        return editor.Replaced(
            editor.Document with { Sets = [..editor.Document.Sets.Select(held => held == chosen ? added : held)] },
            $"Add {StackDocuments.ChannelName(usage)}"
        );
    }

    /// <summary>
    ///     The set without one of its channels. Only the set's list changes: a layer's values and paint for it stay, so
    ///     the channel comes back as it was when it is added again.
    /// </summary>
    static IAuthoringCommand RemoveChannel(StackEditor editor, string usage) {
        var chosen = editor.Set;
        var named = new Dictionary<string, string> { ["channel"] = StackDocuments.ChannelName(usage) };
        if (!chosen.Channels.Exists(channel => channel.Usage == usage)) {
            throw new StackEditRefusedException(
                $"This document has no channel '{usage}'.",
                Api.Refusals.ChannelMissing,
                named
            );
        }

        if (chosen.Channels.Count == 1) {
            throw new StackEditRefusedException(
                $"'{usage}' is the texture set's only channel, and a set with none has nothing to write.",
                Api.Refusals.ChannelLast,
                named
            );
        }

        var removed = chosen with { Channels = [..chosen.Channels.Where(channel => channel.Usage != usage)] };
        return editor.Replaced(
            editor.Document with { Sets = [..editor.Document.Sets.Select(held => held == chosen ? removed : held)] },
            $"Remove {StackDocuments.ChannelName(usage)}"
        );
    }

    /// <summary>The set with one channel painted at another texel width (#19).</summary>
    static IAuthoringCommand ChannelFormat(StackEditor editor, string usage, string format) {
        var chosen = editor.Set;
        if (chosen.Channels.Find(channel => channel.Usage == usage) is not { } held) {
            throw new StackEditRefusedException(
                $"This document has no channel '{usage}'.",
                Api.Refusals.ChannelMissing,
                new Dictionary<string, string> { ["channel"] = StackDocuments.ChannelName(usage) }
            );
        }

        if (!Enum.TryParse<PaintTexelFormat>(format, false, out var width) || !Enum.IsDefined(width)) {
            throw new StackEditRefusedException(
                $"'{format}' is no channel depth. Use {string.Join(", ", Enum.GetNames<PaintTexelFormat>())}.",
                Api.Refusals.ChannelFormatUnknown,
                new Dictionary<string, string> { ["format"] = format }
            );
        }

        var changed = chosen with {
            Channels = [
                ..chosen.Channels.Select(channel => channel == held ? channel with { Format = width } : channel)
            ]
        };
        return editor.Replaced(
            editor.Document with { Sets = [..editor.Document.Sets.Select(set => set == chosen ? changed : set)] },
            "Channel depth"
        );
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
            UvSet = uvSet,
            HeightNormal = settings.HeightNormal is { } method ? Method(method) : set.HeightNormal,
            BakedNormal = settings.BakedNormal is { } normal ? Mix(normal) : set.BakedNormal,
            BakedOcclusion = settings.BakedOcclusion is { } occlusion ? Mix(occlusion) : set.BakedOcclusion,
            Description = settings.Description?.Trim() ?? set.Description
        };
        return document with {
            BaseWidth = width,
            BaseHeight = height,
            Sets = [..document.Sets.Select(held => held == set ? changed : held)]
        };
    }

    static LayerBakeMix Mix(string mix) =>
        Enum.TryParse<LayerBakeMix>(mix, false, out var chosen) && Enum.IsDefined(chosen)
            ? chosen
            : throw new StackEditRefusedException(
                $"'{mix}' is no way for a bake to reach a texture set. "
                + $"Use {string.Join(", ", Enum.GetNames<LayerBakeMix>())}."
            );

    static LayerHeightNormal Method(string method) =>
        Enum.TryParse<LayerHeightNormal>(method, false, out var chosen) && Enum.IsDefined(chosen)
            ? chosen
            : throw new StackEditRefusedException(
                $"'{method}' is no way to turn height into a normal. "
                + $"Use {string.Join(" or ", Enum.GetNames<LayerHeightNormal>())}."
            );
}
