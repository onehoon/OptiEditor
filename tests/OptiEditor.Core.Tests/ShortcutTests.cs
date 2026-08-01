using OptiEditor.Core.Shortcuts;

namespace OptiEditor.Core.Tests;
public sealed class ShortcutTests
{
    [Theory][InlineData("auto", ShortcutValueMode.Auto, null)][InlineData("-1", ShortcutValueMode.Disabled, null)][InlineData("0x2D", ShortcutValueMode.Key, 45)][InlineData("0X2d", ShortcutValueMode.Key, 45)][InlineData("45", ShortcutValueMode.Key, 45)]
    public void Parses_shortcut_values(string raw, ShortcutValueMode mode, int? code) { var value = new ShortcutValueConverter().Parse(raw).Value; Assert.Equal(mode, value.Mode); Assert.Equal(code, value.VirtualKeyCode); }
    [Fact] public void Formats_keys_and_finds_explicit_conflicts() { var c = new ShortcutValueConverter(); Assert.Equal("0x2D", c.Format(new(ShortcutValueMode.Key, 45, null))); var conflicts = ShortcutConflictDetector.FindConflicts([("a", c.Parse("0x2D").Value), ("b", c.Parse("45").Value), ("c", c.Parse("auto").Value)]); Assert.Contains("a", conflicts.Keys); Assert.Single(conflicts["a"]); }
}
