using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexus {
  public class CatalogWindow:Window {
    readonly PortCatalog catalog=new PortCatalog();readonly ListBox list;readonly TextBlock title,repo,version,details,status;readonly TextBox destination;readonly Button download,choose,cancel,refresh;readonly ProgressBar progress;
    readonly Action<Game> added;readonly List<Game> games;CancellationTokenSource request,installation;PortEntry selected;PortRelease release;bool busy,closed;
    readonly SolidColorBrush muted=new SolidColorBrush(Color.FromRgb(167,178,194));
    public CatalogWindow(Window owner,string dataRoot,List<Game> library,Action<Game> onAdded){
      Owner=owner;Title="Baixar ports · NEXUS";Width=1060;Height=760;MinWidth=880;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;FontSize=14;Resources=owner.Resources;games=library;added=onAdded;
      var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
      var header=new StackPanel{Margin=new Thickness(0,0,0,22)};header.Children.Add(new TextBlock{Text="Baixar ports",FontSize=27,FontWeight=FontWeights.SemiBold});header.Children.Add(new TextBlock{Text="Última versão estável, direto das Releases de cada projeto.",Foreground=muted,Margin=new Thickness(0,8,0,0)});root.Children.Add(header);
      var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(310)});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(columns,1);root.Children.Add(columns);
      list=new ListBox{Background=new SolidColorBrush(Color.FromRgb(23,26,32)),Foreground=Foreground,BorderBrush=new SolidColorBrush(Color.FromRgb(48,54,65)),Padding=new Thickness(5),Margin=new Thickness(0,0,24,0)};
      ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,11,8,11)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));var trigger=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(55,73,43))));trigger.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));style.Triggers.Add(trigger);list.ItemContainerStyle=style;
      foreach(var entry in catalog.Entries){var item=new ListBoxItem{Tag=entry};var content=new StackPanel();content.Children.Add(new TextBlock{Text=entry.Name,TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text=entry.Repository,Foreground=muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});item.Content=content;list.Items.Add(item);}columns.Children.Add(list);
      var scroll=new ScrollViewer();Grid.SetColumn(scroll,1);columns.Children.Add(scroll);var right=new StackPanel{Margin=new Thickness(0,0,8,0)};scroll.Content=right;
      title=new TextBlock{Text="Escolha um port",FontSize=25,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};right.Children.Add(title);
      repo=new TextBlock{Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,16)};right.Children.Add(repo);
      version=new TextBlock{FontSize=17,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};right.Children.Add(version);
      details=new TextBlock{Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)};right.Children.Add(details);
      var links=new WrapPanel{Margin=new Thickness(0,0,0,22)};right.Children.Add(links);var project=new Button{Content="Abrir projeto"};project.Click+=delegate{if(selected!=null)Open("https://github.com/"+selected.Repository);};links.Children.Add(project);refresh=new Button{Content="Consultar novamente"};refresh.Click+=delegate{LoadSelected(true);};links.Children.Add(refresh);
      right.Children.Add(new TextBlock{Text="Pasta de instalação",Margin=new Thickness(0,0,0,8)});destination=new TextBox{Text=Path.Combine(dataRoot,"ports"),Margin=new Thickness(0,0,0,10)};right.Children.Add(destination);choose=new Button{Content="Escolher pasta…",HorizontalAlignment=HorizontalAlignment.Left};right.Children.Add(choose);
      choose.Click+=delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Escolha a pasta onde o NEXUS instalará os ports",SelectedPath=Directory.Exists(destination.Text)?destination.Text:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}){if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)destination.Text=picker.SelectedPath;}};
      right.Children.Add(new TextBlock{Text="Baixa apenas o port, sem arquivos do jogo ou firmware. Depois da instalação, use Configurar jogo no NEXUS para indicar seus arquivos.",Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,22,0,18),FontSize=12});
      var actions=new WrapPanel();right.Children.Add(actions);download=new Button{Content="Baixar e adicionar",IsEnabled=false,Background=new SolidColorBrush(Color.FromRgb(180,241,118)),Foreground=Brushes.Black};cancel=new Button{Content="Cancelar download",IsEnabled=false};actions.Children.Add(download);actions.Children.Add(cancel);download.Click+=delegate{InstallSelected();};cancel.Click+=delegate{if(installation!=null)installation.Cancel();};
      progress=new ProgressBar{Minimum=0,Maximum=100,Height=6,Margin=new Thickness(0,18,0,0),Foreground=new SolidColorBrush(Color.FromRgb(180,241,118)),Background=new SolidColorBrush(Color.FromRgb(38,43,51)),BorderThickness=new Thickness(0)};right.Children.Add(progress);
      status=new TextBlock{Text="Consulte uma versão para começar.",Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,0)};Grid.SetRow(status,2);root.Children.Add(status);
      list.SelectionChanged+=delegate{if(!busy&&!IsSmoke)LoadSelected();};Closed+=delegate{closed=true;if(request!=null)request.Cancel();if(installation!=null)installation.Cancel();catalog.Dispose();};
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;status.Text="Use Cancelar download e aguarde a limpeza antes de fechar.";}};
      Loaded+=delegate{if(!IsSmoke)list.SelectedIndex=0;};
    }
    public bool IsSmoke;
    void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}
    async void LoadSelected(bool force=false){
      if(list.SelectedItem==null)return;if(request!=null){request.Cancel();request.Dispose();}request=new CancellationTokenSource();var current=request;
      selected=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;PortEntry entry=selected;release=null;title.Text=entry.Name;repo.Text=entry.Repository;details.Text=entry.Notes;version.Text="Consultando versão…";download.IsEnabled=false;status.Text="Consultando Releases do GitHub…";progress.Value=0;
      try{var found=await catalog.Latest(entry,current.Token,force);if(closed||current!=request)return;release=found;version.Text=found.Version+" · "+(found.Size/1048576.0).ToString("0.0")+" MB";details.Text=entry.Notes+"\n\nPacote: "+found.AssetName+"\nSubpasta: "+entry.Id+"-"+PortCatalog.VersionFolder(found.Version)+"\n"+(String.IsNullOrEmpty(found.Digest)?"O projeto não publicou SHA-256 nesta release.":"SHA-256 publicado pelo GitHub será verificado.");
        Game installed=games.FirstOrDefault(g=>g.Repository==entry.Repository&&g.InstalledVersion==found.Version&&File.Exists(g.Executable));
        download.IsEnabled=installed==null;download.Content=installed==null?"Baixar e adicionar":"Já instalado";
        status.Text=installed==null?"Pronto. O pacote será extraído em uma nova pasta e adicionado à coleção.":"Esta versão já está na biblioteca. Para configurar, volte à coleção.";
        if(installed==null&&games.Any(g=>g.Repository==entry.Repository))status.Text="Esta versão será instalada em uma pasta separada. Saves e ajustes da instalação anterior não são migrados automaticamente.";
        if(found.FromCache){version.Text+=" · cache";DateTime when;string date=DateTime.TryParse(found.CheckedUtc,out when)?when.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):found.CheckedUtc;status.Text="GitHub indisponível ou limite de consultas: exibindo versão verificada em "+date+". Pode haver uma versão mais recente. "+status.Text;}
      }catch(OperationCanceledException){if(current==request&&!closed){version.Text="Consulta cancelada ou tempo esgotado.";status.Text="Use Consultar novamente para tentar outra vez.";}}
      catch(Exception ex){if(current==request&&!closed){version.Text="Download indisponível";status.Text=ex.Message;}}
    }
    async void InstallSelected(){
      if(selected==null||release==null||busy)return;PortEntry entry=selected;PortRelease package=release;string parent=destination.Text.Trim();
      if(String.IsNullOrWhiteSpace(parent)||!Path.IsPathRooted(parent)){status.Text="Escolha uma pasta de instalação com caminho completo.";return;}
      busy=true;list.IsEnabled=false;choose.IsEnabled=false;destination.IsEnabled=false;refresh.IsEnabled=false;download.IsEnabled=false;cancel.IsEnabled=true;installation=new CancellationTokenSource();
      try{
        var progressReporter=new Progress<InstallProgress>(p=>{if(!closed){status.Text=p.Message;progress.Value=p.Percent;}});
        Game game=await catalog.Install(entry,package,parent,progressReporter,installation.Token);added(game);status.Text="Instalado: "+entry.Name+". Volte à coleção e use Configurar jogo para concluir a preparação.";download.Content="Já instalado";
      }catch(OperationCanceledException){status.Text="Download cancelado. Arquivos temporários removidos.";}
      catch(Exception ex){status.Text="Não foi possível instalar: "+ex.Message;}
      finally{busy=false;list.IsEnabled=true;choose.IsEnabled=true;destination.IsEnabled=true;refresh.IsEnabled=true;cancel.IsEnabled=false;download.IsEnabled=!games.Any(g=>g.Repository==entry.Repository&&g.InstalledVersion==package.Version&&File.Exists(g.Executable));installation.Dispose();installation=null;}
    }
    public void CaptureSmoke(string path){list.SelectedIndex=0;title.Text=catalog.Entries[0].Name;repo.Text=catalog.Entries[0].Repository;version.Text="Última versão estável · consulta ao abrir";details.Text=catalog.Entries[0].Notes;Launcher.CaptureWindow(this,path);}
  }
}
