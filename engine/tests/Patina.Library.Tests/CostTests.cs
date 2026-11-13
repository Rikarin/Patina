namespace Patina.Library.Tests;

/// <summary>What the library's documents cost, from the index (#207).</summary>
public sealed class CostTests : IDisposable {
    readonly TestLibrary library = new();

    public void Dispose() => library.Dispose();

    static string Stack(int width, int height, params int[] channels) {
        var sets = string.Concat(
            channels.Select(static (count, set) =>
                $"  - name: Set{set}\n    channels:\n"
                + string.Concat(Enumerable.Range(0, count).Select(static c => $"      - usage: c{c}\n"))
            )
        );
        return $"version: 1\nname: Stack\nbaseWidth: {width}\nbaseHeight: {height}\nsets:\n{sets}";
    }

    void Project(string path, params (string File, string Text)[] files) {
        library.Write(path, TestLibrary.Project(path));
        foreach (var (file, text) in files) {
            library.SaveFile($"projects/{path}/{file}", text);
        }
    }

    [Fact]
    public void A_folder_costs_its_stacks_maps_by_each_folder_or_project_in_it_costliest_first() {
        Project("props/crate", ("crate.vxlayers", Stack(1024, 1024, 3)), ("trim.vxlayers", Stack(512, 256, 2, 1)));
        Project("props/barrel", ("barrel.vxlayers", Stack(2048, 2048, 4)), ("rust.vxtexgraph", "name: Rust"));
        Project("hero", ("hero.vxlayers", Stack(4096, 4096, 5)));
        using var open = library.Open();
        open.Rescan(TestContext.Current.CancellationToken);

        var top = open.Costs(string.Empty);
        var props = open.Costs("props");

        Assert.Equal([("hero", 1), ("props", 3)], top.Items.Select(static item => (item.Name, item.Documents)));
        Assert.Equal(4096L * 4096 * 5 * 4, top.Items[0].Bytes);
        Assert.Equal(top.Items.Sum(static item => item.Bytes), top.Bytes);
        Assert.Equal(
            [("barrel", 2048L * 2048 * 4 * 4), ("crate", 1024L * 1024 * 3 * 4 + 512L * 256 * 3 * 4)],
            props.Items.Select(static item => (item.Name, item.Bytes))
        );
    }

    [Fact]
    public void The_documents_over_a_budget_come_costliest_first_a_page_at_a_time() {
        Project("a", ("big.vxlayers", Stack(2048, 2048, 4)), ("small.vxlayers", Stack(256, 256, 3)));
        Project("b", ("mid.vxlayers", Stack(1024, 1024, 4)));
        using var open = library.Open();
        open.Rescan(TestContext.Current.CancellationToken);

        var over = open.CostlierThan(string.Empty, 1024L * 1024, 0, 1);
        var rest = open.CostlierThan(string.Empty, 1024L * 1024, 1, 10);

        Assert.Equal(2, over.Total);
        Assert.Equal(("projects/a/big.vxlayers", 2048, 2048, 4), Single(over));
        Assert.Equal("projects/b/mid.vxlayers", Assert.Single(rest.Items).Path);
    }

    [Fact]
    public void A_stack_that_changes_size_costs_its_new_size_after_the_next_scan() {
        Project("a", ("doc.vxlayers", Stack(512, 512, 2)));
        using var open = library.Open();
        open.Rescan(TestContext.Current.CancellationToken);
        library.SaveFile("projects/a/doc.vxlayers", Stack(1024, 512, 3));

        open.Rescan(TestContext.Current.CancellationToken);

        Assert.Equal(1024L * 512 * 3 * 4, open.Costs(string.Empty).Bytes);
    }

    static (string, int, int, int) Single(Contracts.DocumentCostPage page) {
        var item = Assert.Single(page.Items);
        return (item.Path, item.Width, item.Height, item.Maps);
    }
}
