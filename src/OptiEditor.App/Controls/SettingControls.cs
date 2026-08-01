using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OptiEditor.App.ViewModels;
using OptiEditor.Core.Schema;

namespace OptiEditor.App.Controls;

public abstract class SettingControlBase : StackPanel
{
    protected SettingControlBase(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed)
    {
        Item = item; Changed = changed; Spacing = 4;
    }
    protected EditorSettingItemViewModel Item { get; }
    protected Action<EditorSettingItemViewModel> Changed { get; }
    protected void SetValue(string value) { if (Item.CurrentRawValue == value) return; Item.CurrentRawValue = value; Changed(Item); }
}

public sealed class AutoBooleanSettingControl : SettingControlBase
{
    public AutoBooleanSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var combo = new ComboBox { MinWidth = 220 };
        Add(combo, "auto", "Auto"); Add(combo, "true", "Enabled"); Add(combo, "false", "Disabled"); Select(combo, item.CurrentRawValue);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem choice) SetValue((string)choice.Tag); };
        Children.Add(combo);
    }
    private static void Add(ComboBox combo, string value, string label) => combo.Items.Add(new ComboBoxItem { Tag = value, Content = label });
    private static void Select(ComboBox combo, string value) => combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(x => string.Equals((string)x.Tag, value, StringComparison.OrdinalIgnoreCase));
}

public sealed class EnumSettingControl : SettingControlBase
{
    public EnumSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var combo = new ComboBox { MinWidth = 220 };
        foreach (var option in item.Options) combo.Items.Add(new ComboBoxItem { Tag = option.Value, Content = option.Label });
        if (!item.Options.Any(x => string.Equals(x.Value, item.CurrentRawValue, StringComparison.OrdinalIgnoreCase))) combo.Items.Add(new ComboBoxItem { Tag = item.CurrentRawValue, Content = $"Unknown (preserved): {item.CurrentRawValue}" });
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(x => string.Equals((string)x.Tag, item.CurrentRawValue, StringComparison.OrdinalIgnoreCase));
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem choice) SetValue((string)choice.Tag); };
        Children.Add(combo);
    }
}

public sealed class AutoNumberSettingControl : SettingControlBase
{
    public AutoNumberSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var box = new TextBox { Text = item.CurrentRawValue, PlaceholderText = item.InputHint, MinWidth = 220 };
        box.TextChanged += (_, _) => SetValue(box.Text); Children.Add(box);
    }
}

public sealed class ShortcutSettingControl : SettingControlBase
{
    public ShortcutSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var box = new TextBox { Text = item.CurrentRawValue, PlaceholderText = "Press a key, or enter Auto, -1, decimal, 0xNN", MinWidth = 220 };
        box.TextChanged += (_, _) => SetValue(box.Text);
        box.KeyDown += OnKeyDown;
        Children.Add(box);
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Menu or Windows.System.VirtualKey.Shift) return;
        var value = $"0x{((int)e.Key):X2}";
        if (sender is TextBox box) box.Text = value;
        SetValue(value); e.Handled = true;
    }
}
