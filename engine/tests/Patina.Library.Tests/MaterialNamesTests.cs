using Patina.Contracts;

namespace Patina.Library.Tests;

public sealed class MaterialNamesTests {
    [Fact]
    public void A_set_can_be_told_from_every_other() {
        Assert.Equal(
            ["Metal", "Metal 2", "Material 3", "Wood"],
            MaterialNames.Unique(["Metal", "Metal", " ", " Wood "])
        );
    }
}
