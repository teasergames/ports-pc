using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Nexus.Native;

namespace Nexus.Desktop;

internal static class Program
{
    internal static string? DataRoot;
    internal static string? SmokePath;
    internal static string SmokeMode = "";
    internal static bool Offline;
    internal static bool Failed;

    [STAThread]
    public static int Main(string[] args)
    {
        string? Value(string name)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0) return null;
            return i + 1 < args.Length ? args[i + 1] : throw new ArgumentException(name + " exige um valor.");
        }
        if (args.Contains("--version")) { Console.WriteLine("NEXUS Linux " + AppInfo.Version); return 0; }
        DataRoot = Value("--data-dir");
        Offline = args.Contains("--offline");
        foreach (string mode in new[] { "--smoke-test", "--catalog-smoke", "--editor-smoke" })
            if (Value(mode) is { } path) { SmokePath = Path.GetFullPath(path); SmokeMode = mode; Offline = true; }
        if (args.Contains("--storage-smoke"))
        {
            if (DataRoot is null) throw new ArgumentException("Use --data-dir para o teste isolado.");
            var store = new Store(DataRoot); var lib = store.Load(); store.Save(lib);
            Console.WriteLine("PASS armazenamento: " + store.Load().Games.Count + " jogos");
            return 0;
        }
        int code = AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
        return Failed ? 1 : code;
    }
}

public sealed class App : Application
{
    public override void Initialize()
    {
        var theme = new FluentTheme();
        theme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = Ui.AccentColor, RegionColor = Ui.SurfaceColor, AltHigh = Ui.BackgroundColor };
        Styles.Add(theme);
        Ui.AddStyles(Styles);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try { desktop.MainWindow = new MainWindow(); }
            catch (Exception ex)
            {
                if (Program.SmokePath is not null) { Console.Error.WriteLine(ex); Environment.Exit(1); }
                desktop.MainWindow = new Window { Title = "NEXUS · Biblioteca preservada", Width = 650, Height = 220, Background = Ui.Background, Content = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) } };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
