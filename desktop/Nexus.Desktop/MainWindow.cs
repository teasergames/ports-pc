using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Nexus.Native;

namespace Nexus.Desktop;

public sealed class MainWindow : Window
{
    internal readonly Store Store = new(Program.DataRoot ?? Store.DefaultRoot);
    internal Library Library;
    internal readonly Catalog Catalog = new();
    readonly FileStream instanceLock;
    readonly CancellationTokenSource shutdown = new();
    readonly Dictionary<string, Bitmap> thumbnails = new();
    readonly HashSet<string> coverRequests = new();

    readonly Button navLibrary, navCatalog, launcherUpdate;
    readonly TextBox search = new() { Watermark = "Buscar na biblioteca", Width = 280, VerticalAlignment = VerticalAlignment.Center };
    readonly ContentControl page = new();
    readonly Grid libraryPage = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
    readonly CatalogView catalogView;
    readonly TextBlock status = new() { Foreground = Ui.TextMuted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    // Hero
    readonly Image heroCover = new() { Stretch = Stretch.UniformToFill };
    readonly Border heroCoverFrame;
    readonly TextBlock heroTitle = new() { FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Ui.Text, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock heroMeta = new() { Foreground = Ui.TextSoft, FontSize = 13, TextWrapping = TextWrapping.Wrap };
    readonly StackPanel heroBadges = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    readonly Button play, configure, favorite, more;
    readonly MenuItem moreUpdate = new() { Header = "Atualizar port" }, moreUninstall = new() { Header = "Desinstalar port…" }, moreFolder = new() { Header = "Abrir pasta do port" };

    // Collection
    readonly WrapPanel chips = new() { Orientation = Orientation.Horizontal };
    readonly WrapPanel grid = new() { Orientation = Orientation.Horizontal };
    readonly ComboBox sort = new() { Width = 220, ItemsSource = new[] { "Favoritos primeiro", "Nome (A–Z)", "Jogados recentemente", "Plataforma" } };
    readonly TextBlock empty = new() { Text = "Sua biblioteca está vazia. Use Adicionar jogo ou Baixar ports.", Foreground = Ui.TextMuted, FontSize = 15, Margin = new Thickness(4, 30), IsVisible = false };
    static readonly string[] SortModes = { "favorites", "name", "recent", "platform" };
    string platformFilter = "";
    bool favoritesOnly;
    Game? selected;

    public MainWindow()
    {
        instanceLock = new FileStream(Path.Combine(Store.Root, "native.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Library = Store.Load();
        Title = "NEXUS · Ports PC"; Width = 1280; Height = 820; MinWidth = 960; MinHeight = 620; Background = Ui.Background;
        try { using var icon = typeof(MainWindow).Assembly.GetManifestResourceStream("Nexus.icon.png"); if (icon is not null) Icon = new WindowIcon(icon); } catch (Exception) { }
        Closed += (_, _) => { shutdown.Cancel(); foreach (var b in thumbnails.Values) b.Dispose(); Catalog.Dispose(); instanceLock.Dispose(); };

        navLibrary = Ui.Button("Biblioteca", () => ShowLibrary(), "nav active");
        navCatalog = Ui.Button("Baixar ports", () => ShowCatalog(CatalogTab.All), "nav");
        var navUpdates = Ui.Button("Atualizações", () => ShowCatalog(CatalogTab.Updates), "nav");
        launcherUpdate = Ui.Button("Atualizar NEXUS", () => _ = UpdateLauncher(), "accent");
        launcherUpdate.IsVisible = false;
        var settings = Ui.Button("Configurações", () => _ = new SettingsDialog(this).ShowDialog(this));
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(24, 16, 24, 12) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        brand.Children.Add(new TextBlock { Text = "NEXUS", FontSize = 22, FontWeight = FontWeight.Black, Foreground = Ui.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) });
        brand.Children.Add(navLibrary); brand.Children.Add(navCatalog); brand.Children.Add(navUpdates);
        top.Children.Add(brand);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        right.Children.Add(launcherUpdate); right.Children.Add(search); right.Children.Add(settings);
        Grid.SetColumn(right, 2); top.Children.Add(right);

        play = Ui.Button("▶  Jogar", () => Start(false), "accent");
        play.FontWeight = FontWeight.SemiBold; play.Padding = new Thickness(22, 10);
        configure = Ui.Button("Configurar", () => Start(true));
        favorite = Ui.Button("☆ Favoritar", () => { if (selected is { } g) Commit(() => g.Favorite = !g.Favorite); });
        more = new Button { Content = "⋯", Padding = new Thickness(14, 8) };
        var edit = new MenuItem { Header = "Editar cadastro…" }; edit.Click += async (_, _) => { if (selected is { } g) await Edit(g); };
        var coverItem = new MenuItem { Header = "Buscar capa oficial" }; coverItem.Click += (_, _) => { if (selected is { } g) { g.Cover = null; g.CoverSource = null; coverRequests.Remove(g.Id); _ = FetchCover(g, true); } };
        moreFolder.Click += (_, _) => { if (selected is { } g) Try(() => Launch.OpenFolder(g.ManagedInstallationRoot ?? Path.GetDirectoryName(g.Executable)!)); };
        moreUpdate.Click += (_, _) => { if (selected is { } g && Catalog.Entries.FirstOrDefault(e => e.Id == g.CatalogId) is { } entry) ShowCatalog(CatalogTab.All, entry); };
        var remove = new MenuItem { Header = "Remover da biblioteca…" };
        remove.Click += async (_, _) => { if (selected is { } g && await Dialogs.Confirm(this, "Remover " + g.Name + " da biblioteca? Os arquivos do jogo são preservados.", "Remover")) Commit(() => Library.Games.Remove(g)); };
        moreUninstall.Click += async (_, _) => await Uninstall();
        more.Flyout = new MenuFlyout { Items = { edit, moreFolder, coverItem, moreUpdate, new Separator(), remove, moreUninstall } };

        heroCoverFrame = new Border { Width = 132, Height = 198, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Ui.Control, Child = heroCover, Margin = new Thickness(0, 0, 24, 0) };
        var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heroText.Children.Add(heroBadges); heroText.Children.Add(heroTitle); heroText.Children.Add(heroMeta);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(play); actions.Children.Add(configure); actions.Children.Add(favorite); actions.Children.Add(more);
        heroText.Children.Add(actions);
        var heroGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        heroGrid.Children.Add(heroCoverFrame); Grid.SetColumn(heroText, 1); heroGrid.Children.Add(heroText);
        var hero = new Border { Background = Ui.Surface, CornerRadius = new CornerRadius(14), Padding = new Thickness(22), Margin = new Thickness(24, 0, 24, 14), Child = heroGrid };
        libraryPage.Children.Add(hero);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 0, 24, 4) };
        var left = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 10) };
        titleRow.Children.Add(new TextBlock { Text = "Coleção", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Ui.Text, VerticalAlignment = VerticalAlignment.Center });
        var add = new Button { Content = "+ Adicionar jogo" };
        var addExe = new MenuItem { Header = "Escolher executável ou AppImage…" }; addExe.Click += async (_, _) => await Edit(null);
        var openCatalog = new MenuItem { Header = "Baixar do catálogo de ports" }; openCatalog.Click += (_, _) => ShowCatalog(CatalogTab.All);
        add.Flyout = new MenuFlyout { Items = { addExe, openCatalog } };
        titleRow.Children.Add(add);
        left.Children.Add(titleRow); left.Children.Add(chips);
        header.Children.Add(left);
        Grid.SetColumn(sort, 1); sort.VerticalAlignment = VerticalAlignment.Top; header.Children.Add(sort);
        Grid.SetRow(header, 1); libraryPage.Children.Add(header);
        var gridHost = new StackPanel(); gridHost.Children.Add(grid); gridHost.Children.Add(empty);
        var scroll = new ScrollViewer { Content = gridHost, Padding = new Thickness(20, 0, 20, 20) };
        Grid.SetRow(scroll, 2); libraryPage.Children.Add(scroll);

        catalogView = new CatalogView(this);
        page.Content = libraryPage;
        var footer = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 8), Child = status };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(top); Grid.SetRow(page, 1); root.Children.Add(page); Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;

        sort.SelectedIndex = Math.Max(0, Array.IndexOf(SortModes, Library.SortMode));
        sort.SelectionChanged += (_, _) => { if (sort.SelectedIndex >= 0 && SortModes[sort.SelectedIndex] != Library.SortMode) Commit(() => Library.SortMode = SortModes[sort.SelectedIndex]); };
        search.TextChanged += (_, _) => { if (page.Content == libraryPage) RefreshGrid(); else catalogView.SetSearch(search.Text ?? ""); };
        KeyDown += OnKey;
        selected = Library.Games.OrderByDescending(g => g.LastPlayed ?? "").FirstOrDefault();
        Refresh();
        status.Text = "Dados: " + Store.Root + "  ·  NEXUS " + AppInfo.Version + " para Linux";
        Opened += (_, _) => { if (Program.SmokePath is null) _ = Startup(); else DispatcherTimer.RunOnce(Smoke, TimeSpan.FromMilliseconds(900)); };
    }

    internal void SetStatus(string text) => status.Text = text;
    internal CancellationToken Token => shutdown.Token;

    void Try(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F) { search.Focus(); e.Handled = true; return; }
        if (page.Content != libraryPage || search.IsFocused) return;
        var visible = Visible();
        int index = selected is null ? -1 : visible.IndexOf(selected);
        int columns = Math.Max(1, (int)((grid.Bounds.Width) / 172));
        int next = e.Key switch { Key.Right => index + 1, Key.Left => index - 1, Key.Down => index + columns, Key.Up => index - columns, _ => int.MinValue };
        if (next != int.MinValue && visible.Count > 0) { Select(visible[Math.Clamp(next, 0, visible.Count - 1)]); e.Handled = true; }
        else if (e.Key == Key.Enter && selected is not null) { Start(false); e.Handled = true; }
        else if (e.Key == Key.Escape) { search.Text = ""; platformFilter = ""; favoritesOnly = false; Refresh(); e.Handled = true; }
    }

    async Task Startup()
    {
        try
        {
            if (!Program.Offline && await Catalog.RefreshFromRemote(Store.Root, Token)) catalogView.Reload();
            catalogView.UpdateBadges();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        if (!Program.Offline && Library.AutoUpdateLauncher) _ = CheckLauncher();
        if (!Program.Offline && Library.OnlineArtwork)
            foreach (var g in Library.Games.Where(g => string.IsNullOrEmpty(g.Cover)).ToList()) await FetchCover(g, false);
    }

    LauncherUpdate? pendingUpdate;
    async Task CheckLauncher()
    {
        try
        {
            pendingUpdate = await SelfUpdate.Check(Catalog.Http, Token);
            if (pendingUpdate is null) return;
            launcherUpdate.Content = "Atualizar para " + pendingUpdate.Version;
            launcherUpdate.IsVisible = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    async Task UpdateLauncher()
    {
        if (pendingUpdate is null) return;
        string? appImage = SelfUpdate.RunningAppImage();
        if (appImage is null || !Installer.Writable(Path.GetDirectoryName(appImage)!))
        {
            Try(() => Launch.OpenUrl("https://github.com/teasergames/ports-pc/releases/latest"));
            status.Text = "Baixe o novo AppImage na página de Releases; esta cópia não pode ser substituída automaticamente.";
            return;
        }
        if (!await Dialogs.Confirm(this, "Baixar o NEXUS " + pendingUpdate.Version + ", conferir o SHA-256 e substituir este AppImage? O NEXUS reabre em seguida; a biblioteca é preservada.", "Atualizar")) return;
        launcherUpdate.IsEnabled = false;
        try
        {
            await SelfUpdate.Apply(Catalog.Http, pendingUpdate, appImage, new Progress<double>(p => status.Text = "Baixando NEXUS " + pendingUpdate.Version + " · " + (int)p + "%"), Token);
            Process.Start(new ProcessStartInfo(appImage) { UseShellExecute = false })?.Dispose();
            Close();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { status.Text = ex.Message; launcherUpdate.IsEnabled = true; }
    }

    internal async Task FetchCover(Game game, bool manual)
    {
        if (!coverRequests.Add(game.Id)) return;
        string title = !string.IsNullOrWhiteSpace(game.ArtworkSearchTitle) ? game.ArtworkSearchTitle! : Native.Catalog.CanonicalTitle(game.Name);
        try
        {
            if (manual) status.Text = "Buscando capa de " + game.Name + "…";
            var found = await Covers.Find(Catalog.Http, title, Store.Covers, game.Id, Token);
            if (found is { } f) { Commit(() => { game.Cover = f.File; game.CoverSource = f.Source; }); if (manual) status.Text = "Capa: " + f.Source; }
            else if (manual) status.Text = "Nenhuma capa encontrada para " + title + ". Escolha uma imagem em Editar cadastro.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { if (manual) status.Text = "Capa: " + ex.Message; }
    }

    internal void ShowLibrary()
    {
        page.Content = libraryPage; navLibrary.Classes.Add("active"); navCatalog.Classes.Remove("active");
        search.Watermark = "Buscar na biblioteca"; search.Text = ""; Refresh();
    }

    internal void ShowCatalog(CatalogTab tab, PortEntry? entry = null)
    {
        page.Content = catalogView; navCatalog.Classes.Add("active"); navLibrary.Classes.Remove("active");
        search.Watermark = "Buscar no catálogo"; search.Text = "";
        catalogView.Open(tab, entry);
    }

    internal void Commit(Action action)
    {
        // Restore the in-memory model if persistence fails.
        string previous = JsonSerializer.Serialize(Library);
        try { action(); Store.Save(Library); }
        catch (Exception ex) { Library = Store.Parse(previous); selected = Library.Games.FirstOrDefault(g => g.Id == selected?.Id); status.Text = ex.Message; }
        Refresh();
    }

    internal void AddOrReplace(Game game)
    {
        Commit(() =>
        {
            int i = Library.Games.FindIndex(g => g.Id == game.Id);
            if (i < 0) Library.Games.Add(game); else Library.Games[i] = game;
            selected = game;
        });
        if (Library.OnlineArtwork && string.IsNullOrEmpty(game.Cover)) _ = FetchCover(game, false);
    }

    List<Game> Visible()
    {
        string text = search.Text?.Trim() ?? "";
        IEnumerable<Game> items = Library.Games.Where(g => (!favoritesOnly || g.Favorite) && (platformFilter == "" || g.Platform == platformFilter)
            && (text == "" || g.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)));
        items = Library.SortMode switch
        {
            "name" => items.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
            "recent" => items.OrderByDescending(g => g.LastPlayed ?? "").ThenBy(g => g.Name),
            "platform" => items.OrderBy(g => g.Platform).ThenBy(g => g.Name),
            _ => items.OrderByDescending(g => g.Favorite).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return items.ToList();
    }

    void Refresh()
    {
        if (selected is not null && !Library.Games.Contains(selected)) selected = Library.Games.FirstOrDefault(g => g.Id == selected.Id);
        RefreshChips(); RefreshGrid(); RefreshHero();
    }

    void RefreshChips()
    {
        chips.Children.Clear();
        var fav = new ToggleButton { Content = "★ Favoritos", IsChecked = favoritesOnly, Classes = { "chip" } };
        fav.Click += (_, _) => { favoritesOnly = fav.IsChecked == true; RefreshGrid(); };
        chips.Children.Add(fav);
        var all = new ToggleButton { Content = "Todas", IsChecked = platformFilter == "", Classes = { "chip" } };
        all.Click += (_, _) => { platformFilter = ""; RefreshChips(); RefreshGrid(); };
        chips.Children.Add(all);
        foreach (var group in Library.Games.GroupBy(g => g.Platform).OrderBy(g => Array.IndexOf(Platforms.All, g.Key)))
        {
            string platform = group.Key;
            var chip = new ToggleButton { Content = platform + "  " + group.Count(), IsChecked = platformFilter == platform, Classes = { "chip" } };
            chip.Click += (_, _) => { platformFilter = platformFilter == platform ? "" : platform; RefreshChips(); RefreshGrid(); };
            chips.Children.Add(chip);
        }
    }

    void RefreshGrid()
    {
        grid.Children.Clear();
        var visible = Visible();
        foreach (var game in visible) grid.Children.Add(Card(game));
        empty.IsVisible = visible.Count == 0;
        empty.Text = Library.Games.Count == 0 ? "Sua biblioteca está vazia. Use Adicionar jogo ou Baixar ports." : "Nenhum jogo com estes filtros.";
    }

    Bitmap? Thumbnail(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if (thumbnails.TryGetValue(path, out var cached)) return cached;
        try { using var stream = File.OpenRead(path); var bitmap = Bitmap.DecodeToWidth(stream, 320); thumbnails[path] = bitmap; return bitmap; }
        catch (Exception) { return null; }
    }

    Control Card(Game game)
    {
        var art = new Grid();
        if (Thumbnail(game.Cover ?? game.Artwork) is { } bitmap) art.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill });
        else
        {
            string initials = string.Concat(Native.Catalog.CanonicalTitle(game.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])));
            art.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#2C313B"), 0), new GradientStop(Color.Parse("#1A1D24"), 1) } } });
            art.Children.Add(new TextBlock { Text = initials, FontSize = 38, FontWeight = FontWeight.Bold, Foreground = Ui.TextMuted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        if (game.Favorite) art.Children.Add(new TextBlock { Text = "★", Foreground = Ui.Favorite, FontSize = 18, Margin = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Right });
        bool isSelected = selected == game;
        var frame = new Border { Width = 148, Height = 222, CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = art, BorderThickness = new Thickness(isSelected ? 3 : 0), BorderBrush = Ui.Accent };
        var stack = new StackPanel { Width = 148 };
        stack.Children.Add(frame);
        stack.Children.Add(new TextBlock { Text = game.Name, Foreground = isSelected ? Ui.Text : Ui.TextSoft, FontSize = 13, FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal, Margin = new Thickness(2, 8, 2, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = game.Platform, Foreground = Ui.TextMuted, FontSize = 11, Margin = new Thickness(2, 2, 2, 0) });
        var button = new Button { Content = stack, Padding = new Thickness(6), Margin = new Thickness(4, 4, 4, 10), Classes = { "card" }, Tag = game };
        ToolTip.SetTip(button, game.Name);
        button.Click += (_, _) => Select(game);
        button.DoubleTapped += (_, _) => { Select(game); Start(false); };
        return button;
    }

    void Select(Game game) { selected = game; RefreshGrid(); RefreshHero(); }

    void RefreshHero()
    {
        bool has = selected is not null;
        heroTitle.Text = has ? selected!.Name : "Seu próximo universo";
        heroBadges.Children.Clear();
        if (has)
        {
            heroBadges.Children.Add(Ui.Badge(selected!.Platform, Ui.AccentSoft, Ui.AccentSoftText));
            if (!string.IsNullOrEmpty(selected.InstalledVersion)) heroBadges.Children.Add(Ui.Badge(selected.InstalledVersion!, Ui.Control, Ui.TextSoft));
            if (selected.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) heroBadges.Children.Add(Ui.Badge("Windows · Wine", Ui.Control, Ui.Warning));
        }
        string meta = has ? (string.IsNullOrEmpty(selected!.CatalogId) ? "Adicionado manualmente" : "Instalado pelo catálogo") : "Adicione seus ports ou baixe builds Linux pelo catálogo.";
        if (has && DateTimeOffset.TryParse(selected!.LastPlayed, out var last)) meta += "   ·   Última abertura: " + last.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        heroMeta.Text = meta;
        heroCover.Source = has ? Thumbnail(selected!.Cover ?? selected.Artwork) : null;
        play.IsEnabled = configure.IsEnabled = favorite.IsEnabled = more.IsEnabled = has;
        configure.IsEnabled = has && !string.IsNullOrWhiteSpace(selected!.ConfigurationExecutable);
        favorite.Content = has && selected!.Favorite ? "★ Favorito" : "☆ Favoritar";
        bool managed = has && !string.IsNullOrEmpty(selected!.CatalogId);
        moreUpdate.IsEnabled = moreUninstall.IsEnabled = managed;
        moreFolder.IsEnabled = has;
    }

    void Start(bool configureMode)
    {
        if (selected is not { } game) return;
        try
        {
            using var process = Process.Start(Launch.Prepare(game, configureMode)) ?? throw new IOException("O sistema não iniciou o processo.");
            if (!configureMode) Commit(() => game.LastPlayed = DateTimeOffset.Now.ToString("O"));
            status.Text = (configureMode ? "Configuração aberta: " : "Iniciando: ") + game.Name;
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    async Task Edit(Game? original)
    {
        var result = await new GameEditor(this, original).ShowDialog<Game?>(this);
        if (result is not null) AddOrReplace(result);
    }

    async Task Uninstall()
    {
        if (selected is not { } game || string.IsNullOrEmpty(game.ManagedInstallationRoot)) return;
        if (!await Dialogs.Confirm(this, "Apagar a pasta " + game.ManagedInstallationRoot + " e remover " + game.Name + " da biblioteca?\n\nSaves e configurações guardados dentro da pasta do port também serão apagados.", "Desinstalar")) return;
        try { Installer.Uninstall(game); Commit(() => Library.Games.Remove(game)); status.Text = game.Name + " foi desinstalado."; }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    void Smoke()
    {
        try
        {
            if (Program.SmokeMode == "--catalog-smoke") { ShowCatalog(CatalogTab.All); catalogView.SmokeChecks(); }
            DispatcherTimer.RunOnce(async () =>
            {
                try
                {
                    if (Program.SmokeMode == "--editor-smoke")
                    {
                        var editor = new GameEditor(this, selected); editor.Show(this);
                        await Task.Delay(500); Capture(editor); editor.Close();
                    }
                    else Capture(this);
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Program.Failed = true; }
                Close();
            }, TimeSpan.FromMilliseconds(600));
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Program.Failed = true; Close(); }
    }

    static void Capture(Window window)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
        bitmap.Render(window);
        bitmap.Save(Program.SmokePath!);
        Console.WriteLine("PASS " + Program.SmokeMode + " " + Program.SmokePath);
    }
}
