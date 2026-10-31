using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexus {
  // Where catalog installs go by default: a "ports" folder next to Nexus.exe, created on demand.
  // A read-only install location (for example under Program Files) falls back to the data folder.
  public static class InstallLocation {
    public static string AppFolder { get { return Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location)); } }

    public static string DefaultFolder(string dataRoot) { return DefaultFolder(dataRoot, AppFolder); }
    public static string DefaultFolder(string dataRoot, string appFolder) {
      string beside = Path.Combine(appFolder, "ports");
      return Writable(beside) ? beside : Path.Combine(dataRoot, "ports");
    }

    public static bool Writable(string folder) {
      try {
        Directory.CreateDirectory(PortCatalog.FsPath(folder));
        string probe = Path.Combine(folder, ".nexus-write-test-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(PortCatalog.FsPath(probe), "");
        File.Delete(PortCatalog.FsPath(probe));
        return true;
      } catch (Exception) { return false; }
    }

    // Free space shown next to each option, or an empty string when the drive cannot be read.
    public static string FreeSpace(string folder) {
      try {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder)));
        return (drive.AvailableFreeSpace / 1073741824.0).ToString("0.0") + " GB livres";
      } catch (Exception) { return ""; }
    }
  }

  // Asked before every new installation from the catalog: the default folder or one the user picks.
  public class InstallLocationWindow : Window {
    public string Result;
    readonly RadioButton useDefault, useOther;
    readonly TextBlock otherPath, otherSpace, error;
    string picked;

    public InstallLocationWindow(Window owner, string portName, string defaultFolder) {
      Owner = owner; Title = "Onde instalar · NEXUS"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
      WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = owner.Background; Foreground = owner.Foreground; FontFamily = owner.FontFamily; FontSize = 14; Resources = owner.Resources;
      var muted = (Brush)Application.Current.Resources["TextMuted"];
      var panel = new StackPanel { Margin = new Thickness(26) }; Content = panel;
      panel.Children.Add(new TextBlock { Text = "Onde deseja instalar?", FontSize = 22, FontWeight = FontWeights.SemiBold });
      panel.Children.Add(new TextBlock { Text = portName, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 18) });

      useDefault = new RadioButton { GroupName = "location", IsChecked = true, Margin = new Thickness(0, 0, 0, 12), Foreground = Foreground };
      useDefault.Content = Option("Pasta padrão do NEXUS", defaultFolder, InstallLocation.FreeSpace(defaultFolder), null, null);
      panel.Children.Add(useDefault);

      useOther = new RadioButton { GroupName = "location", Margin = new Thickness(0, 0, 0, 6), Foreground = Foreground };
      otherPath = new TextBlock(); otherSpace = new TextBlock();
      useOther.Content = Option("Outra pasta", "Nenhuma pasta escolhida", "", otherPath, otherSpace);
      panel.Children.Add(useOther);
      var browse = new Button { Content = "Escolher pasta…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(26, 4, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
      browse.Click += delegate { Browse(defaultFolder); };
      panel.Children.Add(browse);

      panel.Children.Add(new TextBlock { Text = "Cada versão é instalada em uma subpasta própria. Os arquivos do jogo continuam onde você os guarda.", Foreground = muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0) });
      error = new TextBlock { Foreground = (Brush)Application.Current.Resources["Danger"], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
      panel.Children.Add(error);

      var actions = new WrapPanel { Margin = new Thickness(0, 22, 0, 0) }; panel.Children.Add(actions);
      var install = new Button { Content = "Instalar aqui", Style = (Style)Application.Current.Resources["PrimaryButton"], IsDefault = true };
      var cancel = new Button { Content = "Cancelar", IsCancel = true };
      actions.Children.Add(install); actions.Children.Add(cancel);
      install.Click += delegate { Confirm(defaultFolder); };
    }

    static StackPanel Option(string heading, string path, string space, TextBlock pathBlock, TextBlock spaceBlock) {
      var stack = new StackPanel { Margin = new Thickness(6, -2, 0, 0) };
      stack.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeights.SemiBold });
      var p = pathBlock ?? new TextBlock(); p.Text = path; p.FontSize = 12; p.TextWrapping = TextWrapping.Wrap; p.Foreground = (Brush)Application.Current.Resources["TextSoft"]; p.Margin = new Thickness(0, 3, 0, 0); stack.Children.Add(p);
      var s = spaceBlock ?? new TextBlock(); s.Text = space; s.FontSize = 12; s.Foreground = (Brush)Application.Current.Resources["TextMuted"]; stack.Children.Add(s);
      return stack;
    }

    void Browse(string start) {
      using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Escolha a pasta onde o NEXUS instalará este port", SelectedPath = picked ?? (Directory.Exists(start) ? start : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) }) {
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        picked = dialog.SelectedPath; otherPath.Text = picked; otherSpace.Text = InstallLocation.FreeSpace(picked); useOther.IsChecked = true; error.Visibility = Visibility.Collapsed;
      }
    }

    void Confirm(string defaultFolder) {
      string folder = useOther.IsChecked == true ? picked : defaultFolder;
      if (String.IsNullOrWhiteSpace(folder)) { Fail("Escolha a pasta antes de continuar."); return; }
      if (!Path.IsPathRooted(folder)) { Fail("Use uma pasta com caminho completo."); return; }
      if (!InstallLocation.Writable(folder)) { Fail("O NEXUS não consegue gravar nesta pasta. Escolha outra ou execute o NEXUS de uma pasta gravável."); return; }
      Result = Path.GetFullPath(folder); DialogResult = true;
    }

    void Fail(string message) { error.Text = message; error.Visibility = Visibility.Visible; }

    // Smoke test: selects the second option so both states are covered before the capture.
    public void PreviewOther(string folder) { picked = folder; otherPath.Text = folder; otherSpace.Text = InstallLocation.FreeSpace(folder); useOther.IsChecked = true; }
  }
}
