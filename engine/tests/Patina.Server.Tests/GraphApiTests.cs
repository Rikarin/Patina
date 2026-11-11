using Patina.Contracts;
using Patina.Library;
using Patina.Server.Graphs;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Vixen.NodeGraph;
using Vixen.TextureGraph;

namespace Patina.Server.Tests;

/// <summary>Editing a graph through the API with no Worker: the Server alone owns the document.</summary>
public sealed partial class GraphApiTests : IDisposable {
    readonly ServerFixture server = new();

    CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => server.Dispose();

    async Task<(HttpClient Client, GraphState State)> OpenAsync() {
        var client = server.AuthorisedClient();
        return (client, await client.OpenAsync(GraphTesting.Write(server.Library, GraphTesting.Sample()), Token));
    }

    [Fact]
    public async Task Opening_a_graph_reads_its_nodes_edges_and_parameters() {
        var (client, state) = await OpenAsync();
        using (client) {
            Assert.Equal("Sample", state.Name);
            Assert.Equal([1, 2, 3, 4, 5], state.Nodes.Select(static node => node.Id));
            Assert.Equal(4, state.Edges.Count);
            Assert.Equal(4f, Assert.Single(state.Nodes[0].Values["Scale"]));
            Assert.Equal("baseColor", state.Nodes[3].Texts["Usage"]);
            Assert.Equal("7", state.Settings["seed"]);
            var wear = Assert.Single(state.Parameters);
            Assert.Equal(("Wear", "Float", 0f, 1f), (wear.Name, wear.Kind, wear.Minimum, wear.Maximum));
            Assert.False(state.History.CanUndo);
            Assert.False(state.History.Dirty);
        }
    }

    [Fact]
    public async Task Parameters_are_added_edited_and_removed_and_a_bad_default_is_refused() {
        var (client, state) = await OpenAsync();
        using (client) {
            GraphSettingDefinition grit = new("Grit", "0.25", "How rough", "Float", 0f, 1f, "Look", []);
            await client.EditAsync(state.Id, Token, new GraphAddParameter(grit));
            await client.EditAsync(
                state.Id,
                Token,
                new GraphSetParameter("Wear", grit with { Name = "Wear", Default = "1" })
            );
            var edited = await client.StateAsync(state.Id, Token);
            Assert.Equal(["Wear", "Grit"], edited.Parameters.Select(static p => p.Name));
            Assert.Equal("1", edited.Parameters[0].Default);

            using var outside = await client.SendAsync(
                state.Id,
                new([new GraphSetParameter("Grit", grit with { Default = "2" })]),
                Token
            );
            Assert.Equal(HttpStatusCode.UnprocessableEntity, outside.StatusCode);
            using var twice = await client.SendAsync(state.Id, new([new GraphAddParameter(grit)]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, twice.StatusCode);

            await client.EditAsync(state.Id, Token, new GraphRemoveParameter("Grit"));
            Assert.Equal("Wear", Assert.Single((await client.StateAsync(state.Id, Token)).Parameters).Name);
            await client.ActAsync(state.Id, "undo", Token);
            Assert.Equal(2, (await client.StateAsync(state.Id, Token)).Parameters.Count);
        }
    }

    [Fact]
    public async Task Groups_and_notes_are_in_the_state_the_deltas_and_the_file() {
        var (client, state) = await OpenAsync();
        using (client) {
            Assert.Equal("Base", Assert.Single(state.Groups).Title);
            Assert.Equal("Rust goes here", Assert.Single(state.Comments).Text);

            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddGroup("Wear", [3, 4]),
                new GraphSetGroup(state.Groups[0].Id, Nodes: [2]),
                new GraphAddComment("Later", 5, 6),
                new GraphSetComment(state.Comments[0].Id, "Rust", Width: 320)
            );
            var events = await client.EventsAsync(state.Id, state.Sequence, Token);
            var delta = Assert.Single(events.Events).Delta!;
            Assert.Equal(["Base", "Wear"], delta.Groups!.Select(static g => g.Title));
            Assert.Equal([2], delta.Groups![0].Nodes);
            Assert.Equal(["Rust", "Later"], delta.Comments!.Select(static c => c.Text));
            Assert.Null(delta.Parameters);

            await client.ActAsync(state.Id, "save", Token);
            var written = GraphDocuments.Parse(await File.ReadAllTextAsync(state.Path, Token));
            Assert.Equal(2, written.Groups.Length);
            Assert.Equal(320f, written.Comments[0].Width);

            await client.EditAsync(state.Id, Token, new GraphRemoveNodes([2]));
            Assert.Empty((await client.StateAsync(state.Id, Token)).Groups[0].Nodes);
            await client.ActAsync(state.Id, "undo", Token);
            Assert.Equal([2], (await client.StateAsync(state.Id, Token)).Groups[0].Nodes);

            using var missing = await client.SendAsync(state.Id, new([new GraphRemoveGroup(9)]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        }
    }

    [Fact]
    public async Task A_number_takes_an_expression_and_a_colour_does_not() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetText(2, "=Hue", "Wear * 2"));
            Assert.Equal("Wear * 2", (await client.StateAsync(state.Id, Token)).Nodes[1].Texts["=Hue"]);
            using var missing = await client.SendAsync(state.Id, new([new GraphSetText(2, "=Nothing", "1")]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        }
    }

    [Fact]
    public async Task A_group_and_a_note_are_named_by_identity_so_a_stale_client_cannot_hit_the_wrong_one() {
        var (client, state) = await OpenAsync();
        using (client) {
            var first = state.Groups[0].Id;
            var made = await client.EditAsync(
                state.Id,
                Token,
                new GraphAddGroup("Second", [3], -1),
                new GraphAddComment("Second", 0, 0, Ref: -2)
            );
            var second = made.Created.Single(static c => c.Ref == -1).Node;
            var note = made.Created.Single(static c => c.Ref == -2).Node;
            Assert.NotEqual(first, second);

            // A second client takes the first group away, so the one after it moves up the list.
            await client.EditAsync(state.Id, Token, new GraphRemoveGroup(first));
            await client.EditAsync(state.Id, Token, new GraphRemoveComment(state.Comments[0].Id));

            await client.EditAsync(state.Id, Token, new GraphSetGroup(second, "Still second"));
            await client.EditAsync(state.Id, Token, new GraphSetComment(note, "Still second"));
            var after = await client.StateAsync(state.Id, Token);
            Assert.Equal((second, "Still second"), (Assert.Single(after.Groups).Id, after.Groups[0].Title));
            Assert.Equal((note, "Still second"), (Assert.Single(after.Comments).Id, after.Comments[0].Text));

            using var stale = await client.SendAsync(state.Id, new([new GraphSetGroup(first, "Gone")]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
            using var wrongKind = await client.SendAsync(state.Id, new([new GraphRemoveComment(second)]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongKind.StatusCode);

            await client.ActAsync(state.Id, "save", Token);
            var reopened = await client.OpenAsync(
                GraphTesting.Write(
                    server.Library,
                    GraphDocuments.Parse(await File.ReadAllTextAsync(after.Path, Token)),
                    "again.vxtexgraph"
                ),
                Token
            );
            Assert.Equal(second, Assert.Single(reopened.Groups).Id);
        }
    }

    [Theory]
    [InlineData("32", true)]
    [InlineData("16", true)]
    [InlineData("24", false)]
    public async Task A_graph_takes_a_bit_depth_of_16_or_32(string depth, bool accepted) {
        var (client, state) = await OpenAsync();
        using (client) {
            using var response = await client.SendAsync(
                state.Id,
                new([new GraphSetSetting(TextureGraphSettings.BitDepth, depth)]),
                Token
            );
            Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(accepted, (await client.StateAsync(state.Id, Token)).Settings.ContainsKey("bitDepth"));
        }
    }

    [Theory]
    [InlineData(TextureGraphSettings.TilingX, "false", true)]
    [InlineData(TextureGraphSettings.TilingY, "true", true)]
    [InlineData(TextureGraphSettings.TilingX, "1", false)]
    public async Task A_graph_says_whether_it_tiles_along_each_axis(string key, string value, bool accepted) {
        var (client, state) = await OpenAsync();
        using (client) {
            using var response = await client.SendAsync(state.Id, new([new GraphSetSetting(key, value)]), Token);
            Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(accepted, (await client.StateAsync(state.Id, Token)).Settings.ContainsKey(key));
        }
    }

    async Task<GraphState> OpenCompoundAsync(HttpClient client) {
        var compounds = Directory.CreateDirectory(Path.Combine(server.Library, LibraryLayout.CompoundsFolder));
        return await client.OpenAsync(
            GraphTesting.Write(compounds.FullName, GraphTesting.Sample(), "Mine.vxtexgraph"),
            Token
        );
    }

    [Fact]
    public async Task An_interface_is_authored_with_commands_and_its_boundary_nodes_show_it() {
        using var client = server.AuthorisedClient();
        var state = await OpenCompoundAsync(client);
        var made = await client.EditAsync(
            state.Id,
            Token,
            new GraphAddPort(new("Mask", "Input", "Image", [], "What to wear")),
            new GraphAddPort(new("Amount", "Input", "Float", [0.5f], string.Empty)),
            new GraphAddPort(new("Result", "Output", "Image", [], string.Empty)),
            new GraphAddNode(SubGraphs.InputType, 0, 0, -1),
            new GraphAddNode(SubGraphs.OutputType, 900, 0, -2),
            new GraphConnect(-1, "Mask", 2, "Input"),
            new GraphConnect(3, "Out", -2, "Result")
        );
        var entry = made.Created.Single(static c => c.Ref == -1).Node;
        var exit = made.Created.Single(static c => c.Ref == -2).Node;
        var events = await client.EventsAsync(state.Id, state.Sequence, Token);
        Assert.Equal(["Mask", "Amount", "Result"], events.Events[^1].Delta!.Interface!.Select(static p => p.Name));

        var types = (await client.GetFromJsonAsync(
            $"/api/v1/graphs/{state.Id}/node-types",
            ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
            Token
        ))!;
        Assert.Equal(
            ["Mask", "Amount"],
            types.Single(static t => t.Path == SubGraphs.InputType).Ports.Select(static p => p.Name)
        );

        await client.EditAsync(
            state.Id,
            Token,
            new GraphSetPort("Mask", "Input", new("Wear", "Input", "Image", [], string.Empty)),
            new GraphSetPort("Result", "Output", new("Worn", "Output", "Image", [], string.Empty))
        );
        var renamed = await client.StateAsync(state.Id, Token);
        Assert.Contains(renamed.Edges, e => e.FromNode == entry && e.FromPort == "Wear" && e.ToNode == 2);
        Assert.Contains(renamed.Edges, e => e.ToNode == exit && e.ToPort == "Worn");

        await client.ActAsync(state.Id, "undo", Token);
        Assert.Contains((await client.StateAsync(state.Id, Token)).Edges, static e => e.FromPort == "Mask");
        await client.ActAsync(state.Id, "redo", Token);

        await client.EditAsync(state.Id, Token, new GraphRemovePort("Wear", "Input"));
        var removed = await client.StateAsync(state.Id, Token);
        Assert.DoesNotContain(removed.Edges, e => e.FromNode == entry);
        Assert.DoesNotContain(removed.Interface, static p => p.Name == "Wear");
        await client.ActAsync(state.Id, "undo", Token);
        Assert.Contains(
            (await client.StateAsync(state.Id, Token)).Edges,
            e => e.FromNode == entry && e.FromPort == "Wear"
        );

        foreach (var refused in new GraphCommand[] {
                     new GraphAddPort(new("Wear", "Input", "Image", [], string.Empty)),
                     new GraphAddPort(new(string.Empty, "Input", "Image", [], string.Empty)),
                     new GraphAddPort(new("Bad", "Sideways", "Image", [], string.Empty)),
                     new GraphAddPort(new("Bad", "Input", "Nothing", [], string.Empty)),
                     new GraphAddPort(new("Bad", "Input", "Float", [1f, 2f], string.Empty)),
                     new GraphSetPort("Wear", "Output", new("Wear", "Output", "Image", [], string.Empty)),
                     new GraphSetPort("Wear", "Input", new("Wear", "Output", "Image", [], string.Empty)),
                     new GraphSetPort("Wear", "Input", new("Wear", "Input", "Float", [], string.Empty)),
                     new GraphRemovePort("Nothing", "Input")
                 }) {
            using var response = await client.SendAsync(state.Id, new([refused]), Token);
            Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, refused.ToString());
        }

        await client.ActAsync(state.Id, "save", Token);
        Assert.Contains(
            GraphDocuments.Parse(await File.ReadAllTextAsync(state.Path, Token)).Interface,
            static p => p.Name == "Wear"
        );
    }

    [Fact]
    public async Task A_graph_that_is_no_compound_lists_no_boundary_nodes() {
        var (client, state) = await OpenAsync();
        using (client) {
            var types = (await client.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/node-types",
                ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
                Token
            ))!;
            Assert.DoesNotContain(types, static t => SubGraphs.IsBoundary(t.Path));
        }
    }

    async Task<GraphExtraction> ExtractAsync(HttpClient client, string id, GraphExtractRequest request) {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/graphs/{id}/compounds",
            request,
            ContractsJsonContext.Default.GraphExtractRequest,
            Token
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphExtraction, Token))!;
    }

    [Fact]
    public async Task A_selection_saved_as_a_compound_is_one_undo_and_the_file_stays() {
        var (client, state) = await OpenAsync();
        using (client) {
            var other = await client.OpenAsync(
                GraphTesting.Write(server.Library, GraphTesting.Sample(), "other.vxtexgraph"),
                Token
            );
            var before = await client.StateAsync(state.Id, Token);

            var saved = await ExtractAsync(client, state.Id, new("Wear/Tinted", [2, 3], 400, 50));

            Assert.False(saved.Compound.ReadOnly);
            Assert.Equal("Wear/Tinted", saved.Compound.Type);
            Assert.Equal(
                Path.Combine(server.Library, "Compounds", "Wear", "Tinted.vxtexgraph"),
                saved.Compound.Path
            );
            var compound = GraphDocuments.Parse(await File.ReadAllTextAsync(saved.Compound.Path, Token));
            Assert.Equal("Tinted", compound.Name);
            Assert.Equal(
                ["Colour/HSL", "Colour/Invert", "Sub-graph/Input", "Sub-graph/Output"],
                compound.Nodes.Select(static n => n.Type).Order(StringComparer.Ordinal)
            );
            Assert.Equal(2, compound.Interface.Length);

            var after = await client.StateAsync(state.Id, Token);
            Assert.DoesNotContain(after.Nodes, static n => n.Id is 2 or 3);
            Assert.Contains(after.Nodes, n => n.Id == saved.Node && n.Type == "Wear/Tinted");
            Assert.Equal(2, after.Edges.Count(e => e.ToNode == saved.Node || e.FromNode == saved.Node));

            var events = (await client.EventsAsync(state.Id, state.Sequence, Token)).Events;
            Assert.Contains(events, static e => e.Kind == GraphEventKind.TypesChanged);
            Assert.Contains(events, static e => e.Kind == GraphEventKind.Edited && e.Delta!.RemovedNodes.Count == 2);
            Assert.Contains(
                (await client.EventsAsync(other.Id, other.Sequence, Token)).Events,
                static e => e.Kind == GraphEventKind.TypesChanged
            );
            Assert.Contains(await client.CompoundsAsync(state.Id, Token), static c => c.Type == "Wear/Tinted");

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            Assert.Equal(before.Nodes.Select(static n => n.Id), undone.Nodes.Select(static n => n.Id));
            Assert.Equal(before.Edges, undone.Edges);
            Assert.Equal(before.Groups[0].Nodes, undone.Groups[0].Nodes);
            Assert.True(File.Exists(saved.Compound.Path));

            await client.ActAsync(state.Id, "redo", Token);
            Assert.Contains((await client.StateAsync(state.Id, Token)).Nodes, static n => n.Type == "Wear/Tinted");
        }
    }

    [Theory]
    [InlineData("Wear/Tinted", 1)]
    [InlineData("Patterns/Cells", 1)]
    [InlineData("bad:name", 1)]
    [InlineData("../outside", 1)]
    [InlineData("Fresh", 99)]
    public async Task A_selection_is_not_saved_over_anything_or_outside_its_folder(string name, int node) {
        var (client, state) = await OpenAsync();
        using (client) {
            await ExtractAsync(client, state.Id, new("Wear/Tinted", [3], 0, 0));
            var before = await client.StateAsync(state.Id, Token);
            using var response = await client.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/compounds",
                new(name, [node], 0, 0),
                ContractsJsonContext.Default.GraphExtractRequest,
                Token
            );
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var after = await client.StateAsync(state.Id, Token);
            Assert.Equal(before.Revision, after.Revision);
            Assert.False(File.Exists(Path.Combine(server.Library, "Compounds", "Fresh.vxtexgraph")));
            Assert.False(File.Exists(Path.Combine(server.Library, "outside.vxtexgraph")));
        }
    }

    [Fact]
    public async Task A_boundary_node_cannot_leave_its_graph_and_a_shipped_compound_saves_nothing() {
        using var client = server.AuthorisedClient();
        var state = await OpenCompoundAsync(client);
        var made = await client.EditAsync(state.Id, Token, new GraphAddNode(SubGraphs.InputType, 0, 0, -1));
        using var boundary = await client.PostAsJsonAsync(
            $"/api/v1/graphs/{state.Id}/compounds",
            new("Fresh", [made.Created[0].Node], 0, 0),
            ContractsJsonContext.Default.GraphExtractRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, boundary.StatusCode);

        var cells = await client.OpenAsync("shipped:Patterns/Cells", Token);
        using var shipped = await client.PostAsJsonAsync(
            $"/api/v1/graphs/{cells.Id}/compounds",
            new("Fresh", [cells.Nodes[0].Id], 0, 0),
            ContractsJsonContext.Default.GraphExtractRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, shipped.StatusCode);
    }

    Task<HttpResponseMessage> CopyAsync(HttpClient client, GraphCompoundCopy copy) =>
        client.PostAsJsonAsync(
            "/api/v1/library/compounds",
            copy,
            ContractsJsonContext.Default.GraphCompoundCopy,
            Token
        );

    [Fact]
    public async Task A_shipped_compound_copied_into_the_library_opens_to_be_edited_and_saved() {
        LibraryFiles.CreateLibrary(
            server.Library,
            new(LibraryId.New(), "Ember", 1024, ["baseColor"], "pbr", ["default"])
        );
        LibraryFiles.WriteProject(server.Library, "crate", new(ProjectId.New(), "Crate", [], [], []));
        var shared = Path.GetDirectoryName(LibraryFiles.ProjectFile(server.Library, "crate"))!;
        var client = server.AuthorisedClient();
        using (client) {
            var host = await client.OpenAsync(
                GraphTesting.Write(shared, GraphTesting.Sample(), "host.vxtexgraph"),
                Token
            );
            using var refused = await CopyAsync(client, new("Patterns/Cells"));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            using var response = await CopyAsync(client, new("Patterns/Cells", Graph: host.Id));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var copy = (await response.Content.ReadFromJsonAsync(ContractsJsonContext.Default.GraphCompound, Token))!;
            Assert.Equal("Patterns/Cells Copy", copy.Type);
            Assert.False(copy.ReadOnly);

            Assert.Contains(
                (await client.EventsAsync(host.Id, host.Sequence, Token)).Events,
                static e => e.Kind == GraphEventKind.TypesChanged
            );
            Assert.Contains(await client.CompoundsAsync(host.Id, Token), static c => c.Type == "Patterns/Cells Copy");

            var opened = await client.OpenAsync(copy.Path, Token);
            Assert.False(opened.ReadOnly);
            Assert.Equal("Cells Copy", opened.Name);
            Assert.NotEmpty(opened.Interface);
            await client.EditAsync(opened.Id, Token, new GraphAddComment("Mine now", 0, 0));
            await client.ActAsync(opened.Id, "save", Token);

            using var again = await CopyAsync(client, new("Patterns/Cells", Graph: host.Id));
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            using var shadow = await CopyAsync(client, new("Patterns/Cells", "Patterns/Cells", host.Id));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, shadow.StatusCode);
            using var missing = await CopyAsync(client, new("Patterns/Nothing", Graph: host.Id));
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var illegal = await CopyAsync(client, new("Patterns/Cells", "bad:name", host.Id));
            Assert.Equal(HttpStatusCode.BadRequest, illegal.StatusCode);
            using var closed = await CopyAsync(client, new("Patterns/Cells", "Mine", "nowhere"));
            Assert.Equal(HttpStatusCode.NotFound, closed.StatusCode);

            using var beside = await CopyAsync(client, new("Patterns/Cells", "Beside", host.Id));
            Assert.Equal(HttpStatusCode.Created, beside.StatusCode);
            Assert.True(File.Exists(Path.Combine(shared, "Compounds", "Beside.vxtexgraph")));
        }
    }

    [Fact]
    public async Task A_compound_folder_is_read_and_a_sibling_graph_is_not_a_node() {
        var compounds = Directory.CreateDirectory(Path.Combine(server.Library, LibraryLayout.CompoundsFolder));
        GraphTesting.Write(compounds.FullName, GraphTesting.Sample(), "Mine.vxtexgraph");
        GraphTesting.Write(server.Library, GraphTesting.Sample(), "sibling.vxtexgraph");
        var (client, state) = await OpenAsync();
        using (client) {
            var listed = await client.CompoundsAsync(state.Id, Token);
            var mine = listed.Single(static c => c.Type == "Mine");
            Assert.False(mine.ReadOnly);
            Assert.DoesNotContain(listed, static c => c.Type == "sibling");
            Assert.Contains(listed, static c => c.ReadOnly);
            var inner = await client.OpenAsync(mine.Path, Token);
            Assert.Equal(Path.GetFullPath(mine.Path), inner.Path);
            Assert.Equal(LibraryLayout.CompoundsFolder, TextureCompoundLibrary.Folder);
        }
    }

    [Fact]
    public async Task A_shipped_compound_opens_to_be_read_and_keeps_its_boundary_ports() {
        var (client, state) = await OpenAsync();
        using (client) {
            var cells = (await client.CompoundsAsync(state.Id, Token)).Single(static c => c.Type == "Patterns/Cells");
            var opened = await client.OpenAsync(cells.Path, Token);
            Assert.True(opened.ReadOnly);
            Assert.Equal((await client.OpenAsync(cells.Path, Token)).Id, opened.Id);

            using var edit = await client.SendAsync(opened.Id, new([new GraphAddNode("Source/Noise", 0, 0)]), Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
            using var save = await client.PostAsync($"/api/v1/graphs/{opened.Id}/save", null, Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
        }
    }

    [Fact]
    public async Task Saving_a_compound_tells_the_graphs_that_read_its_folder() {
        var compounds = Directory.CreateDirectory(Path.Combine(server.Library, LibraryLayout.CompoundsFolder));
        var file = GraphTesting.Write(compounds.FullName, GraphTesting.Sample(), "Mine.vxtexgraph");
        var (client, state) = await OpenAsync();
        using (client) {
            var inner = await client.OpenAsync(file, Token);
            await client.EditAsync(inner.Id, Token, new GraphSetValue(1, "Scale", [9f]));
            await client.ActAsync(inner.Id, "save", Token);

            var events = await client.EventsAsync(state.Id, state.Sequence, Token);
            Assert.Contains(events.Events, static e => e.Kind == GraphEventKind.TypesChanged);
        }
    }

    [Fact]
    public async Task A_full_size_image_needs_a_worker_that_has_the_graph() {
        var (client, state) = await OpenAsync();
        using (client) {
            using var response = await client.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/images",
                new(1, "Out"),
                ContractsJsonContext.Default.GraphImageRequest,
                Token
            );
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }

    [Fact]
    public async Task Opening_the_same_file_twice_joins_one_session() {
        var (client, state) = await OpenAsync();
        using (client) {
            var again = await client.OpenAsync(state.Path, Token);
            Assert.Equal(state.Id, again.Id);
            var relative = await client.OpenAsync(Path.GetFileName(state.Path), Token);
            Assert.Equal(state.Id, relative.Id);
        }
    }

    [Fact]
    public async Task A_file_that_is_missing_or_not_a_graph_is_refused() {
        using var client = server.AuthorisedClient();
        using var missing = await client.PostAsJsonAsync(
            "/api/v1/graphs",
            new(Path.Combine(server.Library, "nothing.vxtexgraph")),
            ContractsJsonContext.Default.GraphOpenRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var bad = Path.Combine(server.Library, "bad.vxtexgraph");
        await File.WriteAllTextAsync(bad, "nodes: [ this is not", Token);
        using var unreadable = await client.PostAsJsonAsync(
            "/api/v1/graphs",
            new(bad),
            ContractsJsonContext.Default.GraphOpenRequest,
            Token
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unreadable.StatusCode);
    }

    [Fact]
    public async Task The_node_types_describe_a_palette() {
        var (client, state) = await OpenAsync();
        using (client) {
            var types = (await client.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/node-types",
                ContractsJsonContext.Default.IReadOnlyListGraphNodeType,
                Token
            ))!;

            Assert.True(types.Count >= 49, $"{types.Count} types");
            var blur = types.Single(static type => type.Path == "Filters/Blur");
            Assert.Equal(("Blur", "Filters"), (blur.Title, blur.Category));
            Assert.Equal("Image", blur.Ports.Single(static port => port.Name == "Input").Kind);
            Assert.Equal(
                ("Input", "Float"),
                (blur.Ports.Single(static p => p.Name == "Radius").Direction,
                    blur.Ports.Single(static p => p.Name == "Radius").Kind)
            );
            Assert.Equal(8f, Assert.Single(blur.Ports.Single(static p => p.Name == "Radius").Default));
            var radius = blur.Ports.Single(static p => p.Name == "Radius");
            Assert.Equal((0f, null), (radius.Range?.Minimum, radius.Range?.Maximum));
            var hue = types.Single(static type => type.Path == "Colour/HSL").Ports.Single(static p => p.Name == "Hue");
            Assert.Equal((null, -0.5f, 0.5f), (hue.Range?.Minimum, hue.Range?.SoftMinimum, hue.Range?.SoftMaximum));
            Assert.Null(blur.Ports.Single(static p => p.Name == "Input").Range);
            var blend = types.Single(static type => type.Path == "Colour/Blend");
            Assert.Contains("Multiply", blend.Settings.Single(static s => s.Name == "Mode").Accepted);
            Assert.Contains(types, static type => type.Path == "Patterns/Cells");
        }
    }

    [Fact]
    public async Task A_batch_adds_and_wires_nodes_and_names_them_for_the_client() {
        var (client, state) = await OpenAsync();
        using (client) {
            var result = await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 10, 20, -1),
                new GraphConnect(1, "Out", -1, "Input"),
                new GraphSetValue(-1, "Red", [0f])
            );

            var created = Assert.Single(result.Created);
            Assert.Equal(-1, created.Ref);
            Assert.Equal(6, created.Node);
            var after = await client.StateAsync(state.Id, Token);
            Assert.Contains(new(1, "Out", 6, "Input"), after.Edges);
            var node = after.Nodes.Single(static node => node.Id == 6);
            Assert.Equal((10f, 20f), (node.X, node.Y));
            Assert.Equal(state.Revision + 1, after.Revision);
            Assert.True(after.History.Dirty);
        }
    }

    [Fact]
    public async Task The_history_lists_its_steps_and_jumps_to_one_as_a_single_event() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.25f]));
            await client.EditAsync(state.Id, Token, new GraphSetSetting("seed", "9"));
            var third = await client.EditAsync(state.Id, Token, new GraphMoveNodes([new(1, 50, 60)]));
            var listed = (await client.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/history",
                ContractsJsonContext.Default.HistoryList,
                Token
            ))!;
            Assert.Equal(3, listed.Done.Count);
            Assert.Empty(listed.Undone);
            Assert.Equal(third.History.Step, listed.Done[^1].Id);
            Assert.Equal(third.History.UndoLabel, listed.Done[^1].Label);

            // Back to the first step: two undone, and followers hear one event.
            var jumped = await client.JumpAsync(state.Id, listed.Done[0].Id, Token);
            Assert.Equal(listed.Done[0].Id, jumped.History.Step);
            Assert.Equal(third.Sequence + 1, jumped.Sequence);
            var after = await client.StateAsync(state.Id, Token);
            Assert.Equal("7", after.Settings["seed"]);
            var page = await client.EventsAsync(state.Id, third.Sequence, Token);
            var heard = Assert.Single(page.Events);
            Assert.Equal((GraphEventKind.Undone, listed.Done[1].Label), (heard.Kind, heard.Label));
            var back = (await client.GetFromJsonAsync(
                $"/api/v1/graphs/{state.Id}/history",
                ContractsJsonContext.Default.HistoryList,
                Token
            ))!;
            Assert.Equal([listed.Done[0]], back.Done);
            Assert.Equal([listed.Done[1], listed.Done[2]], back.Undone);

            // Forward to the newest, then to before everything, which is the file as it was opened.
            Assert.Equal(
                third.History.Step,
                (await client.JumpAsync(state.Id, third.History.Step, Token)).History.Step
            );
            Assert.Equal("9", (await client.StateAsync(state.Id, Token)).Settings["seed"]);
            var start = await client.JumpAsync(state.Id, null, Token);
            Assert.Null(start.History.Step);
            Assert.False(start.History.Dirty);
            AssertSameDocument(state, await client.StateAsync(state.Id, Token));

            // A new edit drops what was undone, and a jump to a dropped step changes nothing and says why.
            await client.EditAsync(state.Id, Token, new GraphSetSetting("seed", "3"));
            using var gone = await client.PostAsJsonAsync(
                $"/api/v1/graphs/{state.Id}/history",
                new(third.History.Step),
                ContractsJsonContext.Default.HistoryJump,
                Token
            );
            Assert.Equal(HttpStatusCode.Conflict, gone.StatusCode);
            Assert.Contains("history-step-gone", await gone.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
            Assert.Equal("3", (await client.StateAsync(state.Id, Token)).Settings["seed"]);
        }
    }

    [Fact]
    public async Task Undo_and_redo_go_a_batch_at_a_time_and_cover_every_kind_of_edit() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 1, 2, -1),
                new GraphConnect(5, "Out", -1, "Input"),
                new GraphDisconnect(3, "Input"),
                new GraphMoveNodes([new(1, 50, 60)]),
                new GraphSetValue(2, "Hue", [0.25f]),
                new GraphSetText(4, "Usage", "normal"),
                new GraphSetSetting("seed", "9"),
                new GraphRemoveNodes([5])
            );
            var edited = await client.StateAsync(state.Id, Token);
            Assert.Equal("normal", edited.Nodes.Single(static n => n.Id == 4).Texts["Usage"]);
            Assert.DoesNotContain(edited.Nodes, static n => n.Id == 5);

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            AssertSameDocument(state, undone);
            Assert.False(undone.History.CanUndo);
            Assert.False(undone.History.Dirty);
            Assert.True(undone.History.CanRedo);

            await client.ActAsync(state.Id, "redo", Token);
            AssertSameDocument(edited, await client.StateAsync(state.Id, Token));

            await client.ActAsync(state.Id, "undo", Token);
            using var nothing = await client.PostAsync($"/api/v1/graphs/{state.Id}/undo", null, Token);
            Assert.Equal(HttpStatusCode.Conflict, nothing.StatusCode);
        }
    }

    [Fact]
    public async Task A_refused_batch_changes_nothing_and_sends_no_event() {
        var (client, state) = await OpenAsync();
        using (client) {
            GraphCommand[][] refused = [
                [new GraphAddNode("No/Such Node", 0, 0)],
                [new GraphRemoveNodes([99])],
                [new GraphMoveNodes([new(99, 0, 0)])],
                [new GraphConnect(1, "Nope", 2, "Input")],
                [new GraphConnect(3, "Out", 1, "Nope")],
                [new GraphConnect(3, "Out", 2, "Input")], // a cycle
                [new GraphDisconnect(1, "Input")],
                [new GraphSetValue(1, "Nope", [1f])],
                [new GraphSetValue(1, "Scale", [1f, 2f])],
                [new GraphSetValue(2, "Input", [1f])],
                [new GraphSetText(4, "Usage", "no such usage")],
                [new GraphSetText(4, "Nope", "x")],
                [new GraphSetSetting("nope", "1")],
                [new GraphSetSetting("baseWidth", "0")],
                [new GraphSetSetting(TextureSizes.GraphSetting, "300")],
                [new GraphAddNode("Colour/Invert", 0, 0, 5)],
                []
            ];
            foreach (var commands in refused) {
                // A good command first, so that a partial application would show.
                GraphCommand[] batch = commands.Length == 0 ? [] : [new GraphMoveNodes([new(1, 7, 7)]), ..commands];
                using var response = await client.SendAsync(state.Id, new(batch), Token);
                Assert.True(
                    response.StatusCode == HttpStatusCode.UnprocessableEntity,
                    $"{commands.FirstOrDefault()}: {response.StatusCode}"
                );
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
                Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
            }

            var after = await client.StateAsync(state.Id, Token);
            AssertSameDocument(state, after);
            Assert.Equal(state.Revision, after.Revision);
            Assert.Empty((await client.EventsAsync(state.Id, state.Sequence, Token)).Events);
        }
    }

    [Fact]
    public async Task A_drag_merges_into_one_undo_and_undo_goes_back_to_before_it() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.1f]));
            foreach (var value in new[] { 0.2f, 0.3f, 0.4f }) {
                using var merged = await client.SendAsync(
                    state.Id,
                    new([new GraphSetValue(2, "Hue", [value])], Merge: true),
                    Token
                );
                merged.EnsureSuccessStatusCode();
            }

            var dragged = await client.StateAsync(state.Id, Token);
            Assert.Equal(0.4f, Assert.Single(dragged.Nodes[1].Values["Hue"]));

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            Assert.False(undone.History.CanUndo);
            Assert.DoesNotContain("Hue", undone.Nodes[1].Values.Keys);

            await client.ActAsync(state.Id, "redo", Token);
            Assert.Equal(0.4f, Assert.Single((await client.StateAsync(state.Id, Token)).Nodes[1].Values["Hue"]));
        }
    }

    [Fact]
    public async Task Merging_stops_at_a_save_so_undo_can_return_to_the_saved_state() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.1f]));
            await client.ActAsync(state.Id, "save", Token);
            using var merged = await client.SendAsync(
                state.Id,
                new([new GraphSetValue(2, "Hue", [0.2f])], Merge: true),
                Token
            );
            merged.EnsureSuccessStatusCode();
            Assert.True((await client.StateAsync(state.Id, Token)).History.Dirty);

            await client.ActAsync(state.Id, "undo", Token);
            var undone = await client.StateAsync(state.Id, Token);
            Assert.False(undone.History.Dirty);
            Assert.Equal(0.1f, Assert.Single(undone.Nodes[1].Values["Hue"]));
        }
    }

    [Fact]
    public async Task Events_are_numbered_and_their_deltas_rebuild_the_document_for_a_second_client() {
        var (first, state) = await OpenAsync();
        using (first) {
            using var second = server.AuthorisedClient();
            await first.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 0, 0, -1),
                new GraphConnect(1, "Out", -1, "Input")
            );
            await first.EditAsync(state.Id, Token, new GraphRemoveNodes([5]));
            await second.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));
            await second.ActAsync(state.Id, "undo", Token);
            await first.ActAsync(state.Id, "undo", Token);
            await first.ActAsync(state.Id, "redo", Token);
            await first.EditAsync(state.Id, Token, new GraphMoveNodes([new(3, 1, 1)]));

            var page = await second.EventsAsync(state.Id, state.Sequence, Token);
            var edits = page.Events.Where(static e => e.Kind != GraphEventKind.Evaluated).ToArray();
            Assert.Equal(
                Enumerable.Range(1, page.Events.Count).Select(static i => (long)i),
                page.Events.Select(e => e.Sequence - state.Sequence)
            );
            Assert.Equal(
                [
                    GraphEventKind.Edited, GraphEventKind.Edited, GraphEventKind.Edited, GraphEventKind.Undone,
                    GraphEventKind.Undone, GraphEventKind.Redone, GraphEventKind.Edited
                ],
                edits.Select(static e => e.Kind)
            );

            var held = GraphTesting.Replay(state, edits);
            var now = await first.StateAsync(state.Id, Token);
            Assert.Equal(now.Nodes.Select(static n => n.Id), held.Nodes.Keys);
            foreach (var node in now.Nodes) {
                var replayed = held.Nodes[node.Id];
                Assert.Equal((node.Type, node.X, node.Y), (replayed.Type, replayed.X, replayed.Y));
                Assert.Equal(node.Values.Keys.Order(), replayed.Values.Keys.Order());
            }

            Assert.True(held.Edges.SetEquals(now.Edges));
            Assert.Equal(now.Revision, edits[^1].Revision);
        }
    }

    [Fact]
    public async Task A_second_client_on_the_socket_sees_the_first_clients_edits_and_a_late_one_catches_up() {
        var (client, state) = await OpenAsync();
        using (client) {
            var sockets = server.Server.CreateWebSocketClient();
            sockets.ConfigureRequest =
                static request => request.Headers.Authorization = $"Bearer {ServerFixture.AccessToken}";
            var address = new Uri(server.Server.BaseAddress, $"ws/graphs/{state.Id}?since={state.Sequence}");
            using var socket = await sockets.ConnectAsync(new UriBuilder(address) { Scheme = "ws" }.Uri, Token);

            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));
            await client.ActAsync(state.Id, "undo", Token);

            var edited = await ReceiveAsync(socket);
            Assert.Equal((state.Sequence + 1, GraphEventKind.Edited), (edited.Sequence, edited.Kind));
            Assert.Equal(2, Assert.Single(edited.Delta!.Nodes).Id);
            var undone = await ReceiveAsync(socket);
            Assert.Equal((state.Sequence + 2, GraphEventKind.Undone), (undone.Sequence, undone.Kind));

            // A client that comes back later says where it got to, and misses nothing.
            using var late = await sockets.ConnectAsync(new UriBuilder(address) { Scheme = "ws" }.Uri, Token);
            Assert.Equal(edited.Sequence, (await ReceiveAsync(late)).Sequence);
            Assert.Equal(undone.Sequence, (await ReceiveAsync(late)).Sequence);
        }
    }

    [Fact]
    public async Task A_client_that_asks_for_events_nobody_holds_is_told_to_read_the_state() {
        var (client, state) = await OpenAsync();
        using (client) {
            var page = await client.EventsAsync(state.Id, state.Sequence + 50, Token);
            Assert.Equal(GraphEventKind.Resync, Assert.Single(page.Events).Kind);
        }
    }

    [Fact]
    public async Task A_saved_file_reopens_as_the_same_document_with_the_same_bytes() {
        var (client, state) = await OpenAsync();
        using (client) {
            await client.EditAsync(
                state.Id,
                Token,
                new GraphAddNode("Colour/Invert", 10, 20, -1),
                new GraphConnect(1, "Out", -1, "Input"),
                new GraphSetValue(2, "Hue", [0.125f])
            );
            var saved = await client.ActAsync(state.Id, "save", Token);
            Assert.False(saved.History.Dirty);
            var edited = await client.StateAsync(state.Id, Token);
            var bytes = await File.ReadAllBytesAsync(state.Path, Token);

            using var closed = await client.DeleteAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
            using var gone = await client.GetAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

            var reopened = await client.OpenAsync(state.Path, Token);
            Assert.NotEqual(state.Id, reopened.Id);
            AssertSameDocument(edited, reopened);
            Assert.Empty(reopened.Repairs);
            await client.ActAsync(reopened.Id, "save", Token);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(state.Path, Token));
        }
    }

    [Fact]
    public async Task Closing_with_unsaved_edits_needs_discard_and_leaves_the_file() {
        var (client, state) = await OpenAsync();
        using (client) {
            var before = await File.ReadAllTextAsync(state.Path, Token);
            await client.EditAsync(state.Id, Token, new GraphSetValue(2, "Hue", [0.5f]));

            using var refused = await client.DeleteAsync($"/api/v1/graphs/{state.Id}", Token);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            // A client tells the refusal apart by its code, and says it in its own words.
            using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
            Assert.Equal("document-unsaved", problem.RootElement.GetProperty("code").GetString());
            using var discarded = await client.DeleteAsync($"/api/v1/graphs/{state.Id}?discard=true", Token);
            Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
            Assert.Equal(before, await File.ReadAllTextAsync(state.Path, Token));
        }
    }

    [Fact]
    public async Task A_file_changed_on_disk_refuses_a_save_unless_forced() {
        var (client, state) = await OpenAsync();
        using (client) {
            await File.AppendAllTextAsync(state.Path, "# someone else\n", Token);
            using var refused = await client.PostAsync($"/api/v1/graphs/{state.Id}/save", null, Token);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

            using var forced = await client.PostAsync($"/api/v1/graphs/{state.Id}/save?force=true", null, Token);
            Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
            Assert.DoesNotContain(
                "someone else",
                await File.ReadAllTextAsync(state.Path, Token),
                StringComparison.Ordinal
            );
        }
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("not-a-hash")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task A_preview_that_is_not_in_the_cache_is_a_404(string hash) {
        using var client = server.AuthorisedClient();
        using var response = await client.GetAsync($"/api/v1/previews/{Uri.EscapeDataString(hash)}", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_graph_api_needs_the_token() {
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/api/v1/graphs", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    static void AssertSameDocument(GraphState expected, GraphState actual) {
        Assert.Equal(Project(expected), Project(actual));
        Assert.Equal(expected.Edges.ToHashSet(), actual.Edges.ToHashSet());
        Assert.Equal(expected.Settings.OrderBy(static s => s.Key), actual.Settings.OrderBy(static s => s.Key));
    }

    static string[] Project(GraphState state) => [
        ..state.Nodes.Select(static node =>
            $"{node.Id} {node.Type} {node.X} {node.Y} "
            + string.Join(
                ",",
                node.Values.OrderBy(static v => v.Key).Select(static v => $"{v.Key}={string.Join('/', v.Value)}")
            )
            + " "
            + string.Join(",", node.Texts.OrderBy(static t => t.Key).Select(static t => $"{t.Key}={t.Value}"))
        )
    ];

    async Task<GraphEvent> ReceiveAsync(WebSocket socket) {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Token);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        var buffer = new byte[256 * 1024];
        var length = 0;
        while (true) {
            var part = await socket.ReceiveAsync(buffer.AsMemory(length), patience.Token);
            length += part.Count;
            if (part.EndOfMessage) {
                return JsonSerializer.Deserialize(buffer.AsSpan(0, length), ContractsJsonContext.Default.GraphEvent)!;
            }
        }
    }
}
