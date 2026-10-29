using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using Nexus.Native;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Nexus.Desktop;

internal static class Program
{
    internal static string? DataRoot;
    internal static string? SmokePath;
    [STAThread] public static void Main(string[] args)
    {
        int smoke = Array.IndexOf(args, "--smoke-test");
        if (smoke >= 0) SmokePath = smoke + 1 < args.Length ? Path.GetFullPath(args[smoke + 1]) : throw new ArgumentException("--smoke-test exige um PNG.");
        int data = Array.IndexOf(args, "--data-dir");
        if (data >= 0) DataRoot = data + 1 < args.Length ? args[data + 1] : throw new ArgumentException("--data-dir exige uma pasta.");
        if (args.Contains("--version")) { Console.WriteLine("NEXUS Linux 1.7.0-beta.1"); return; }
        if (args.Contains("--storage-smoke"))
        {
            if (DataRoot is null) throw new ArgumentException("Use --data-dir para o teste isolado.");
            var store = new Store(DataRoot); var lib = store.Load(); store.Save(lib);
            Console.WriteLine("PASS armazenamento: " + store.Load().Games.Count + " jogos"); return;
        }
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
    }
}

public sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try { desktop.MainWindow = new MainWindow(); }
            catch (Exception ex) { if (Program.SmokePath is not null) { Console.Error.WriteLine(ex); Environment.Exit(1); } desktop.MainWindow = new Window { Title = "NEXUS · Biblioteca preservada", Width = 650, Height = 200, Content = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) } }; }
        }
        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class MainWindow : Window
{
    readonly Store store = new(Program.DataRoot ?? Store.DefaultRoot);
    readonly Library library;
    readonly FileStream instanceLock;
    readonly Image artwork = new() { Stretch = Stretch.Uniform, Height = 320 };
    Bitmap? artworkBitmap;
    readonly ListBox games = new();
    readonly TextBox search = new() { Watermark = "Buscar jogos" };
    readonly CheckBox favorites = new() { Content = "Favoritos" };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    Game? Selected => (games.SelectedItem as ListBoxItem)?.Tag as Game;
    public MainWindow()
    {
        instanceLock = new FileStream(Path.Combine(store.Root, "native.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Closed += (_, _) => { artworkBitmap?.Dispose(); instanceLock.Dispose(); };
        library = store.Load(); Title = "NEXUS · Linux · Prévia nativa"; Width = 1100; Height = 740; MinWidth = 800; MinHeight = 500; Background = new SolidColorBrush(Color.Parse("#101115"));
        var root = new DockPanel { Margin = new Thickness(24), LastChildFill = true }; Content = root;
        var header = new StackPanel { Spacing = 12 };
        header.Children.Add(new TextBlock { Text = "NEXUS", FontSize = 32, FontWeight = FontWeight.Bold });
        header.Children.Add(new TextBlock { Text = "Biblioteca Linux · executáveis nativos e AppImages", FontSize = 16 });
        header.Children.Add(search); header.Children.Add(favorites);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        AddButton(buttons, "Adicionar jogo", async () => await Edit(null));
        AddButton(buttons, "Editar cadastro", async () => { if (Selected is { } g) await Edit(g); });
        AddButton(buttons, "Jogar", () => Start(false));
        AddButton(buttons, "Configurar jogo", () => Start(true));
        AddButton(buttons, "Favoritar", () => { if (Selected is { } g) Commit(() => g.Favorite = !g.Favorite); });
        AddButton(buttons, "Remover cadastro", async () => { if (Selected is { } g && await Confirm("Remover apenas o cadastro de " + g.Name + "? Os arquivos do jogo serão preservados.")) Commit(() => library.Games.Remove(g)); });
        AddButton(buttons, "Catálogo de projetos", ShowCatalog);
        header.Children.Add(buttons); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        DockPanel.SetDock(status, Dock.Bottom); status.Margin = new Thickness(0, 12, 0, 0); root.Children.Add(status);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,3*"), Margin = new Thickness(0, 12, 0, 0) };
        body.Children.Add(games); Grid.SetColumn(artwork, 1); body.Children.Add(artwork); root.Children.Add(body);
        games.SelectionChanged += (_, _) => {
            artwork.Source = null; artworkBitmap?.Dispose(); artworkBitmap = null;
            try { if (Selected?.Artwork is { Length: > 0 } path && File.Exists(path)) artwork.Source = artworkBitmap = new Bitmap(path); }
            catch (Exception ex) { status.Text = "Artwork: " + ex.Message; }
        };
        if (Program.SmokePath is { } smokePath) Opened += (_, _) => DispatcherTimer.RunOnce(() => {
            try { using var bitmap = new RenderTargetBitmap(new PixelSize((int)ClientSize.Width, (int)ClientSize.Height)); bitmap.Render(this); bitmap.Save(smokePath); Close(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; Close(); }
        }, TimeSpan.FromMilliseconds(700));
        search.TextChanged += (_, _) => Refresh(); favorites.IsCheckedChanged += (_, _) => Refresh(); Refresh();
        status.Text = "Dados: " + store.Root + " · Catálogo Windows disponível para consulta; instalação automática ainda não portada para Linux.";
    }
    static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 6, 8, 8) };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    void Refresh()
    {
        string? selected = Selected?.Id;
        var items = library.Games.Where(g => (favorites.IsChecked != true || g.Favorite) && g.Name.Contains(search.Text ?? "", StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(g => g.Favorite).ThenBy(g => g.Name).Select(g => new ListBoxItem { Tag = g, Content = new TextBlock { Text = (g.Favorite ? "★ " : "") + g.Name + "  ·  " + g.Platform, Margin = new Thickness(12), FontSize = 18, TextWrapping = TextWrapping.Wrap } }).ToList();
        games.ItemsSource = items; games.SelectedItem = items.FirstOrDefault(i => ((Game)i.Tag!).Id == selected) ?? items.FirstOrDefault();
    }
    void Commit(Action action)
    {
        // Restore the in-memory model if persistence fails.
        string previous = JsonSerializer.Serialize(library);
        try { action(); store.Save(library); Refresh(); }
        catch (Exception ex) { var before = Store.Parse(previous); library.Games = before.Games; library.Extra = before.Extra; Refresh(); status.Text = ex.Message; }
    }
    void Start(bool configure)
    {
        if (Selected is not { } game) return;
        try
        {
            using var process = Process.Start(Launch.Prepare(game, configure));
            if (process is null) throw new IOException("O sistema não iniciou o processo.");
            if (!configure) Commit(() => game.LastPlayed = DateTimeOffset.Now.ToString("O"));
            status.Text = "Inicialização solicitada: " + game.Name;
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    async Task Edit(Game? original)
    {
        try
        {
            var window = new Window { Title = original is null ? "Adicionar jogo Linux" : "Editar cadastro", Width = 640, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var form = new StackPanel { Margin = new Thickness(24), Spacing = 8 };
            TextBox Field(string label, string? value) { form.Children.Add(new TextBlock { Text = label }); var t = new TextBox { Text = value ?? "" }; form.Children.Add(t); return t; }
            var name = Field("Nome", original?.Name); var exe = Field("Executável Linux / AppImage", original?.Executable);
            AddButton(form, "Escolher executável", async () => {
                try { var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Executável Linux", AllowMultiple = false }); if (files.FirstOrDefault()?.TryGetLocalPath() is { } p) { exe.Text = p; if (string.IsNullOrWhiteSpace(name.Text)) name.Text = Path.GetFileNameWithoutExtension(p); } }
                catch (Exception ex) { status.Text = ex.Message; }
            });
            var args = Field("Argumentos (sem comandos de shell)", original?.Arguments);
            var cwd = Field("Pasta de trabalho (vazia = pasta do executável)", original?.WorkingDirectory);
            var platform = Field("Plataforma", original?.Platform ?? "Outras");
            var config = Field("Executável de configuração (opcional)", original?.ConfigurationExecutable);
            var configArgs = Field("Argumentos de configuração", original?.ConfigurationArguments);
            var artworkPath = Field("Imagem local (PNG/JPEG, opcional)", original?.Artwork);
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; form.Children.Add(error);
            AddButton(form, "Salvar", () => {
                try {
                    if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidDataException("Informe o nome.");
                    var game = original is null ? new Game() : JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(original))!;
                    game.Name = name.Text.Trim(); game.Executable = exe.Text ?? ""; game.Arguments = args.Text ?? ""; game.WorkingDirectory = cwd.Text ?? ""; game.Platform = platform.Text ?? "Outras";
                    game.Artwork = artworkPath.Text ?? "";
                    if (!string.IsNullOrWhiteSpace(game.Artwork)) {
                        using var image = new Bitmap(game.Artwork);
                        if (image.PixelSize.Width > 16000 || image.PixelSize.Height > 16000) throw new InvalidDataException("Imagem muito grande.");
                    }
                    game.ConfigurationExecutable = config.Text ?? ""; game.ConfigurationArguments = configArgs.Text ?? "";
                    Launch.Prepare(game); if (!string.IsNullOrWhiteSpace(game.ConfigurationExecutable)) Launch.Prepare(game, true);
                    var next = new Library { Games = new List<Game>(library.Games), Extra = library.Extra };
                    int i = next.Games.FindIndex(g => g.Id == game.Id); if (i < 0) next.Games.Add(game); else next.Games[i] = game;
                    store.Save(next); library.Games = next.Games; Refresh(); window.Close();
                } catch (Exception ex) { error.Text = ex.Message; }
            });
            AddButton(form, "Cancelar", window.Close); window.Content = new ScrollViewer { Content = form }; await window.ShowDialog(this);
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    async Task<bool> Confirm(string message)
    {
        var dialog = new Window { Title = "NEXUS", Width = 500, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12 }; panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        AddButton(panel, "Remover cadastro", () => dialog.Close(true)); AddButton(panel, "Cancelar", () => dialog.Close(false)); dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }
    async void ShowCatalog()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.ports.json")!;
            using var doc = JsonDocument.Parse(stream);
            var dialog = new Window { Title = "Projetos · pacotes Windows não indicam suporte Linux", Width = 800, Height = 650 };
            var list = new StackPanel { Spacing = 8, Margin = new Thickness(20) };
            list.Children.Add(new TextBlock { Text = "Consulta às fontes do catálogo. Verifique no projeto se há uma versão Linux.\nEsta prévia não baixa nem instala pacotes Windows.", TextWrapping = TextWrapping.Wrap });
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                string repo = entry.GetProperty("Repository").GetString() ?? "";
                if (repo.Split('/') is not [var owner, var project] || !Safe(owner) || !Safe(project)) continue;
                string url = "https://github.com/" + repo;
                AddButton(list, entry.GetProperty("Name").GetString() ?? repo, () => {
                    try { Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { url }, UseShellExecute = false })?.Dispose(); }
                    catch (Exception ex) { status.Text = ex.Message; }
                });
            }
            dialog.Content = new ScrollViewer { Content = list }; await dialog.ShowDialog(this);
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    static bool Safe(string part) => part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
