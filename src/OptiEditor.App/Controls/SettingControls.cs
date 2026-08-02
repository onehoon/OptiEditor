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

public sealed class CollapsibleSectionCard : Grid
{
    private readonly StackPanel _body;
    private readonly TextBlock _chevron;

    public CollapsibleSectionCard(string title, UIElement content, bool isExpanded = false)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        var root = new StackPanel();
        var header = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14, 12, 14, 12) };
        var headerContent = new Grid { ColumnSpacing = 12 };
        headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerContent.Children.Add(new SymbolIcon(Symbol.Bullets) { VerticalAlignment = VerticalAlignment.Center });
        var titleText = new TextBlock { Text = title, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(titleText, 1); headerContent.Children.Add(titleText);
        _chevron = new TextBlock { Text = "›", FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_chevron, 2); headerContent.Children.Add(_chevron);
        header.Content = headerContent;
        header.Click += (_, _) => IsExpanded = !IsExpanded;
        root.Children.Add(header);

        _body = new StackPanel { Spacing = 8, Padding = new Thickness(14, 0, 14, 14), Visibility = Visibility.Collapsed };
        _body.Children.Add(content);
        root.Children.Add(_body);
        Children.Add(new Border
        {
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = root
        });
        IsExpanded = isExpanded;
    }

    public bool IsExpanded
    {
        get => _body.Visibility == Visibility.Visible;
        set
        {
            _body.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            _chevron.Text = value ? "⌄" : "›";
        }
    }
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
        if (item.Binding.Definition.SupportsAuto) combo.Items.Add(new ComboBoxItem { Tag = "auto", Content = "Auto" });
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

public sealed class AutoStepperSettingControl : SettingControlBase
{
    public AutoStepperSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var auto = new Button { Content = "Auto" };
        auto.Click += (_, _) => SetValue("auto");
        row.Children.Add(auto);
        var definition = item.Binding.Definition;
        var number = new NumberBox
        {
            Minimum = definition.Minimum ?? double.MinValue,
            Maximum = definition.Maximum ?? double.MaxValue,
            SmallChange = definition.Step ?? 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 180,
            Value = double.TryParse(item.CurrentRawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var current) ? current : definition.Minimum ?? 0
        };
        number.ValueChanged += (_, _) =>
        {
            if (double.IsNaN(number.Value)) return;
            var value = definition.ValueKind == SettingValueKind.Integer
                ? ((long)Math.Round(number.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            SetValue(value);
        };
        row.Children.Add(number);
        Children.Add(row);
    }
}

public sealed class ShortcutSettingControl : SettingControlBase
{
    private readonly TextBox _box;
    private readonly TextBlock _captureHint;
    private bool _isCapturing;

    public ShortcutSettingControl(EditorSettingItemViewModel item, Action<EditorSettingItemViewModel> changed) : base(item, changed)
    {
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        AddChoice(choices, "Auto", "auto");
        AddChoice(choices, "Disabled", "-1");
        var capture = new Button { Content = "Capture Key" };
        capture.Click += (_, _) => BeginCapture();
        choices.Children.Add(capture);
        Children.Add(choices);

        _box = new TextBox { Text = item.CurrentRawValue, PlaceholderText = "Raw input: Auto, -1, decimal key, or 0xNN", MinWidth = 220 };
        _box.TextChanged += (_, _) => SetValue(_box.Text);
        _box.KeyDown += OnKeyDown;
        Children.Add(_box);
        _captureHint = new TextBlock { Text = "Enter a raw value, or use Capture Key." };
        Children.Add(_captureHint);
    }

    private void AddChoice(Panel panel, string label, string value)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => ApplyChoice(value);
        panel.Children.Add(button);
    }

    private void ApplyChoice(string value)
    {
        _isCapturing = false;
        _captureHint.Text = "Enter a raw value, or use Capture Key.";
        _box.Text = value;
        SetValue(value);
    }

    private void BeginCapture()
    {
        _isCapturing = true;
        _captureHint.Text = "Press a key to capture it. Press Escape to cancel.";
        _box.Focus(FocusState.Programmatic);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturing) return;
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _isCapturing = false;
            _captureHint.Text = "Key capture canceled. Enter a raw value, or use Capture Key.";
            e.Handled = true;
            return;
        }
        if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Menu or Windows.System.VirtualKey.Shift) return;
        var value = $"0x{((int)e.Key):X2}";
        _box.Text = value;
        SetValue(value); e.Handled = true;
        _isCapturing = false;
        _captureHint.Text = "Captured key value. You can still edit the raw value.";
    }
}
