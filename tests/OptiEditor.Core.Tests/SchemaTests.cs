using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;
using OptiEditor.Core.Storage;

namespace OptiEditor.Core.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void Generated_catalog_contains_only_root_ini_keys_processed_by_upstream_code()
    {
        var resolver = new OptiSchemaResolver();
        var v09 = resolver.Resolve(OptiSchemaFamily.V09);
        var v10 = resolver.Resolve(OptiSchemaFamily.V10);
        Assert.Equal(288, v09.Settings.Count);
        Assert.Equal(305, v10.Settings.Count);
        Assert.NotNull(v09.FindById("Plugins.LoadAsiPlugins"));
        Assert.NotNull(v10.FindById("Magnifier.Enabled"));
        Assert.Null(v09.FindById("DLSSG.InterpolationCount"));
        Assert.Null(v10.FindById("Hotfix.ManualInputPolling"));
    }

    [Fact]
    public void Generated_types_follow_the_upstream_config_reader()
    {
        var schema = new Opti10SchemaProvider();
        Assert.Equal(SettingValueKind.Boolean, schema.FindById("Plugins.LoadAsiPlugins")!.ValueKind);
        Assert.Equal(SettingValueKind.Integer, schema.FindById("FrameGen.AllowedFrameAhead")!.ValueKind);
        Assert.Equal(SettingValueKind.Double, schema.FindById("FSR.VerticalFov")!.ValueKind);
        Assert.Equal(SettingValueKind.String, schema.FindById("Menu.TTFFontPath")!.ValueKind);
        var forceModel = schema.FindById("FSR.Fsr4ForceModel")!;
        Assert.Equal(SettingValueKind.Enum, forceModel.ValueKind);
        Assert.Equal(["0", "1", "2"], forceModel.Options.Select(x => x.Value));
        Assert.Equal(0, forceModel.Minimum);
        Assert.Equal(2, forceModel.Maximum);
        Assert.Equal(SettingValueKind.Enum, schema.FindById("Upscalers.Dx11Upscaler")!.ValueKind);
        Assert.Equal(1, schema.FindById("Anisotropy.AnisotropyOverride")!.Minimum);
        Assert.Equal(16, schema.FindById("Anisotropy.AnisotropyOverride")!.Maximum);
        var menuScale = schema.FindById("Menu.Scale")!;
        Assert.Equal(SettingValueKind.Enum, menuScale.ValueKind);
        Assert.Equal(["0.5", "0.6", "0.7", "0.8", "0.9", "1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "2.0"], menuScale.Options.Select(x => x.Value));
    }

    [Fact]
    public void Generated_ranges_and_enum_values_are_validated()
    {
        var schema = new Opti10SchemaProvider();
        var anisotropy = schema.FindById("Anisotropy.AnisotropyOverride")!;
        Assert.False(SettingValidator.Validate(anisotropy, "17").IsValid);
        Assert.True(SettingValidator.Validate(anisotropy, "16").IsValid);
        var downscaler = schema.FindById("OutputScaling.Downscaler")!;
        Assert.False(SettingValidator.Validate(downscaler, "8").IsValid);
        Assert.True(SettingValidator.Validate(downscaler, "7").IsValid);
    }

    [Fact]
    public void Section_visibility_hides_every_key_in_that_section()
    {
        var schema = new Opti10SchemaProvider();
        var definition = schema.FindById("FrameGen.Enabled")!;
        Assert.Equal(EditorVisibility.Hidden, EditorVisibilityPolicy.Resolve(definition, OptiSchemaFamily.V10, [new(OptiSchemaFamily.V10, "FrameGen", EditorVisibility.Hidden)]));
    }

    [Fact]
    public async Task Visibility_store_round_trips_section_overrides()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OptiEditorTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { var store = new EditorVisibilityStore(folder); await store.SaveAsync([new(OptiSchemaFamily.V09, "Plugins", EditorVisibility.Hidden)]); var item = Assert.Single(await store.LoadAsync()); Assert.Equal("Plugins", item.Section); }
        finally { Directory.Delete(folder, true); }
    }
}
