using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Nexus.Native;

namespace Nexus.Desktop;

public static class Dialogs
{
    public static async Task<bool> Confirm(Window owner, string message, string action)
    {
        var dialog = new Window { Title = "NEXUS", Width = 520, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ui.Surface };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Text, FontSize = 14 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("Cancelar", () => dialog.Close(false)));
        buttons.Children.Add(Ui.Button(action, () => dialog.Close(true), "accent"));
        panel.Children.Add(buttons);
        dialog.Content = panel;
        return await dialog.ShowDialog<bool>(owner);
    }
}

// Lets the user pick the main program (and optionally the settings program) of an installed package.
public sealed class ExecutableChooser : Window
{
    public ExecutableChooser(string root, string[] executables)
    {
        Title = "Escolha o programa do port"; Width = 640; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Ui.Surface;
        string Rel(string p) => Path.GetRelativePath(root, p);
        var playList = new ListBox { ItemsSource = executables.Select(Rel).ToList(), SelectedIndex = 0, MaxHeight = 220 };
        var configBox = new ComboBox { ItemsSource = new[] { "(nenhum)" }.Concat(executables.Select(Rel)).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "O pacote tem mais de um programa. Escolha o que abre o jogo ou o launcher do port.", TextWrapping = TextWrapping.Wrap, Foreground = Ui.Text });
        panel.Children.Add(Ui.Label("Jogar"));
        panel.Children.Add(playList);
        panel.Children.Add(Ui.Label("Configurar (opcional)"));
        panel.Children.Add(configBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(Ui.Button("Cancelar", () => Close(null)));
        buttons.Children.Add(Ui.Button("Adicionar à biblioteca", () =>
        {
            if (playList.SelectedIndex < 0) return;
            string? config = configBox.SelectedIndex > 0 ? executables[configBox.SelectedIndex - 1] : null;
            Close(((string Play, string? Config)?)(executables[playList.SelectedIndex], config));
        }, "accent"));
        panel.Children.Add(buttons);
        Content = panel;
    }
}

public sealed class GameEditor : Window
{
    public GameEditor(MainWindow main, Game? original)
    {
        Title = original is null ? "Adicionar jogo" : "Editar cadastro"; Width = 680; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Ui.Surface;
        var form = new StackPanel { Margin = new Thickness(24), Spacing = 6 };
        TextBox Field(string label, string? value)
        {
            form.Children.Add(new TextBlock { Text = label, Foreground = Ui.TextSoft, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });
            var t = new TextBox { Text = value ?? "" }; form.Children.Add(t); return t;
        }
        TextBox PathField(string label, string? value, string pickerTitle, bool image = false)
        {
            form.Children.Add(new TextBlock { Text = label, Foreground = Ui.TextSoft, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var t = new TextBox { Text = value ?? "" }; row.Children.Add(t);
            var pick = Ui.Button("Escolher…", async () =>
            {
                var options = new FilePickerOpenOptions { Title = pickerTitle, AllowMultiple = false };
                if (image) options.FileTypeFilter = new[] { new FilePickerFileType("Imagens") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } } };
                var files = await StorageProvider.OpenFilePickerAsync(options);
                if (files.FirstOrDefault()?.TryGetLocalPath() is { } p) t.Text = p;
            });
            pick.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(pick, 1); row.Children.Add(pick);
            form.Children.Add(row); return t;
        }
        var name = Field("Nome", original?.Name);
        var exe = PathField("Executável Linux, AppImage ou .exe (Wine/Proton)", original?.Executable, "Executável do jogo");
        exe.TextChanged += (_, _) => { if (string.IsNullOrWhiteSpace(name.Text) && !string.IsNullOrWhiteSpace(exe.Text)) name.Text = Path.GetFileNameWithoutExtension(exe.Text); };
        var args = Field("Argumentos (sem comandos de shell)", original?.Arguments);
        var cwd = Field("Pasta de trabalho (vazia = pasta do executável)", original?.WorkingDirectory);
        form.Children.Add(new TextBlock { Text = "Plataforma", Foreground = Ui.TextSoft, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) });
        var platform = new ComboBox { ItemsSource = Platforms.All, SelectedItem = original?.Platform ?? "Outras", HorizontalAlignment = HorizontalAlignment.Stretch };
        form.Children.Add(platform);
        var config = PathField("Executável de configuração (opcional)", original?.ConfigurationExecutable, "Programa de configuração");
        var configArgs = Field("Argumentos de configuração", original?.ConfigurationArguments);
        var cover = PathField("Capa (imagem 2:3, opcional; vazia = buscar a capa oficial)", original?.Cover, "Capa do jogo", true);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Danger };
        form.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(Ui.Button("Cancelar", () => Close(null)));
        buttons.Children.Add(Ui.Button("Salvar", () =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidDataException("Informe o nome.");
                var game = original is null ? new Game() : JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(original))!;
                game.Name = name.Text.Trim(); game.Executable = exe.Text?.Trim() ?? ""; game.Arguments = args.Text ?? ""; game.WorkingDirectory = cwd.Text?.Trim() ?? "";
                game.Platform = platform.SelectedItem as string ?? "Outras";
                game.ConfigurationExecutable = config.Text?.Trim() ?? ""; game.ConfigurationArguments = configArgs.Text ?? "";
                string coverPath = cover.Text?.Trim() ?? "";
                if (coverPath != (original?.Cover ?? ""))
                {
                    if (coverPath == "") { game.Cover = null; game.CoverSource = null; }
                    else
                    {
                        using (var image = new Bitmap(coverPath)) if (image.PixelSize.Width > 16000 || image.PixelSize.Height > 16000) throw new InvalidDataException("Imagem muito grande.");
                        Directory.CreateDirectory(main.Store.Covers);
                        foreach (string old in Directory.GetFiles(main.Store.Covers, game.Id + ".*")) File.Delete(old);
                        string copy = Path.Combine(main.Store.Covers, game.Id + Path.GetExtension(coverPath).ToLowerInvariant());
                        File.Copy(coverPath, copy, true);
                        game.Cover = copy; game.CoverSource = "Capa escolhida por você";
                    }
                }
                if (string.IsNullOrWhiteSpace(game.Executable)) throw new InvalidDataException("Informe o executável.");
                if (!File.Exists(game.Executable)) throw new FileNotFoundException("Executável não encontrado: " + game.Executable);
                if (!game.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Launch.Prepare(game);
                if (!string.IsNullOrWhiteSpace(game.ConfigurationExecutable) && !File.Exists(game.ConfigurationExecutable)) throw new FileNotFoundException("Programa de configuração não encontrado.");
                Close(game);
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }, "accent"));
        form.Children.Add(buttons);
        Content = new ScrollViewer { Content = form };
    }
}

public sealed class SettingsDialog : Window
{
    public SettingsDialog(MainWindow main)
    {
        Title = "Configurações"; Width = 600; SizeToContent = SizeToContent.Height; CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Ui.Surface;
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
        var covers = new CheckBox { Content = "Buscar capas oficiais automaticamente (Wikipedia)", IsChecked = main.Library.OnlineArtwork };
        var updates = new CheckBox { Content = "Avisar quando houver uma nova versão do NEXUS", IsChecked = main.Library.AutoUpdateLauncher };
        panel.Children.Add(Ui.Label("Biblioteca"));
        panel.Children.Add(covers); panel.Children.Add(updates);
        panel.Children.Add(Ui.Label("Pastas"));
        string ports = string.IsNullOrWhiteSpace(main.Library.InstallFolder) ? Installer.DefaultFolder(main.Store.Root) + " (padrão)" : main.Library.InstallFolder!;
        panel.Children.Add(new TextBlock { Text = "Dados e biblioteca: " + main.Store.Root + "\nPorts do catálogo: " + ports, Foreground = Ui.TextSoft, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(Ui.Button("Abrir pasta de dados", () => { try { Launch.OpenFolder(main.Store.Root); } catch (Exception) { } }));
        if (!string.IsNullOrWhiteSpace(main.Library.InstallFolder)) row.Children.Add(Ui.Button("Voltar à pasta padrão dos ports", () => { main.Commit(() => main.Library.InstallFolder = null); Close(); }));
        panel.Children.Add(row);
        string runner = Launch.WindowsRunner() is { } r ? "Programas Windows (.exe) abrem com: " + r : "Nenhum Wine ou umu-run encontrado: programas Windows (.exe) não podem ser abertos.";
        panel.Children.Add(Ui.Label("Programas Windows"));
        panel.Children.Add(new TextBlock { Text = runner, Foreground = Ui.TextSoft, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        panel.Children.Add(new TextBlock { Text = "NEXUS " + AppInfo.Version + " · Linux", Foreground = Ui.TextMuted, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("Cancelar", Close));
        buttons.Children.Add(Ui.Button("Salvar", () => { main.Commit(() => { main.Library.OnlineArtwork = covers.IsChecked == true; main.Library.AutoUpdateLauncher = updates.IsChecked == true; }); Close(); }, "accent"));
        panel.Children.Add(buttons);
        Content = panel;
    }
}
