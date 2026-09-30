using MineEngine.Core.Identifiers;

namespace MineEngine.Tests;

public sealed class IdentifierTests
{
    [Theory]
    [InlineData("magic_sword", true)]
    [InlineData("a", true)]
    [InlineData("ruby2", true)]
    [InlineData("Magic", false)]
    [InlineData("2ruby", false)]
    [InlineData("magic-sword", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ResourceId_validation_follows_minecraft_rules(string? candidate, bool expected) =>
        Assert.Equal(expected, ResourceId.IsValid(candidate));

    [Theory]
    [InlineData("Épée magique", "epee_magique")]
    [InlineData("  Ruby   Block!! ", "ruby_block")]
    [InlineData("42 blocs", "id_42_blocs")]
    [InlineData("???", "unnamed")]
    public void ResourceId_is_built_from_free_text(string text, string expected) =>
        Assert.Equal(expected, ResourceId.FromText(text).Value);

    [Fact]
    public void ModId_requires_two_characters()
    {
        Assert.False(ModId.IsValid("a"));
        Assert.True(ModId.IsValid("ab"));
        Assert.Equal("mon_mod", ModId.FromText("Mon mod").Value);
    }

    [Fact]
    public void Identifiers_are_compared_by_value() =>
        Assert.Equal(ResourceId.Parse("ruby"), ResourceId.Parse("ruby"));
}
