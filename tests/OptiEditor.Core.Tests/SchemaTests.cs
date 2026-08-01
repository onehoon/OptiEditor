using System.Text;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.Core.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void Resolver_uses_only_the_detected_binary_family()
    {
        var resolver = new OptiSchemaResolver(); Assert.IsType<Opti09SchemaProvider>(resolver.Resolve(OptiSchemaFamily.V09)); Assert.IsType<Opti10SchemaProvider>(resolver.Resolve(OptiSchemaFamily.V10)); Assert.Throws<UnsupportedOptiSchemaException>(() => resolver.Resolve(OptiSchemaFamily.Unsupported));
    }
    [Fact]
    public void Version_specific_options_and_definitions_are_separate()
    {
        var resolver = new OptiSchemaResolver(); var v09 = resolver.Resolve(OptiSchemaFamily.V09); var v10 = resolver.Resolve(OptiSchemaFamily.V10);
        Assert.Contains(v09.FindById("upscalers.dx12")!.Options, x => x.Value == "fsr31"); Assert.DoesNotContain(v09.FindById("upscalers.dx12")!.Options, x => x.Value == "ffx"); Assert.Contains(v09.FindById("framegen.input")!.Options, x => x.Value == "nukems"); Assert.Null(v09.FindById("dlssg.interpolation")); Assert.Null(v09.FindById("image.shader"));
        Assert.Contains(v10.FindById("upscalers.dx12")!.Options, x => x.Value == "ffx"); Assert.DoesNotContain(v10.FindById("upscalers.dx12")!.Options, x => x.Value == "fsr31"); Assert.Contains(v10.FindById("framegen.output")!.Options, x => x.Value == "dlssgwithnvngx"); Assert.NotNull(v10.FindById("dlssg.interpolation")); Assert.NotNull(v10.FindById("image.shader"));
    }
    [Fact]
    public void Binding_shows_only_existing_keys_and_preserves_unknown_enum_values()
    {
        var doc = IniParser.Parse("[Upscalers]\nDx12Upscaler=future-value\n[Unknown]\nValue=x\n", new UTF8Encoding(false), false); var bindings = SettingBindingFactory.CreateVisible(new Opti10SchemaProvider(), doc);
        var binding = Assert.Single(bindings); Assert.True(binding.IsUnknownValue); Assert.False(doc.IsDirty); Assert.Equal("future-value", doc.GetRawValue(new("Upscalers", "Dx12Upscaler")));
    }
    [Fact]
    public void Validation_rejects_invalid_values_without_creating_patches()
    {
        var definition = new Opti10SchemaProvider().FindById("overlay.scale")!; var binding = new SettingValueBinding(definition, "auto"); binding.SetRawValue("3.0"); Assert.True(binding.HasValidationError); Assert.True(SettingValidator.Validate(definition, "1.5").IsValid); binding.SetRawValue("1.5"); Assert.False(binding.HasValidationError);
    }
    [Theory]
    [InlineData("auto")]
    [InlineData("-1")]
    [InlineData("0x2D")]
    [InlineData("0X2d")]
    [InlineData("45")]
    public void Shortcut_values_accept_supported_OptiScaler_formats(string value)
    {
        var definition = new Opti10SchemaProvider().FindById("shortcuts.menu")!;
        Assert.True(SettingValidator.Validate(definition, value).IsValid);
    }

    [Fact]
    public void Unknown_existing_value_does_not_block_an_unrelated_save()
    {
        var definition = new Opti10SchemaProvider().FindById("framegen.output")!;
        var binding = new SettingValueBinding(definition, "future-new-value");
        Assert.True(binding.IsUnknownValue);
        Assert.False(binding.HasValidationError);
        binding.SetRawValue("still-invalid");
        Assert.True(binding.HasValidationError);
    }

    [Fact]
    public void Fixture_ranges_match_schema()
    {
        var schema = new Opti10SchemaProvider();
        Assert.True(SettingValidator.Validate(schema.FindById("output.multiplier")!, "3.0").IsValid);
        Assert.False(SettingValidator.Validate(schema.FindById("output.multiplier")!, "3.1").IsValid);
        Assert.True(SettingValidator.Validate(schema.FindById("texture.mipmap-bias")!, "15.0").IsValid);
    }

    [Fact]
    public void Advanced_fixture_entries_are_schema_backed_and_group_order_is_stable()
    {
        var schema = new Opti10SchemaProvider();
        Assert.NotNull(schema.FindById("quality.ultra-performance"));
        Assert.NotNull(schema.FindById("texture.modify-comparison"));
        Assert.NotNull(schema.FindById("texture.mipmap-all"));
        Assert.True(schema.Settings.ToList().FindIndex(x => x.GroupId == "quality") < schema.Settings.ToList().FindIndex(x => x.GroupId == "texture"));
    }

    [Fact]
    public void User_visibility_override_wins_over_the_app_default()
    {
        var definition = new Opti10SchemaProvider().FindById("framegen.enabled")!;
        Assert.Equal(EditorVisibility.Visible, EditorVisibilityPolicy.Resolve(definition, OptiSchemaFamily.V10, []));
        Assert.Equal(EditorVisibility.Hidden, EditorVisibilityPolicy.Resolve(definition, OptiSchemaFamily.V10, [new(OptiSchemaFamily.V10, definition.Id, EditorVisibility.Hidden)]));
    }

    [Fact]
    public async Task Visibility_store_round_trips_user_overrides()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { var store = new EditorVisibilityStore(folder); await store.SaveAsync([new(OptiSchemaFamily.V09, "framegen.enabled", EditorVisibility.Hidden)]); var loaded = await store.LoadAsync(); var item = Assert.Single(loaded); Assert.Equal(OptiSchemaFamily.V09, item.Family); Assert.Equal(EditorVisibility.Hidden, item.Visibility); }
        finally { Directory.Delete(folder, true); }
    }
}
