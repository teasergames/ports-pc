using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexus {
  // "⋯ → Mover para outra pasta…": choose the destination, then move with progress.
  // On success the caller saves Result and calls PortMove.Finish(Plan); on a save failure, PortMove.Abort(Plan).
  public class PortMoveWindow : Window {
    public Game Result; public MovePlan Plan;
    readonly Game game; readonly List<Game> library; readonly string defaultFolder;
    readonly RadioButton useDefault, useOther; readonly TextBlock otherPath, status; readonly CheckBox backups;
    readonly ProgressBar progress; readonly Button move, cancel, browse;
    string picked; CancellationTokenSource running;

    public PortMoveWindow(Window owner, Game game, List<Game> library, string defaultFolder) {
      this.game = game; this.library = library; this.defaultFolder = defaultFolder;
      Owner = owner; Title = "Mover port · NEXUS"; Width = 640; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
      WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = owner.Background; Foreground = owner.Foreground; FontFamily = owner.FontFamily; FontSize = 14; Resources = owner.Resources;
      var muted = (Brush)Application.Current.Resources["TextMuted"]; var soft = (Brush)Application.Current.Resources["TextSoft"];
      var panel = new StackPanel { Margin = new Thickness(26) }; Content = panel;
      panel.Children.Add(new TextBlock { Text = "Mover para outra pasta", FontSize = 22, FontWeight = FontWeights.SemiBold });
      panel.Children.Add(new TextBlock { Text = game.Name, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 16) });

      string current = Path.GetFullPath(game.ManagedInstallationRoot);
      long size = FolderSize(current); var backupFolders = PortMove.Backups(game).ToList(); long backupSize = backupFolders.Sum(f => FolderSize(f));
      panel.Children.Add(new TextBlock { Text = "PASTA ATUAL", Style = (Style)Application.Current.Resources["SectionLabel"] });
      panel.Children.Add(new TextBlock { Text = current, Foreground = soft, FontSize = 12, TextWrapping = TextWrapping.Wrap });
      panel.Children.Add(new TextBlock { Text = Gb(size), Foreground = muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 16) });

      panel.Children.Add(new TextBlock { Text = "NOVA PASTA", Style = (Style)Application.Current.Resources["SectionLabel"] });
      useDefault = new RadioButton { GroupName = "target", IsChecked = true, Foreground = Foreground, Margin = new Thickness(0, 0, 0, 10) };
      useDefault.Content = Option("Pasta padrão do NEXUS", defaultFolder, InstallLocation.FreeSpace(defaultFolder), null);
      panel.Children.Add(useDefault);
      useOther = new RadioButton { GroupName = "target", Foreground = Foreground };
      otherPath = new TextBlock(); useOther.Content = Option("Outra pasta", "Nenhuma pasta escolhida", "", otherPath);
      panel.Children.Add(useOther);
      browse = new Button { Content = "Escolher pasta…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(26, 6, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
      browse.Click += delegate { Browse(); }; panel.Children.Add(browse);

      backups = new CheckBox { Content = "Mover também as cópias de versões anteriores (" + Gb(backupSize) + ")", IsChecked = true, Margin = new Thickness(0, 16, 0, 0), Visibility = backupFolders.Count > 0 ? Visibility.Visible : Visibility.Collapsed };
      panel.Children.Add(backups);
      panel.Children.Add(new TextBlock { Text = "Saves, configurações e arquivos preparados dentro da pasta do port vão junto. Na mesma unidade a mudança é instantânea; em outra unidade, os arquivos são copiados e conferidos antes de apagar a pasta antiga.", Foreground = muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });

      progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Margin = new Thickness(0, 16, 0, 0), Foreground = (Brush)Application.Current.Resources["Accent"], Background = new SolidColorBrush(Color.FromRgb(38, 43, 51)), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
      panel.Children.Add(progress);
      status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Foreground = soft, Visibility = Visibility.Collapsed };
      panel.Children.Add(status);

      var actions = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) }; panel.Children.Add(actions);
      move = new Button { Content = "Mover port", Style = (Style)Application.Current.Resources["PrimaryButton"], IsDefault = true };
      cancel = new Button { Content = "Cancelar", IsCancel = true };
      actions.Children.Add(move); actions.Children.Add(cancel);
      move.Click += delegate { Start(); };
      cancel.Click += delegate (object sender, RoutedEventArgs e) { if (running != null) { running.Cancel(); status.Text = "Cancelando e restaurando…"; e.Handled = true; } };
      Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e) { if (running != null) e.Cancel = true; };
    }

    static StackPanel Option(string heading, string path, string space, TextBlock pathBlock) {
      var stack = new StackPanel { Margin = new Thickness(6, -2, 0, 0) };
      stack.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeights.SemiBold });
      var p = pathBlock ?? new TextBlock(); p.Text = path; p.FontSize = 12; p.TextWrapping = TextWrapping.Wrap; p.Foreground = (Brush)Application.Current.Resources["TextSoft"]; p.Margin = new Thickness(0, 3, 0, 0); stack.Children.Add(p);
      if (space.Length > 0) stack.Children.Add(new TextBlock { Text = space, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextMuted"] });
      return stack;
    }
    static long FolderSize(string folder) { try { return Directory.GetFiles(PortCatalog.FsPath(folder), "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length); } catch (Exception) { return 0; } }
    static string Gb(long bytes) { return (bytes / 1073741824.0).ToString("0.0") + " GB"; }

    void Browse() {
      using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Escolha a pasta para onde o port será movido", SelectedPath = picked ?? (Directory.Exists(defaultFolder) ? defaultFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) }) {
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        picked = dialog.SelectedPath; otherPath.Text = picked + "   ·   " + InstallLocation.FreeSpace(picked); useOther.IsChecked = true;
      }
    }

    async void Start() {
      string target = useOther.IsChecked == true ? picked : defaultFolder;
      status.Visibility = Visibility.Visible; status.Foreground = (Brush)Application.Current.Resources["TextSoft"];
      if (String.IsNullOrWhiteSpace(target)) { Fail("Escolha a pasta antes de continuar."); return; }
      if (!InstallLocation.Writable(target)) { Fail("O NEXUS não consegue gravar nesta pasta. Escolha outra."); return; }
      MovePlan plan;
      try { plan = PortMove.Build(game, library, target, backups.IsChecked == true && backups.Visibility == Visibility.Visible); }
      catch (Exception ex) { Fail(ex.Message); return; }
      running = new CancellationTokenSource(); move.IsEnabled = false; browse.IsEnabled = false; useDefault.IsEnabled = false; useOther.IsEnabled = false; backups.IsEnabled = false;
      progress.Visibility = Visibility.Visible; progress.Value = 0; status.Text = plan.SameVolume ? "Movendo…" : "Preparando a cópia…";
      var reporter = new Progress<InstallProgress>(p => { status.Text = p.Message; progress.Value = p.Percent; });
      var token = running.Token;
      try {
        Result = await Task.Run(() => PortMove.Execute(plan, reporter, token));
        Plan = plan; running = null; DialogResult = true;
      } catch (OperationCanceledException) { running = null; Reset(); status.Text = "Mudança cancelada. O port continua na pasta original."; }
      catch (Exception ex) { running = null; Reset(); Fail(ex.Message + " O port continua na pasta original."); }
    }

    void Reset() { move.IsEnabled = true; browse.IsEnabled = true; useDefault.IsEnabled = true; useOther.IsEnabled = true; backups.IsEnabled = true; progress.Visibility = Visibility.Collapsed; }
    void Fail(string message) { status.Visibility = Visibility.Visible; status.Text = message; status.Foreground = (Brush)Application.Current.Resources["Danger"]; }
  }
}
