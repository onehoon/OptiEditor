using System.Text;
using OptiEditor.Core.Ini;
using OptiEditor.Core.Models;
using OptiEditor.Core.Schema;

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
}
