using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Nexus.Desktop;

// Same palette as the Windows edition (src/Ui.xaml).
public static class Ui
{
    public static readonly Color BackgroundColor = Color.Parse("#101115"), SurfaceColor = Color.Parse("#171A20"), AccentColor = Color.Parse("#B4F176");
    public static readonly IBrush Background = new SolidColorBrush(BackgroundColor);
    public static readonly IBrush Surface = new SolidColorBrush(SurfaceColor);
    public static readonly IBrush SurfaceRaised = Brush("#1A1D24");
    public static readonly IBrush Control = Brush("#22262E");
    public static readonly IBrush ControlActive = Brush("#2C313B");
    public static readonly IBrush Line = Brush("#23262E");
    public static readonly IBrush ControlLine = Brush("#3A404B");
    public static readonly IBrush Text = Brush("#EFF2F8");
    public static readonly IBrush TextSoft = Brush("#BCC4D1");
    public static readonly IBrush TextMuted = Brush("#A3ACBC");
    public static readonly IBrush Accent = new SolidColorBrush(AccentColor);
    public static readonly IBrush OnAccent = Brush("#15200E");
    public static readonly IBrush AccentSoft = Brush("#2C3D1C");
    public static readonly IBrush AccentSoftText = Brush("#C6F59A");
    public static readonly IBrush Warning = Brush("#F2B66B");
    public static readonly IBrush Danger = Brush("#FFAEAE");
    public static readonly IBrush Favorite = Brush("#F2D46B");

    static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    static Style Presenter(Func<Selector?, Selector> selector, IBrush background, IBrush? foreground = null)
    {
        var style = new Style(x => selector(x).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, background));
        if (foreground is not null) style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, foreground));
        return style;
    }

    public static void AddStyles(Styles styles)
    {
        var button = new Style(x => x.OfType<Button>());
        button.Setters.Add(new Setter(Avalonia.Controls.Button.BackgroundProperty, Control));
        button.Setters.Add(new Setter(Avalonia.Controls.Button.ForegroundProperty, Text));
        button.Setters.Add(new Setter(Avalonia.Controls.Button.CornerRadiusProperty, new CornerRadius(8)));
        button.Setters.Add(new Setter(Avalonia.Controls.Button.PaddingProperty, new Thickness(14, 8)));
        styles.Add(button);
        styles.Add(Presenter(x => x.OfType<Button>().Class(":pointerover"), ControlActive, Text));
        styles.Add(Presenter(x => x.OfType<Button>().Class("accent"), Accent, OnAccent));
        styles.Add(Presenter(x => x.OfType<Button>().Class("accent").Class(":pointerover"), Brush("#C6F59A"), OnAccent));
        styles.Add(Presenter(x => x.OfType<Button>().Class("nav"), Brushes.Transparent, TextSoft));
        styles.Add(Presenter(x => x.OfType<Button>().Class("nav").Class(":pointerover"), Control, Text));
        styles.Add(Presenter(x => x.OfType<Button>().Class("nav").Class("active"), AccentSoft, AccentSoftText));
        styles.Add(Presenter(x => x.OfType<Button>().Class("card"), Brushes.Transparent));
        styles.Add(Presenter(x => x.OfType<Button>().Class("card").Class(":pointerover"), SurfaceRaised));
        styles.Add(Presenter(x => x.OfType<Button>().Class("card").Class(":pressed"), Control));
        var toggle = new Style(x => x.OfType<ToggleButton>().Class("chip"));
        toggle.Setters.Add(new Setter(ToggleButton.CornerRadiusProperty, new CornerRadius(16)));
        toggle.Setters.Add(new Setter(ToggleButton.PaddingProperty, new Thickness(12, 5)));
        toggle.Setters.Add(new Setter(ToggleButton.MarginProperty, new Thickness(0, 0, 8, 8)));
        toggle.Setters.Add(new Setter(ToggleButton.FontSizeProperty, 13.0));
        styles.Add(toggle);
        styles.Add(Presenter(x => x.OfType<ToggleButton>().Class("chip"), Control, TextSoft));
        styles.Add(Presenter(x => x.OfType<ToggleButton>().Class("chip").Class(":pointerover"), ControlActive, Text));
        styles.Add(Presenter(x => x.OfType<ToggleButton>().Class("chip").Class(":checked"), AccentSoft, AccentSoftText));
        var text = new Style(x => x.OfType<TextBlock>().Class("label"));
        text.Setters.Add(new Setter(TextBlock.ForegroundProperty, TextMuted));
        text.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
        text.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold));
        text.Setters.Add(new Setter(TextBlock.LetterSpacingProperty, 1.2));
        styles.Add(text);
    }

    public static Button Button(string label, Action action, string? cls = null)
    {
        var b = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center };
        if (cls is not null) foreach (string c in cls.Split(' ')) b.Classes.Add(c);
        b.Click += (_, _) => action();
        return b;
    }

    public static TextBlock Label(string text) => new() { Text = text.ToUpperInvariant(), Classes = { "label" }, Margin = new Thickness(0, 0, 0, 8) };

    public static Border Badge(string text, IBrush background, IBrush foreground) => new()
    {
        Background = background, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2), Margin = new Thickness(0, 0, 6, 0),
        Child = new TextBlock { Text = text, Foreground = foreground, FontSize = 11, FontWeight = FontWeight.SemiBold }
    };
}
