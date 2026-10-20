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
    readonly CheckBox acceptBeta;readonly Button importZip;readonly ComboBox platform;readonly Action<Game> added;readonly List<Game> games;CancellationTokenSource request,installation;PortEntry selected;PortRelease release;bool busy,closed;
    readonly SolidColorBrush muted=new SolidColorBrush(Color.FromRgb(167,178,194));
    public CatalogWindow(Window owner,string dataRoot,List<Game> library,Action<Game> onAdded){
      Owner=owner;Title="Baixar ports · NEXUS";Width=1060;Height=760;MinWidth=880;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;FontSize=14;Resources=owner.Resources;games=library;added=onAdded;
      var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
      var header=new StackPanel{Margin=new Thickness(0,0,0,22)};header.Children.Add(new TextBlock{Text="Baixar ports",FontSize=27,FontWeight=FontWeights.SemiBold});header.Children.Add(new TextBlock{Text="Ports por plataforma, com downloads e projetos em desenvolvimento.",Foreground=muted,Margin=new Thickness(0,8,0,0)});root.Children.Add(header);
      platform=new ComboBox{ItemsSource=Platforms.All,SelectedIndex=0,Foreground=Brushes.Black,Padding=new Thickness(8),Width=240,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,16,0,0)};System.Windows.Automation.AutomationProperties.SetName(platform,"Plataforma do catálogo");header.Children.Add(platform);
      var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(310)});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(columns,1);root.Children.Add(columns);
      list=new ListBox{Background=new SolidColorBrush(Color.FromRgb(23,26,32)),Foreground=Foreground,BorderBrush=new SolidColorBrush(Color.FromRgb(48,54,65)),Padding=new Thickness(5),Margin=new Thickness(0,0,24,0)};
      ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,11,8,11)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));var trigger=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(55,73,43))));trigger.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));style.Triggers.Add(trigger);list.ItemContainerStyle=style;
      columns.Children.Add(list);
      var scroll=new ScrollViewer();Grid.SetColumn(scroll,1);columns.Children.Add(scroll);var right=new StackPanel{Margin=new Thickness(0,0,8,0)};scroll.Content=right;
      title=new TextBlock{Text="Escolha um port",FontSize=25,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};right.Children.Add(title);
      repo=new TextBlock{Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,16)};right.Children.Add(repo);
      version=new TextBlock{FontSize=17,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};right.Children.Add(version);
      details=new TextBlock{Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)};right.Children.Add(details);
      var links=new WrapPanel{Margin=new Thickness(0,0,0,22)};right.Children.Add(links);var project=new Button{Content="Abrir projeto"};project.Click+=delegate{if(selected!=null)Open(selected.ProjectUrl);};links.Children.Add(project);refresh=new Button{Content="Consultar novamente"};refresh.Click+=delegate{LoadSelected(true);};links.Children.Add(refresh);
      right.Children.Add(new TextBlock{Text="Pasta de instalação",Margin=new Thickness(0,0,0,8)});destination=new TextBox{Text=Path.Combine(dataRoot,"ports"),Margin=new Thickness(0,0,0,10)};right.Children.Add(destination);choose=new Button{Content="Escolher pasta…",HorizontalAlignment=HorizontalAlignment.Left};right.Children.Add(choose);
      choose.Click+=delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Escolha a pasta onde o NEXUS instalará os ports",SelectedPath=Directory.Exists(destination.Text)?destination.Text:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}){if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)destination.Text=picker.SelectedPath;}};
      right.Children.Add(new TextBlock{Text="Baixa apenas o port, sem arquivos do jogo ou firmware. A preparação usa seus próprios arquivos. Consulte as instruções acima; alguns ports têm opções apenas dentro do jogo.",Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,22,0,18),FontSize=12});
      acceptBeta=new CheckBox{Content="Aceito instalar esta versão beta / experimental",Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,14)};acceptBeta.Checked+=delegate{UpdateInstallState();};acceptBeta.Unchecked+=delegate{UpdateInstallState();};right.Children.Add(acceptBeta);
      var actions=new WrapPanel();right.Children.Add(actions);download=new Button{Content="Baixar e adicionar",IsEnabled=false,Background=new SolidColorBrush(Color.FromRgb(180,241,118)),Foreground=Brushes.Black};cancel=new Button{Content="Cancelar download",IsEnabled=false};importZip=new Button{Content="Importar ZIP e adicionar",Visibility=Visibility.Collapsed};importZip.Click+=delegate{var picker=new Microsoft.Win32.OpenFileDialog{Title="Importar o ZIP oficial do Azimuth",Filter="Pacote Azimuth|azimuth-windows*.zip|ZIP|*.zip",CheckFileExists=true};if(picker.ShowDialog(this)==true)InstallSelected(picker.FileName);};actions.Children.Add(download);actions.Children.Add(importZip);actions.Children.Add(cancel);download.Click+=delegate{InstallSelected();};cancel.Click+=delegate{if(installation!=null)installation.Cancel();};
      progress=new ProgressBar{Minimum=0,Maximum=100,Height=6,Margin=new Thickness(0,18,0,0),Foreground=new SolidColorBrush(Color.FromRgb(180,241,118)),Background=new SolidColorBrush(Color.FromRgb(38,43,51)),BorderThickness=new Thickness(0)};right.Children.Add(progress);
      status=new TextBlock{Text="Consulte uma versão para começar.",Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,0)};Grid.SetRow(status,2);root.Children.Add(status);
      platform.SelectionChanged+=delegate{if(!busy)FilterEntries();};
      FilterEntries();
      list.SelectionChanged+=delegate{if(!busy&&!IsSmoke)LoadSelected();};Closed+=delegate{closed=true;if(request!=null)request.Cancel();if(installation!=null)installation.Cancel();catalog.Dispose();};
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;status.Text="Use Cancelar download e aguarde a limpeza antes de fechar.";}};
      Loaded+=delegate{if(!IsSmoke){if(list.SelectedIndex<0)list.SelectedIndex=0;else LoadSelected();}};
    }
    void FilterEntries(){
      if(list==null)return;if(request!=null){request.Cancel();request=null;}selected=null;release=null;list.Items.Clear();
      foreach(var entry in catalog.Entries.Where(e=>platform.SelectedIndex==0||e.Platform==(string)platform.SelectedItem)){
        var item=new ListBoxItem{Tag=entry};var content=new StackPanel();content.Children.Add(new TextBlock{Text=entry.Name,TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text=entry.Platform+" · "+(entry.ManualPackage?"ZIP do site oficial":entry.ProjectOnly?"Ver projeto":entry.AllowPrerelease?"Beta / recompilação":entry.Kind),Foreground=muted,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});item.Content=content;list.Items.Add(item);
      }
      if(download==null)return;download.IsEnabled=false;title.Text="Escolha um port";repo.Text="";details.Text="";acceptBeta.Visibility=Visibility.Collapsed;importZip.Visibility=Visibility.Collapsed;download.Visibility=Visibility.Visible;version.Text="";status.Text=list.Items.Count==0?"Nenhum port Windows com instalação validada para esta plataforma. Você pode adicionar sua instalação manualmente na coleção.":list.Items.Count+" projetos nesta plataforma.";
      if(list.Items.Count>0&&!IsSmoke)list.SelectedIndex=0;
    }
    public bool IsSmoke;
    void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}
    async void LoadSelected(bool force=false){
      if(list.SelectedItem==null)return;if(request!=null){request.Cancel();request.Dispose();}request=new CancellationTokenSource();var current=request;
      selected=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;PortEntry entry=selected;release=null;title.Text=entry.Name;repo.Text=entry.Platform+" · "+entry.Kind+"\n"+entry.Identity;acceptBeta.Visibility=Visibility.Collapsed;acceptBeta.IsChecked=false;importZip.Visibility=entry.ManualPackage?Visibility.Visible:Visibility.Collapsed;download.Visibility=Visibility.Visible;download.Content=entry.ManualPackage?"Baixar no site oficial":"Baixar e adicionar";details.Text=entry.Notes;version.Text="Consultando versão…";download.IsEnabled=false;status.Text="Consultando Releases do GitHub…";progress.Value=0;
      if(entry.ManualPackage){version.Text="Download oficial no itch.io · importar ZIP";download.IsEnabled=true;importZip.IsEnabled=true;status.Text="Baixe azimuth-windows.zip no site oficial. Depois use Importar ZIP e adicionar; o NEXUS extrai e cadastra o Azimuth.";return;}
      if(entry.ProjectOnly){version.Text="Instalação manual / em desenvolvimento";status.Text="Use Abrir projeto para instruções e disponibilidade. Depois de preparar o port, use Adicionar jogo na coleção.";return;}
      try{var found=await catalog.Latest(entry,current.Token,force);if(closed||current!=request)return;PresentRelease(entry,found);
      }catch(OperationCanceledException){if(current==request&&!closed){version.Text="Consulta cancelada ou tempo esgotado.";status.Text="Use Consultar novamente para tentar outra vez.";}}
      catch(Exception ex){if(current==request&&!closed){version.Text="Download indisponível";status.Text=ex.Message;}}
    }
    void PresentRelease(PortEntry entry,PortRelease found){
      release=found;version.Text=found.Version+" · "+(found.Size/1048576.0).ToString("0.0")+" MB";details.Text=entry.Notes+"\n\nPacote: "+found.AssetName+"\nSubpasta: "+entry.Id+"-"+PortCatalog.VersionFolder(found.Version)+"\n"+(String.IsNullOrEmpty(found.Digest)?"O projeto não publicou SHA-256 nesta release.":"SHA-256 publicado pelo GitHub será verificado.");
        if(found.IsPrerelease){version.Text+=" · BETA";acceptBeta.Visibility=Visibility.Visible;}
        Game installed=games.FirstOrDefault(g=>g.Repository==entry.Identity&&g.InstalledVersion==found.Version&&File.Exists(g.Executable));
        UpdateInstallState();download.Content=installed==null?"Baixar e adicionar":"Já instalado";
        status.Text=installed==null?"Pronto. O pacote será extraído em uma nova pasta e adicionado à coleção.":"Esta versão já está na biblioteca. Para configurar, volte à coleção.";
        if(installed==null&&games.Any(g=>g.Repository==entry.Identity))status.Text="Esta versão será instalada em uma pasta separada. Saves e ajustes da instalação anterior não são migrados automaticamente.";
        if(found.FromCache){version.Text+=" · cache";DateTime when;string date=DateTime.TryParse(found.CheckedUtc,out when)?when.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):found.CheckedUtc;status.Text="GitHub indisponível ou limite de consultas: exibindo versão verificada em "+date+". Pode haver uma versão mais recente. "+status.Text;}
    }
    void UpdateInstallState(){
      if(download==null||selected==null)return;if(selected.ManualPackage){download.IsEnabled=!busy;return;}
      download.IsEnabled=!busy&&release!=null&&(!release.IsPrerelease||acceptBeta.IsChecked==true)&&!games.Any(g=>g.Repository==selected.Identity&&g.InstalledVersion==release.Version&&File.Exists(g.Executable));
    }
    async void InstallSelected(string localZip=null){
      if(selected==null||busy)return;if(selected.ManualPackage&&localZip==null){Open(selected.ProjectUrl);return;}if(!selected.ManualPackage&&(release==null||release.IsPrerelease&&acceptBeta.IsChecked!=true))return;PortEntry entry=selected;PortRelease package=release;string parent=destination.Text.Trim();
      if(String.IsNullOrWhiteSpace(parent)||!Path.IsPathRooted(parent)){status.Text="Escolha uma pasta de instalação com caminho completo.";return;}
      busy=true;importZip.IsEnabled=false;acceptBeta.IsEnabled=false;platform.IsEnabled=false;list.IsEnabled=false;choose.IsEnabled=false;destination.IsEnabled=false;refresh.IsEnabled=false;download.IsEnabled=false;cancel.IsEnabled=true;installation=new CancellationTokenSource();
      try{
        var progressReporter=new Progress<InstallProgress>(p=>{if(!closed){status.Text=p.Message;progress.Value=p.Percent;}});
        Game game=localZip==null?await catalog.Install(entry,package,parent,progressReporter,installation.Token,acceptBeta.IsChecked==true):await catalog.ImportZip(entry,localZip,parent,progressReporter,installation.Token);added(game);status.Text="Instalado: "+entry.Name+(entry.ConfigNames.Length>0?". Use Configurar jogo para preparar seus arquivos.":". Abra Jogar e siga as instruções de preparação do projeto.");download.Content="Já instalado";
      }catch(OperationCanceledException){status.Text="Download cancelado. Arquivos temporários removidos.";}
      catch(Exception ex){status.Text="Não foi possível instalar: "+ex.Message;}
      finally{busy=false;importZip.IsEnabled=true;acceptBeta.IsEnabled=true;platform.IsEnabled=true;list.IsEnabled=true;choose.IsEnabled=true;destination.IsEnabled=true;refresh.IsEnabled=true;cancel.IsEnabled=false;UpdateInstallState();installation.Dispose();installation=null;}
    }
    public void CaptureSmoke(string path){
      foreach(string name in Platforms.All){platform.SelectedItem=name;int expected=catalog.Entries.Count(e=>name==Platforms.All[0]||e.Platform==name);if(list.Items.Count!=expected)throw new Exception("Catalog platform filter failed");}
      platform.SelectedItem="PlayStation 3";list.SelectedIndex=0;LoadSelected();if(download.IsEnabled||release!=null)throw new Exception("Project-only download must remain disabled");
      platform.SelectedItem="Xbox";list.SelectedItem=list.Items.Cast<ListBoxItem>().First(i=>((PortEntry)i.Tag).Id=="darkwatch");selected=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;acceptBeta.IsChecked=false;var sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-stable";PresentRelease(selected,sample);
      if(acceptBeta.Visibility!=Visibility.Collapsed||!download.IsEnabled)throw new Exception("Stable Darker Edition profile must not require beta acceptance");
      platform.SelectedItem="Xbox One";list.SelectedIndex=0;LoadSelected();if(!importZip.IsVisible||!download.IsEnabled||acceptBeta.IsChecked==true)throw new Exception("Official-site import UI failed");
      Launcher.CaptureWindow(this,path);}
  }
}
