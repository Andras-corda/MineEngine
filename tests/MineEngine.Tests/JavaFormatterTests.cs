using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Tests;

public sealed class JavaFormatterTests
{
    private readonly JavaFormatter _formatter = new();

    [Fact]
    public void Lines_are_reindented_by_braces_and_switch_cases()
    {
        const string messy = "package a;\n\n\n\npublic class A {   \nint x;\n      void run(int v) {\nswitch (v) {\ncase 1:\nx = 1;\nbreak;\ndefault:\nx = 0;\n}\nif (v > 2) {\nx++;\n} else {\nx--;\n}\n}\n}";
        const string expected = "package a;\n\npublic class A {\n    int x;\n    void run(int v) {\n        switch (v) {\n            case 1:\n                x = 1;\n                break;\n            default:\n                x = 0;\n        }\n        if (v > 2) {\n            x++;\n        } else {\n            x--;\n        }\n    }\n}\n";

        Assert.Equal(expected, _formatter.Format(messy));
    }

    [Fact]
    public void Strings_comments_and_open_parentheses_are_respected()
    {
        const string source = "class B {\n/**\n* Doc { pas une accolade\n*/\nString s = \"{ }\";\nchar c = '{';\nvoid f() { call(1,\n2); // }\n}\n}";
        const string expected = "class B {\n    /**\n     * Doc { pas une accolade\n     */\n    String s = \"{ }\";\n    char c = '{';\n    void f() { call(1,\n                2); // }\n    }\n}\n";

        Assert.Equal(expected, _formatter.Format(source));
    }

    [Theory]
    [InlineData(ModLoaderKind.Forge)]
    [InlineData(ModLoaderKind.NeoForge)]
    public void Formatting_keeps_the_class_templates_unchanged(ModLoaderKind loader)
    {
        foreach (JavaClassKind kind in Enum.GetValues<JavaClassKind>())
        {
            string source = JavaClassTemplates.Create(kind, "a.b", "Sample", ModId.Parse("ruby"), loader);
            Assert.Equal(source, _formatter.Format(source));
        }
    }
}
