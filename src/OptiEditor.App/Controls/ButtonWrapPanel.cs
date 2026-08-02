using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace OptiEditor.App.Controls;

public sealed class ButtonWrapPanel : Panel
{
    public double HorizontalSpacing { get; set; } = 8;
    public double VerticalSpacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var availableWidth = double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width;
        var lineWidth = 0d;
        var lineHeight = 0d;
        var desiredWidth = 0d;
        var desiredHeight = 0d;

        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var size = child.DesiredSize;
            var spacing = lineWidth > 0 ? HorizontalSpacing : 0;
            if (lineWidth > 0 && lineWidth + spacing + size.Width > availableWidth)
            {
                desiredWidth = Math.Max(desiredWidth, lineWidth);
                desiredHeight += lineHeight + VerticalSpacing;
                lineWidth = 0;
                lineHeight = 0;
                spacing = 0;
            }
            lineWidth += spacing + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        if (lineWidth > 0)
        {
            desiredWidth = Math.Max(desiredWidth, lineWidth);
            desiredHeight += lineHeight;
        }
        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;
        var y = 0d;
        var lineHeight = 0d;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var spacing = x > 0 ? HorizontalSpacing : 0;
            if (x > 0 && x + spacing + size.Width > finalSize.Width)
            {
                x = 0;
                y += lineHeight + VerticalSpacing;
                lineHeight = 0;
                spacing = 0;
            }
            x += spacing;
            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return finalSize;
    }
}
