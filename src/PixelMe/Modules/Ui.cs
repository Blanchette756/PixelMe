using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelMe.Modules;

public static class Ui
{
    public static TextBlock Text(string text, double size = 13, FontWeight? weight = null, string brush = "Island.Foreground")
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    public static TextBlock Glyph(string glyph, double size = 16, string brush = "Island.Foreground")
    {
        var block = Text(glyph, size, null, brush);
        block.TextTrimming = TextTrimming.None;
        block.SetResourceReference(TextBlock.FontFamilyProperty, "Island.IconFont");
        return block;
    }

    public static Button IconButton(string glyph, string tooltip, Action click, double size = 28, double fontSize = 13)
    {
        var button = new Button { Content = glyph, ToolTip = tooltip, Width = size, Height = size, FontSize = fontSize };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Island.IconButton");
        button.Click += (_, _) => click();
        return button;
    }

    public static Button Pill(string text, Action click, string? glyph = null)
    {
        object content = text;
        if (glyph is not null)
        {
            var icon = Glyph(glyph, 12, "Island.Foreground");
            icon.Margin = new Thickness(0, 0, 6, 0);
            content = Row(0, icon, Text(text, 12.5, FontWeights.SemiBold));
        }

        var button = new Button { Content = content };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Island.PillButton");
        button.Click += (_, _) => click();
        return button;
    }

    public static Button Tile(UIElement content, Action click, bool active = false)
    {
        var button = new Button { Content = content };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Island.Tile");
        SetActive(button, active);
        button.Click += (_, _) => click();
        return button;
    }

    public static void SetActive(Control control, bool active)
    {
        if (active)
        {
            control.SetResourceReference(Control.BackgroundProperty, "Island.Accent");
            control.Foreground = Brushes.Black;
        }
        else
        {
            control.SetResourceReference(Control.BackgroundProperty, "Island.Subtle");
            control.SetResourceReference(Control.ForegroundProperty, "Island.Foreground");
        }
    }

    public static ToggleButton Chip(string text, bool isChecked, Action click)
    {
        var chip = new ToggleButton { Content = text, IsChecked = isChecked };
        chip.SetResourceReference(FrameworkElement.StyleProperty, "Island.Chip");
        chip.Click += (_, _) => click();
        return chip;
    }

    public static Border Card(UIElement child, double padding = 12)
    {
        var card = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(padding), Child = child };
        card.SetResourceReference(Border.BackgroundProperty, "Island.Subtle");
        return card;
    }

    public static TextBox Input(string placeholder, Action<TextBox>? submit = null)
    {
        var box = new TextBox { Tag = placeholder };
        box.SetResourceReference(FrameworkElement.StyleProperty, "Island.TextBox");
        if (submit is not null)
        {
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    submit(box);
                    e.Handled = true;
                }
            };
        }

        return box;
    }

    public static CheckBox Check(string text, bool isChecked, Action<bool> changed)
    {
        var check = new CheckBox { Content = text, IsChecked = isChecked };
        check.SetResourceReference(FrameworkElement.StyleProperty, "Island.CheckBox");
        check.Click += (_, _) => changed(check.IsChecked == true);
        return check;
    }

    public static ScrollViewer Scroll(UIElement content, bool horizontal = false)
    {
        var viewer = new ScrollViewer
        {
            Content = content,
            Focusable = false,
            VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
            PanningMode = horizontal ? PanningMode.HorizontalOnly : PanningMode.VerticalOnly,
        };

        if (horizontal)
        {
            viewer.PreviewMouseWheel += (_, e) =>
            {
                viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            };
        }

        return viewer;
    }

    public static StackPanel Row(double spacing, params UIElement[] children) => Stack(Orientation.Horizontal, spacing, children);

    public static StackPanel Column(double spacing, params UIElement[] children) => Stack(Orientation.Vertical, spacing, children);

    public static Grid Empty(string glyph, string title, string hint)
    {
        var icon = Glyph(glyph, 28, "Island.Secondary");
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var heading = Text(title, 15, FontWeights.SemiBold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.Margin = new Thickness(0, 10, 0, 4);
        var detail = Text(hint, 12, null, "Island.Secondary");
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.TextWrapping = TextWrapping.Wrap;
        detail.TextAlignment = TextAlignment.Center;
        detail.MaxWidth = 440;

        var panel = Column(0, icon, heading, detail);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;
        return new Grid { Children = { panel } };
    }

    public static Grid Columns(params (GridLength Width, UIElement Child)[] cells)
    {
        var grid = new Grid();
        for (var i = 0; i < cells.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = cells[i].Width });
            Grid.SetColumn(cells[i].Child, i);
            grid.Children.Add(cells[i].Child);
        }

        return grid;
    }

    public static GridLength Star(double value = 1) => new(value, GridUnitType.Star);

    public static GridLength Auto => GridLength.Auto;

    public static GridLength Px(double value) => new(value);

    public static T With<T>(this T element, Thickness margin)
        where T : FrameworkElement
    {
        element.Margin = margin;
        return element;
    }

    private static StackPanel Stack(Orientation orientation, double spacing, UIElement[] children)
    {
        var panel = new StackPanel { Orientation = orientation };
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0 && children[i] is FrameworkElement element)
            {
                var margin = element.Margin;
                element.Margin = orientation == Orientation.Horizontal
                    ? new Thickness(margin.Left + spacing, margin.Top, margin.Right, margin.Bottom)
                    : new Thickness(margin.Left, margin.Top + spacing, margin.Right, margin.Bottom);
            }

            panel.Children.Add(children[i]);
        }

        return panel;
    }
}
