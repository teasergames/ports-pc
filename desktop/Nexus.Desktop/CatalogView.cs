using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Nexus.Native;

namespace Nexus.Desktop;

public enum CatalogTab { All, News, Updates }

public sealed class CatalogView : Grid
{
    readonly MainWindow main;
    readonly ListBox platforms = new() { Background = Brushes.Transparent };
    readonly CheckBox linuxOnly = new() { Content = "Só com build Linux", IsChecked = true, Margin = new Thickness(0, 14, 0, 0) };
    readonly ListBox list = new() { Background = Brushes.Transparent };
    readonly ToggleButton tabAll = new() { Content = "Todos", Classes = { "chip" } }, tabNews = new() { Content = "Novidades", Classes = { "chip" } }, tabUpdates = new() { Content = "Atualizações", Classes = { "chip" } };
    readonly TextBlock count = new() { Foreground = Ui.TextMuted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 8) };
    // Detail panel
    readonly TextBlock title = new() { FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Ui.Text, TextWrapping = TextWrapping.Wrap };
    readonly StackPanel badges = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
    readonly TextBlock notes = new() { Foreground = Ui.TextSoft, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 20 };
    readonly TextBlock version = new() { Foreground = Ui.Text, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
    readonly TextBlock destination = new() { Foreground = Ui.TextSoft, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    readonly Button install, project, choose;
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 6, IsVisible = false, Margin = new Thickness(0, 12, 0, 0) };
    readonly TextBlock detailStatus = new() { Foreground = Ui.TextMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    readonly StackPanel detail = new();
    CatalogTab tab = CatalogTab.All;
    string platform = "", searchText = "";
    PortEntry? current;
    ReleaseAsset? currentRelease;
    bool busy;
    readonly Dictionary<string, string> latestVersions = new();

    public CatalogView(MainWindow owner)
    {
        main = owner;
        ColumnDefinitions = new ColumnDefinitions("230,*,380");
        Margin = new Thickness(24, 0, 24, 16);

        var side = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        side.Children.Add(Ui.Label("Plataforma"));
        side.Children.Add(new ScrollViewer { Content = platforms, MaxHeight = 520 });
        side.Children.Add(linuxOnly);
        side.Children.Add(new TextBlock { Text = "Builds Linux são baixadas e instaladas pelo NEXUS. Os demais projetos publicam só pacotes Windows: abra o projeto e adicione o .exe à biblioteca para rodá-lo com Wine ou Proton.", Foreground = Ui.TextMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        Children.Add(side);

        var middle = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        tabs.Children.Add(tabAll); tabs.Children.Add(tabNews); tabs.Children.Add(tabUpdates); tabs.Children.Add(count);
        middle.Children.Add(tabs);
        var listFrame = new Border { Background = Ui.Surface, CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Child = list };
        Grid.SetRow(listFrame, 1); middle.Children.Add(listFrame);
        Grid.SetColumn(middle, 1); Children.Add(middle);

        install = Ui.Button("Baixar e adicionar", () => _ = Install(), "accent");
        project = Ui.Button("Abrir projeto", () => { if (current is not null) Try(() => Launch.OpenUrl(current.ProjectUrl)); });
        choose = Ui.Button("Alterar…", () => _ = ChooseFolder());
        detail.Children.Add(title); detail.Children.Add(badges); detail.Children.Add(notes); detail.Children.Add(version);
        var dest = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        dest.Children.Add(Ui.Label("Pasta de instalação"));
        var destRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        destRow.Children.Add(destination); Grid.SetColumn(choose, 1); destRow.Children.Add(choose);
        dest.Children.Add(destRow);
        detail.Children.Add(dest);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(install); buttons.Children.Add(project);
        detail.Children.Add(buttons); detail.Children.Add(progress); detail.Children.Add(detailStatus);
        var detailFrame = new Border { Background = Ui.Surface, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(16, 0, 0, 0), Child = new ScrollViewer { Content = detail } };
        Grid.SetColumn(detailFrame, 2); Children.Add(detailFrame);

        tabAll.Click += (_, _) => SetTab(CatalogTab.All); tabNews.Click += (_, _) => SetTab(CatalogTab.News); tabUpdates.Click += (_, _) => SetTab(CatalogTab.Updates);
        platforms.SelectionChanged += (_, _) => { if (platforms.SelectedItem is ListBoxItem { Tag: string p } && p != platform) { platform = p; RefreshList(); } };
        linuxOnly.IsCheckedChanged += (_, _) => { RefreshPlatforms(); RefreshList(); };
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is ListBoxItem { Tag: PortEntry e }) ShowDetail(e); };
        Reload();
    }

    void Try(Action action) { try { action(); } catch (Exception ex) { detailStatus.Text = ex.Message; } }

    IEnumerable<PortEntry> Entries => main.Catalog.Entries.Where(main.Catalog.Supported);

    Game? Installed(PortEntry e) => main.Library.Games.FirstOrDefault(g => g.CatalogId == e.Id && !string.IsNullOrEmpty(g.ManagedInstallationRoot));

    public void Reload() { RefreshPlatforms(); RefreshList(); UpdateBadges(); }

    public void UpdateBadges()
    {
        int news = Entries.Count(e => !main.Library.SeenCatalogIds.Contains(e.Id) && (linuxOnly.IsChecked != true || e.HasLinux));
        tabNews.Content = news > 0 ? "Novidades  " + news : "Novidades";
        int updates = UpdatesAvailable().Count();
        tabUpdates.Content = updates > 0 ? "Atualizações  " + updates : "Atualizações";
    }

    IEnumerable<PortEntry> UpdatesAvailable() => Entries.Where(e =>
    {
        var g = Installed(e);
        if (g is null) return false;
        string? latest = latestVersions.TryGetValue(e.Id, out var v) ? v : main.Catalog.CachedLinux(e)?.Version;
        return latest is not null && latest != g.InstalledVersion;
    });

    public void Open(CatalogTab next, PortEntry? entry)
    {
        if (entry is not null) { platform = ""; searchText = ""; SetTab(CatalogTab.All); if (!entry.HasLinux) linuxOnly.IsChecked = false; }
        else SetTab(next);
        RefreshPlatforms(); RefreshList();
        if (entry is not null) foreach (var item in list.Items.OfType<ListBoxItem>()) if (item.Tag == entry) { list.SelectedItem = item; list.ScrollIntoView(item); }
        if (next == CatalogTab.Updates) _ = CheckUpdates();
    }

    public void SetSearch(string text) { searchText = text.Trim(); RefreshList(); }

    void SetTab(CatalogTab next)
    {
        tab = next;
        tabAll.IsChecked = tab == CatalogTab.All; tabNews.IsChecked = tab == CatalogTab.News; tabUpdates.IsChecked = tab == CatalogTab.Updates;
        RefreshList();
        if (tab == CatalogTab.Updates) _ = CheckUpdates();
    }

    void RefreshPlatforms()
    {
        var source = Entries.Where(e => linuxOnly.IsChecked != true || e.HasLinux).ToList();
        var items = new List<ListBoxItem> { Row("Todas as plataformas", "", source.Count) };
        foreach (string p in Platforms.All) { int n = source.Count(e => e.Platform == p); if (n > 0) items.Add(Row(p, p, n)); }
        platforms.ItemsSource = items;
        platforms.SelectedItem = items.FirstOrDefault(i => (string)i.Tag! == platform) ?? items[0];
        static ListBoxItem Row(string label, string tag, int n)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            g.Children.Add(new TextBlock { Text = label, FontSize = 13 });
            var c = new TextBlock { Text = n.ToString(), Foreground = Ui.TextMuted, FontSize = 12 }; Grid.SetColumn(c, 1); g.Children.Add(c);
            return new ListBoxItem { Tag = tag, Content = g, Padding = new Thickness(10, 6) };
        }
    }

    void RefreshList()
    {
        var source = Entries.Where(e => (linuxOnly.IsChecked != true || e.HasLinux || tab == CatalogTab.Updates) && (platform == "" || e.Platform == platform)
            && (searchText == "" || e.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || e.Repository.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        source = tab switch
        {
            CatalogTab.News => source.Where(e => !main.Library.SeenCatalogIds.Contains(e.Id)),
            CatalogTab.Updates => source.Where(e => Installed(e) is not null),
            _ => source
        };
        var entries = source.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var items = entries.Select(Item).ToList();
        list.ItemsSource = items;
        count.Text = entries.Count + (entries.Count == 1 ? " projeto" : " projetos");
        var keep = items.FirstOrDefault(i => i.Tag == current) ?? items.FirstOrDefault();
        list.SelectedItem = keep;
        if (keep is null) ShowDetail(null);
    }

    ListBoxItem Item(PortEntry e)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = e.Name, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Ui.Text, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = e.Platform + " · " + e.ProjectType + " · " + e.Repository, FontSize = 12, Foreground = Ui.TextMuted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });
        g.Children.Add(text);
        var tags = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (Installed(e) is not null) tags.Children.Add(Ui.Badge("Instalado", Ui.AccentSoft, Ui.AccentSoftText));
        tags.Children.Add(e.HasLinux ? Ui.Badge("Linux", Ui.Control, Ui.Accent) : Ui.Badge("Windows", Ui.Control, Ui.TextMuted));
        Grid.SetColumn(tags, 1); g.Children.Add(tags);
        return new ListBoxItem { Tag = e, Content = g, Padding = new Thickness(12, 9) };
    }

    string InstallParent => string.IsNullOrWhiteSpace(main.Library.InstallFolder) ? Installer.DefaultFolder(main.Store.Root) : main.Library.InstallFolder!;

    void ShowDetail(PortEntry? e)
    {
        current = e; currentRelease = null;
        detail.IsVisible = e is not null;
        if (e is null) return;
        if (!main.Library.SeenCatalogIds.Contains(e.Id)) { main.Library.SeenCatalogIds.Add(e.Id); try { main.Store.Save(main.Library); } catch (Exception) { } UpdateBadges(); }
        title.Text = e.Name;
        badges.Children.Clear();
        badges.Children.Add(Ui.Badge(e.Platform, Ui.AccentSoft, Ui.AccentSoftText));
        if (!string.IsNullOrEmpty(e.ProjectType)) badges.Children.Add(Ui.Badge(e.ProjectType, Ui.Control, Ui.TextSoft));
        badges.Children.Add(e.HasLinux ? Ui.Badge("Build Linux", Ui.Control, Ui.Accent) : Ui.Badge("Somente Windows", Ui.Control, Ui.Warning));
        notes.Text = e.HasLinux ? (e.LinuxNotes ?? "O NEXUS baixa o pacote Linux da última Release, confere o SHA-256 e o instala na pasta abaixo. Na primeira abertura, prepare seus próprios arquivos do jogo seguindo o port. ") + (string.IsNullOrEmpty(e.LinuxNotes) ? "\n\nNotas do perfil Windows: " + e.Notes : "")
            : e.Notes + "\n\nEste projeto não publica build Linux. Baixe o pacote Windows pelo projeto e adicione o .exe à biblioteca: o NEXUS abre programas Windows pelo umu-run (Proton) ou Wine quando estão instalados.";
        destination.Text = Installer.TargetFolder(InstallParent, e);
        choose.IsEnabled = e.HasLinux && !busy;
        var installed = Installed(e);
        install.IsVisible = e.HasLinux; install.IsEnabled = !busy;
        install.Content = installed is null ? "Baixar e adicionar" : "Atualizar / reinstalar";
        project.Content = e.HasLinux ? "Abrir projeto" : "Ver downloads oficiais";
        progress.IsVisible = false; detailStatus.Text = "";
        if (!e.HasLinux) { version.Text = "Instalação automática disponível só no Windows."; return; }
        var cached = main.Catalog.CachedLinux(e);
        version.Text = (installed is not null ? "Instalado: " + installed.InstalledVersion + "   ·   " : "") + (cached is not null ? "Última conferida: " + cached.Version : "Consultando versão…");
        _ = LoadRelease(e);
    }

    async Task LoadRelease(PortEntry e)
    {
        try
        {
            if (Program.Offline) { currentRelease = main.Catalog.CachedLinux(e); return; }
            var release = await main.Catalog.LatestLinux(e, main.Token);
            if (current != e) return;
            currentRelease = release; latestVersions[e.Id] = release.Version;
            var installed = Installed(e);
            version.Text = (installed is not null ? "Instalado: " + installed.InstalledVersion + "   ·   " : "") + "Última: " + release.Version + (release.IsPrerelease ? " (beta)" : "") + "   ·   " + release.AssetName + " · " + Math.Max(1, release.Size / 1048576) + " MB" + (release.FromCache ? "  (cache diário)" : "");
            if (installed is not null && installed.InstalledVersion == release.Version) install.Content = "Reinstalar " + release.Version;
            else if (installed is not null) install.Content = "Atualizar para " + release.Version;
            UpdateBadges();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { if (current == e) detailStatus.Text = ex.Message; }
    }

    async Task CheckUpdates()
    {
        if (Program.Offline) return;
        foreach (var e in Entries.Where(e => e.HasLinux && Installed(e) is not null).ToList())
        {
            try { latestVersions[e.Id] = (await main.Catalog.LatestLinux(e, main.Token)).Version; }
            catch (Exception ex) when (ex is not OperationCanceledException) { main.SetStatus(e.Name + ": " + ex.Message); break; }
        }
        UpdateBadges();
        if (tab == CatalogTab.Updates) RefreshList();
    }

    async Task ChooseFolder()
    {
        var folders = await main.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pasta onde os ports serão instalados", AllowMultiple = false });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        if (!Installer.Writable(path)) { detailStatus.Text = "Sem permissão de escrita em " + path; return; }
        main.Commit(() => main.Library.InstallFolder = path);
        if (current is not null) destination.Text = Installer.TargetFolder(InstallParent, current);
    }

    async Task Install()
    {
        if (current is not { } e || busy) return;
        busy = true; install.IsEnabled = false; choose.IsEnabled = false; progress.IsVisible = true; progress.Value = 0;
        try
        {
            var release = currentRelease ?? await main.Catalog.LatestLinux(e, main.Token);
            if (release.IsPrerelease && !await Dialogs.Confirm(main, release.Version + " é uma versão beta publicada pelo autor. Instalar mesmo assim?", "Instalar beta")) return;
            var previous = Installed(e);
            string parent = previous?.ManagedInstallationRoot is { } root ? Path.GetDirectoryName(root)! : InstallParent;
            var report = new Progress<InstallProgress>(p => { progress.Value = p.Percent; detailStatus.Text = p.Message; });
            var (folder, executables) = await Installer.Install(main.Catalog.Http, e, release, parent, report, main.Token);
            string? play = previous?.Executable is { } old && executables.Contains(old) ? old : null;
            string? config = previous?.ConfigurationExecutable is { Length: > 0 } oldConfig && executables.Contains(oldConfig) ? oldConfig : null;
            if (play is null)
            {
                if (executables.Length == 1 || (e.LinuxPlayNames?.Length > 0 && Path.GetFileName(executables[0]).Equals(e.LinuxPlayNames[0], StringComparison.OrdinalIgnoreCase))) play = executables[0];
                else
                {
                    var choice = await new ExecutableChooser(folder, executables).ShowDialog<(string Play, string? Config)?>(main);
                    if (choice is null) { detailStatus.Text = "Instalado em " + folder + ", mas nenhum programa foi escolhido. Use Adicionar jogo para cadastrá-lo."; return; }
                    (play, config) = choice.Value;
                }
            }
            var game = previous ?? new Game { Name = e.Name, Platform = e.Platform, ArtworkSearchTitle = e.CanonicalTitle };
            game.Executable = play; game.WorkingDirectory = Path.GetDirectoryName(play)!; game.ConfigurationExecutable = config ?? "";
            game.Repository = e.Repository; game.CatalogId = e.Id; game.InstalledVersion = release.Version; game.ManagedInstallationRoot = folder;
            var receipt = Installer.ReadReceipt(folder);
            if (receipt is not null) { receipt.Executable = Path.GetRelativePath(folder, play); Installer.WriteReceipt(folder, receipt); }
            main.AddOrReplace(game);
            latestVersions[e.Id] = release.Version;
            detailStatus.Text = (previous is null ? "Instalado e adicionado à biblioteca: " : "Atualizado para " + release.Version + ": ") + folder;
            main.SetStatus(e.Name + " " + release.Version + " pronto para jogar.");
            ShowDetail(e); detailStatus.Text = previous is null ? "Instalado e adicionado à biblioteca." : "Atualizado para " + release.Version + ".";
        }
        catch (OperationCanceledException) { detailStatus.Text = "Instalação cancelada."; }
        catch (Exception ex) { detailStatus.Text = ex.Message; }
        finally { busy = false; install.IsEnabled = true; choose.IsEnabled = current?.HasLinux == true; progress.IsVisible = false; UpdateBadges(); }
    }

    // Smoke checks run against the bundled catalog in --catalog-smoke.
    public void SmokeChecks()
    {
        if (!Entries.Any(e => e.HasLinux)) throw new Exception("catálogo sem perfis Linux");
        linuxOnly.IsChecked = false; RefreshPlatforms(); RefreshList();
        int all = list.Items.Count;
        linuxOnly.IsChecked = true; RefreshPlatforms(); RefreshList();
        if (list.Items.Count == 0 || list.Items.Count >= all) throw new Exception("filtro Linux não reduziu a lista");
        SetSearch("zzzz-nada"); if (list.Items.Count != 0) throw new Exception("busca sem resultado");
        SetSearch("");
    }
}
