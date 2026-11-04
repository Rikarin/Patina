using CsCheck;

namespace Patina.Library.Tests;

public sealed class IdsTests {
    [Fact]
    [Trait("Category", "Property")]
    public void A_library_id_survives_its_text_form() =>
        Gen.Guid.Where(static guid => guid != Guid.Empty)
            .Sample(static guid => GuidId.Parse<LibraryId>(new LibraryId(guid).ToString()) == new LibraryId(guid));

    [Fact]
    [Trait("Category", "Property")]
    public void A_project_id_survives_its_text_form() =>
        Gen.Guid.Where(static guid => guid != Guid.Empty)
            .Sample(static guid => GuidId.Parse<ProjectId>(new ProjectId(guid).ToString()) == new ProjectId(guid));

    [Fact]
    public void New_ids_differ() {
        Assert.NotEqual(LibraryId.New(), LibraryId.New());
        Assert.NotEqual(ProjectId.New(), ProjectId.New());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("crate_01")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Text_that_is_no_id_does_not_parse(string? text) {
        Assert.False(GuidId.TryParse<LibraryId>(text, out _));
        Assert.False(GuidId.TryParse<ProjectId>(text, out _));
    }
}
