using Patina.Contracts;
using Patina.Library;
using Vixen.Texturing.Layers;

namespace Patina.Server.Stacks;

/// <summary>
///     Apply Channels to All Sets (#141): each of the model's other texture sets gains the channels one set has and it
///     lacks, each starting as Add Channel starts one, from what leaves its material alone. Sets of one model are the
///     stacks of one project, which sit together. Nothing is taken off a set, so a set's own extra channels stay.
/// </summary>
/// <remarks>
///     ⚠ <b>An open set is changed through its session and a closed one through its file.</b> Writing the file of an
///     open set would be overwritten by its next save, and opening a closed one for this would leave a session nobody
///     asked for. A channel touches no paint, so the file's text is all that changes.
/// </remarks>
static class StackChannelSpread {
    /// <summary>Gives the other sets of a session's model its channels.</summary>
    /// <param name="from">The set whose channels go to the others.</param>
    /// <param name="sessions">The open stacks.</param>
    /// <returns>What each other set was given.</returns>
    public static StackChannelsApplied Spread(StackSession from, StackSessions sessions) {
        var usages = from.Published?.Channels.Select(static channel => channel.Usage).ToList() ?? [];
        var label = $"Add channels from {from.Published?.Name}";
        List<StackChannelsSpread> done = [];
        HashSet<string> open = new(StringComparer.Ordinal) { from.Path };
        foreach (var session in sessions.All) {
            if (session == from || session.Folder != from.Folder) {
                continue;
            }

            open.Add(session.Path);
            done.Add(InSession(session, usages, label));
        }

        if (from.Folder is { } folder && Directory.Exists(folder)) {
            var files = Directory.EnumerateFiles(folder, $"*{StackDocuments.Extension}").Order(StringComparer.Ordinal);
            foreach (var file in files.Except(open, StringComparer.Ordinal)) {
                done.Add(InFile(file, usages));
            }
        }

        return new(done);
    }

    /// <summary>The channels of these that a set lacks, in the order the giving set has them.</summary>
    static List<string> Missing(TextureSetAsset set, List<string> usages) =>
        [..usages.Where(usage => set.Channels.TrueForAll(channel => channel.Usage != usage))];

    static StackChannelsSpread InSession(StackSession session, List<string> usages, string label) {
        var set = session.Published;
        if (set is null) {
            return new(string.Empty, session.Path, [], true, "It has no texture set.");
        }

        var missing = Missing(set, usages);
        if (missing.Count == 0) {
            return new(set.Name, session.Path, [], true);
        }

        try {
            session.Apply(new([..missing.Select(static usage => (StackCommand)new StackAddChannel(usage))], label));
            return new(set.Name, session.Path, missing, true);
        } catch (StackEditRefusedException e) {
            return new(set.Name, session.Path, [], true, e.Message);
        }
    }

    static StackChannelsSpread InFile(string file, List<string> usages) {
        LayerStackAsset document;
        try {
            document = StackDocuments.Parse(SharedFile.ReadAllText(file));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or StackEditRefusedException) {
            return new(string.Empty, file, [], false, e.Message);
        }

        if (LayerStackEdit.SetFor(document, string.Empty) is not { } set) {
            return new(string.Empty, file, [], false, "It has no texture set.");
        }

        var missing = Missing(set, usages);
        if (missing.Count == 0) {
            return new(set.Name, file, [], false);
        }

        var added = set with {
            Channels = [
                ..set.Channels,
                ..missing.Select(static usage => new ChannelAsset {
                        Usage = usage, Default = StackEditor.DefaultOf(usage)
                    }
                )
            ]
        };
        try {
            LibraryFiles.Replace(
                file,
                StackDocuments.Text(
                    document with { Sets = [..document.Sets.Select(held => held == set ? added : held)] }
                )
            );
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            return new(set.Name, file, [], false, e.Message);
        }

        return new(set.Name, file, missing, false);
    }
}
