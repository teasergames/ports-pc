using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Automation;
using Microsoft.Win32;

[assembly: AssemblyTitle("NEXUS · Ports PC")]
[assembly: AssemblyProduct("NEXUS Ports PC")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
namespace Nexus {
  public static class Program {
    [STAThread] public static int Main(string[] args) {
      if(args.Contains("--self-test"))return Tests.Run();
      string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NexusPortsPC");
      int index=System.Array.IndexOf(args,"--data-dir");if(index>=0&&index+1<args.Length)root=Path.GetFullPath(args[index+1]);
      Store store=new Store(root);bool smoke=args.Contains("--smoke-test");
      using(var mutex=new Mutex(false,"Local\\NexusPortsPC-"+Ports.Normalize(root))){
        bool owns=false;try{try{owns=mutex.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
          if(!owns){MessageBox.Show("O NEXUS já está aberto.","NEXUS");return 0;}
          var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;
          app.DispatcherUnhandledException+=(sender,eventArgs)=>{store.Log(eventArgs.Exception.ToString());MessageBox.Show(eventArgs.Exception.Message,"Não foi possível concluir",MessageBoxButton.OK,MessageBoxImage.Warning);eventArgs.Handled=true;};
          var controller=new Launcher(store,smoke);app.MainWindow=controller.Window;
          if(smoke){index=System.Array.IndexOf(args,"--smoke-test");string target=index+1<args.Length?args[index+1]:Path.Combine(root,"preview.png");controller.Window.Loaded+=delegate{controller.Window.Dispatcher.BeginInvoke(new Action(delegate{controller.Capture(target);app.Shutdown();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);};}
          app.Run(controller.Window);return 0;
        }catch(Exception ex){store.Log(ex.ToString());if(smoke)File.WriteAllText(Path.Combine(root,"smoke-error.txt"),ex.ToString());else MessageBox.Show(ex.Message,"NEXUS",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
        finally{if(owns)mutex.ReleaseMutex();}
      }
    }
  }
  public class Launcher {
    public readonly Window Window;
    readonly Store store;readonly Library library;readonly ArtworkService artwork;readonly CancellationTokenSource shutdown=new CancellationTokenSource();
    readonly SemaphoreSlim artQueue=new SemaphoreSlim(1,1);readonly HashSet<string> pending=new HashSet<string>();
    Game selected;bool smoke;bool closing;
    T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public Launcher(Store data,bool test) {
      store=data;smoke=test;library=store.Load();artwork=new ArtworkService(store);
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.MainWindow.xaml")){Window=(Window)XamlReader.Load(stream);}
      Window.SourceInitialized+=delegate{int value=1;DwmSetWindowAttribute(new WindowInteropHelper(Window).Handle,20,ref value,4);};
      Window.Closed+=delegate{closing=true;shutdown.Cancel();artwork.Dispose();};
      Find<Button>("AddButton").Click+=delegate{Add();};Find<Button>("EditButton").Click+=delegate{Edit();};Find<Button>("PlayButton").Click+=delegate{Launch();};Find<Button>("FavoriteButton").Click+=delegate{if(selected!=null){selected.Favorite=!selected.Favorite;Save();Refresh();}};
      Find<Button>("ConfigureButton").Click+=delegate{if(selected==null)return;try{Process.Start(Ports.ConfigurationStartInfo(selected));Status("Configuração aberta: "+selected.Name);}catch(Exception ex){MessageBox.Show(Window,ex.Message,"Configurar jogo",MessageBoxButton.OK,MessageBoxImage.Information);}};
      Find<Button>("SettingsButton").Click+=delegate{Settings();};Find<Button>("DetectButton").Click+=delegate{Detect(true);};
      Find<TextBox>("SearchBox").TextChanged+=delegate{RefreshGrid();};Find<CheckBox>("FavoritesOnly").Checked+=delegate{RefreshGrid();};Find<CheckBox>("FavoritesOnly").Unchecked+=delegate{RefreshGrid();};
      if(!File.Exists(store.FilePath)&&!smoke)Detect(false);
      selected=library.Games.FirstOrDefault();Refresh();
      Window.Loaded+=delegate{if(!smoke){foreach(Game g in library.Games.ToArray())if(String.IsNullOrEmpty(g.Artwork)||!File.Exists(g.Artwork))ResolveArtwork(g);}};
    }
    void Save(){store.Save(library);}
    void Status(string text){if(!closing)Find<TextBlock>("StatusLabel").Text=text;}
    public void Refresh(){
      bool has=selected!=null;Find<Button>("PlayButton").IsEnabled=has;Find<Button>("EditButton").IsEnabled=has;Find<Button>("FavoriteButton").IsEnabled=has;
      Find<Button>("ConfigureButton").IsEnabled=has;
      Find<TextBlock>("HeroTitle").Text=has?selected.Name:"Seu próximo universo";
      Find<TextBlock>("HeroDescription").Text=!has?"Adicione seus ports e reúna suas aventuras em um só lugar.":selected.Name=="Bloodborne"?"As ruas de Yharnam estão à sua espera. Sua próxima caçada começa aqui.":selected.Name=="God of War III"?"Enfrente os deuses do Olimpo. Sua próxima aventura começa aqui.":selected.Name=="Shadow of the Colossus"?"Uma terra esquecida. Dezesseis colossos. Uma jornada para revisitar.":"Seu port, suas configurações. Pronto para a próxima aventura.";
      Find<Button>("FavoriteButton").Content=has&&selected.Favorite?"★ Favorito":"☆ Favoritar";
      string meta=has?"Port local · Windows":"Windows 11 · Biblioteca local";
      if(has&&!String.IsNullOrEmpty(selected.LastPlayed)){DateTime time;if(DateTime.TryParse(selected.LastPlayed,out time))meta+="   /   Última abertura: "+time.ToLocalTime().ToString("dd/MM/yyyy HH:mm");}
      Find<TextBlock>("HeroMeta").Text=meta;
      Find<Image>("Backdrop").Source=has?LoadImage(selected.Artwork):null;RefreshGrid();
    }
    static ImageSource LoadImage(string path){
      if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;
      try{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=1600;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();return image;}catch{return null;}
    }
    void RefreshGrid(){
      WrapPanel grid=Find<WrapPanel>("GameGrid");grid.Children.Clear();string search=Find<TextBox>("SearchBox").Text.Trim();bool favorite=Find<CheckBox>("FavoritesOnly").IsChecked==true;
      List<Game> visible=library.Games.Where(g=>(!favorite||g.Favorite)&&g.Name.IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0).OrderByDescending(g=>g.Favorite).ThenBy(g=>g.Name).ToList();
      foreach(Game g in visible){
        var card=new StackPanel();var art=new Grid{Width=220,Height=135,Background=new SolidColorBrush(Color.FromRgb(37,43,53))};ImageSource source=LoadImage(g.Artwork);
        if(source!=null)art.Children.Add(new Image{Source=source,Stretch=Stretch.UniformToFill});
        else {var blank=new TextBlock{Text="◈",Foreground=new SolidColorBrush(Color.FromRgb(180,241,118)),FontSize=45,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};art.Children.Add(blank);}
        var shade=new System.Windows.Shapes.Rectangle{Fill=new LinearGradientBrush(Color.FromArgb(0,16,17,21),Color.FromArgb(225,16,17,21),90)};art.Children.Add(shade);
        art.Children.Add(new TextBlock{Text=g.Name,TextWrapping=TextWrapping.Wrap,FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(14),VerticalAlignment=VerticalAlignment.Bottom});
        var border=new Border{CornerRadius=new CornerRadius(8),ClipToBounds=true,Child=art};card.Children.Add(border);
        card.Children.Add(new TextBlock{Text=g.Favorite?"★  Favorito · Port local":"Port local · Windows",Foreground=new SolidColorBrush(Color.FromRgb(158,168,184)),Margin=new Thickness(2,10,0,0),FontSize=12});
        var button=new Button{Content=card,Padding=new Thickness(4),Margin=new Thickness(0,0,17,18),BorderBrush=new SolidColorBrush(g==selected?Color.FromRgb(180,241,118):Color.FromRgb(48,53,63)),Background=Brushes.Transparent,ToolTip=g.Name};
        AutomationProperties.SetName(button,"Selecionar "+g.Name);Game current=g;button.Click+=delegate{selected=current;Refresh();};grid.Children.Add(button);
      }
      Find<TextBlock>("CountLabel").Text=library.Games.Count+" jogos";
      Find<TextBlock>("EmptyLabel").Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
      Find<TextBlock>("EmptyLabel").Text=library.Games.Count==0?"Sua coleção começa aqui. Adicione um executável ou detecte ports na pasta Downloads.":"Nenhum jogo corresponde à busca.";
    }
    void Add(){
      var picker=new OpenFileDialog{Title="Adicionar port",Filter="Executáveis e atalhos|*.exe;*.lnk",CheckFileExists=true};
      if(picker.ShowDialog(Window)!=true)return;
      if(library.Games.Any(g=>g.Executable.Equals(picker.FileName,StringComparison.OrdinalIgnoreCase))){Status("Este port já está na coleção.");return;}
      Game game=Ports.FromPath(picker.FileName);var editor=new GameEditor(Window,game,store,false);
      if(editor.ShowDialog()!=true)return;
      library.Games.Add(editor.Result);selected=editor.Result;Save();Refresh();Status("Port adicionado.");if(String.IsNullOrEmpty(selected.Artwork))ResolveArtwork(selected);
    }
    void Edit(){
      if(selected==null)return;Game original=selected;var editor=new GameEditor(Window,original.Copy(),store,true);
      if(editor.ShowDialog()!=true)return;
      if(editor.Remove){library.Games.Remove(original);selected=library.Games.FirstOrDefault();Save();Refresh();Status("Removido da coleção. Os arquivos do port foram preservados.");return;}
      int index=library.Games.IndexOf(original);library.Games[index]=editor.Result;selected=editor.Result;Save();Refresh();Status("Configurações salvas.");
      if(editor.RefreshArtwork||String.IsNullOrEmpty(selected.Artwork)||!File.Exists(selected.Artwork))ResolveArtwork(selected);
    }
    void Launch(){
      if(selected==null)return;
      try{Process.Start(Ports.StartInfo(selected));selected.LastPlayed=DateTime.UtcNow.ToString("o");Save();Refresh();Status("Inicialização solicitada: "+selected.Name);if(library.MinimizeOnLaunch)Window.WindowState=WindowState.Minimized;}
      catch(Exception ex){Status("Não foi possível abrir o port.");MessageBox.Show(Window,ex.Message,"Falha ao iniciar",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    void Detect(bool notify){
      string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");int count=0;
      foreach(Game g in Ports.Detect(downloads)){if(library.Games.Any(x=>x.Executable.Equals(g.Executable,StringComparison.OrdinalIgnoreCase)))continue;library.Games.Add(g);count++;}
      if(count>0){Save();if(selected==null)selected=library.Games[0];Refresh();}
      Status(count>0?count+" ports adicionados automaticamente.":"Nenhum port novo nos locais conhecidos de Downloads. Use Adicionar jogo para outras pastas.");
      if(notify)foreach(Game g in library.Games.ToArray())if(String.IsNullOrEmpty(g.Artwork)||!File.Exists(g.Artwork))ResolveArtwork(g);
    }
    async void ResolveArtwork(Game game){
      if(pending.Contains(game.Id))return;pending.Add(game.Id);Game snapshot=game.Copy();bool entered=false;
      try{
        await artQueue.WaitAsync(shutdown.Token);entered=true;if(closing)return;Status("Buscando artwork de "+snapshot.Name+"…");
        bool online=library.OnlineArtwork;string key=library.ApiKey();
        ArtworkResult result=await Task.Run(()=>artwork.Resolve(snapshot,online,key,shutdown.Token));
        if(closing)return;Game live=library.Games.FirstOrDefault(g=>g.Id==snapshot.Id);
        if(live==null||live.Artwork!=snapshot.Artwork||live.Name!=snapshot.Name||live.Executable!=snapshot.Executable||live.ArtworkSearchTitle!=snapshot.ArtworkSearchTitle)return;
        if(!String.IsNullOrEmpty(result.File)){live.Artwork=result.File;live.ArtworkSource=result.Source;Save();Refresh();Status("Artwork pronta: "+live.Name+" · "+result.Source);}else Status(result.Message);
      }catch(OperationCanceledException){}catch(Exception ex){if(!closing){store.Log(ex.ToString());Status("Não foi possível buscar a artwork. Você pode escolher uma imagem local.");}}
      finally{pending.Remove(game.Id);if(entered)artQueue.Release();}
    }
    void Settings(){
      var dialog=new Window{Title="Configurações · NEXUS",Owner=Window,Width=560,Height=470,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Window.Background,Foreground=Window.Foreground,FontFamily=Window.FontFamily};
      dialog.Resources=Window.Resources;var panel=new StackPanel{Margin=new Thickness(26)};dialog.Content=panel;
      panel.Children.Add(new TextBlock{Text="Artworks e biblioteca",FontSize=23,Margin=new Thickness(0,0,0,14)});
      var online=new CheckBox{Content="Buscar imagens online automaticamente",IsChecked=library.OnlineArtwork};panel.Children.Add(online);
      var minimize=new CheckBox{Content="Minimizar NEXUS ao abrir um jogo",IsChecked=library.MinimizeOnLaunch};panel.Children.Add(minimize);
      panel.Children.Add(new TextBlock{Text="SteamGridDB · chave de API opcional",Margin=new Thickness(0,14,0,8)});
      var key=new PasswordBox{Password=library.ApiKey(),Padding=new Thickness(10),Background=new SolidColorBrush(Color.FromRgb(29,32,39)),Foreground=Brushes.White};panel.Children.Add(key);
      panel.Children.Add(new TextBlock{Text="Sem chave: busca a imagem da página exata na Wikipedia. Com SteamGridDB: prioriza fundos panorâmicos. Só o nome do jogo é enviado; caminhos locais ficam no PC.",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(163,173,189)),Margin=new Thickness(0,12,0,18),FontSize=12});
      var link=new Button{Content="Obter chave do SteamGridDB"};link.Click+=delegate{Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api"){UseShellExecute=true});};panel.Children.Add(link);
      var actions=new WrapPanel{Margin=new Thickness(0,22,0,0)};panel.Children.Add(actions);var save=new Button{Content="Salvar",Background=new SolidColorBrush(Color.FromRgb(180,241,118)),Foreground=Brushes.Black};var folder=new Button{Content="Abrir dados locais"};actions.Children.Add(save);actions.Children.Add(folder);
      folder.Click+=delegate{Process.Start(new ProcessStartInfo("explorer.exe", "\""+store.Root+"\""){UseShellExecute=true});};
      save.Click+=delegate{library.OnlineArtwork=online.IsChecked==true;library.MinimizeOnLaunch=minimize.IsChecked==true;library.SetApiKey(key.Password);Save();dialog.DialogResult=true;};dialog.ShowDialog();
    }
    public void Capture(string target){CaptureWindow(Window,target);}
    public static void CaptureWindow(Window window,string target){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));using(var output=File.Create(target))encoder.Save(output);}
  }
  public class GameEditor:Window {
    public Game Result;public bool Remove;public bool RefreshArtwork;
    readonly TextBox name,exe,arguments,working,configExe,configArgs,artTitle;readonly Store store;readonly TextBlock artLabel;string picked;
    public GameEditor(Window owner,Game game,Store data,bool existing){
      Result=game;store=data;Owner=owner;Title=existing?"Editar cadastro · NEXUS":"Adicionar jogo · NEXUS";Width=700;Height=830;MinHeight=580;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var scroll=new ScrollViewer();Content=scroll;var p=new StackPanel{Margin=new Thickness(26)};scroll.Content=p;
      p.Children.Add(new TextBlock{Text=existing?"Seu jogo, do seu jeito":"Adicionar à coleção",FontSize=25,Margin=new Thickness(0,0,0,20)});
      name=Field(p,"Nome do jogo",game.Name);exe=Field(p,"Executável ou atalho (.exe / .lnk)",game.Executable);
      var browse=new Button{Content="Escolher executável",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12)};p.Children.Add(browse);
      browse.Click+=delegate{var pick=new OpenFileDialog{Filter="Executáveis e atalhos|*.exe;*.lnk",CheckFileExists=true};if(pick.ShowDialog(this)==true){exe.Text=pick.FileName;working.Text=Path.GetDirectoryName(pick.FileName);}};
      arguments=Field(p,"Argumentos de inicialização",game.Arguments);working=Field(p,"Pasta de trabalho",game.WorkingDirectory);
      configExe=Field(p,"Launcher de configuração (opcional)",Ports.FindConfigurationExecutable(game));
      var configBrowse=new Button{Content="Escolher launcher de configuração",HorizontalAlignment=HorizontalAlignment.Left};p.Children.Add(configBrowse);
      configBrowse.Click+=delegate{var pick=new OpenFileDialog{Filter="Executáveis e atalhos|*.exe;*.lnk",CheckFileExists=true};if(pick.ShowDialog(this)==true)configExe.Text=pick.FileName;};
      configArgs=Field(p,"Argumentos do launcher de configuração",game.ConfigurationArguments);
      artTitle=Field(p,"Título para buscar artwork (opcional)",game.ArtworkSearchTitle);
      p.Children.Add(new TextBlock{Text="Artwork",Margin=new Thickness(0,12,0,8)});
      artLabel=new TextBlock{Text=String.IsNullOrEmpty(game.Artwork)?"Busca automática ao adicionar o port.":game.ArtworkSource??"Imagem local",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(167,177,192)),FontSize=12,Margin=new Thickness(0,0,0,10)};p.Children.Add(artLabel);
      var artActions=new WrapPanel();p.Children.Add(artActions);var choose=new Button{Content="Escolher imagem"};var auto=new Button{Content="Buscar automaticamente"};artActions.Children.Add(choose);artActions.Children.Add(auto);
      choose.Click+=delegate{var pick=new OpenFileDialog{Filter="Imagens|*.jpg;*.jpeg;*.png;*.bmp",CheckFileExists=true};if(pick.ShowDialog(this)==true){picked=pick.FileName;RefreshArtwork=false;artLabel.Text="Imagem escolhida: "+Path.GetFileName(picked);}};
      auto.Click+=delegate{picked=null;RefreshArtwork=true;artLabel.Text="Uma nova busca será feita ao salvar.";};
      var actions=new WrapPanel{Margin=new Thickness(0,26,0,0)};p.Children.Add(actions);var save=new Button{Content="Salvar",Background=new SolidColorBrush(Color.FromRgb(180,241,118)),Foreground=Brushes.Black,IsDefault=true};var cancel=new Button{Content="Cancelar",IsCancel=true};actions.Children.Add(save);actions.Children.Add(cancel);
      if(existing){var remove=new Button{Content="Remover da coleção",Foreground=new SolidColorBrush(Color.FromRgb(255,170,170))};actions.Children.Add(remove);remove.Click+=delegate{if(MessageBox.Show(this,"Remover apenas o cadastro? Os arquivos do port serão preservados.","Remover jogo",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){Remove=true;DialogResult=true;}};}
      save.Click+=delegate{
        try{
          if(String.IsNullOrWhiteSpace(name.Text))throw new InvalidDataException("Informe o nome do jogo.");
          Game candidate=Result.Copy();candidate.Name=name.Text.Trim();candidate.Executable=exe.Text.Trim().Trim('"');candidate.Arguments=arguments.Text;candidate.WorkingDirectory=working.Text.Trim().Trim('"');Ports.StartInfo(candidate,false);
          candidate.ConfigurationExecutable=configExe.Text.Trim().Trim('"');candidate.ConfigurationArguments=configArgs.Text;candidate.ArtworkSearchTitle=artTitle.Text.Trim();
          if(!String.IsNullOrEmpty(candidate.ConfigurationExecutable))Ports.ConfigurationStartInfo(candidate);
          if(!String.IsNullOrEmpty(picked)){candidate.Artwork=store.ImportArtwork(picked,candidate.Id);candidate.ArtworkSource="Imagem escolhida por você";}
          Result=candidate;DialogResult=true;
        }catch(Exception ex){MessageBox.Show(this,ex.Message,"Confira o cadastro",MessageBoxButton.OK,MessageBoxImage.Warning);}
      };
    }
    static TextBox Field(StackPanel panel,string label,string value){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,7,0,7)});var field=new TextBox{Text=value??"",Margin=new Thickness(0,0,0,5)};AutomationProperties.SetName(field,label);panel.Children.Add(field);return field;}
  }
}
