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
[assembly: AssemblyVersion("1.3.2.0")]
[assembly: AssemblyFileVersion("1.3.2.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
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
          app.DispatcherUnhandledException+=(sender,eventArgs)=>{store.Log(eventArgs.Exception.ToString());if(smoke){File.WriteAllText(Path.Combine(root,"smoke-error.txt"),eventArgs.Exception.ToString());eventArgs.Handled=true;app.Shutdown(1);return;}MessageBox.Show(eventArgs.Exception.Message,"Não foi possível concluir",MessageBoxButton.OK,MessageBoxImage.Warning);eventArgs.Handled=true;};
          var controller=new Launcher(store,smoke);app.MainWindow=controller.Window;
          if(smoke){index=System.Array.IndexOf(args,"--smoke-test");string target=index+1<args.Length?args[index+1]:Path.Combine(root,"preview.png");int viewIndex=System.Array.IndexOf(args,"--preview-view"),sizeIndex=System.Array.IndexOf(args,"--preview-size");string view=viewIndex>=0&&viewIndex+1<args.Length?args[viewIndex+1]:null;int size=0;if(sizeIndex>=0&&sizeIndex+1<args.Length)Int32.TryParse(args[sizeIndex+1],out size);controller.Window.Loaded+=delegate{controller.Window.Dispatcher.BeginInvoke(new Action(delegate{if(view!=null)controller.PreviewView(view,size);controller.Capture(target);app.Shutdown();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);};}
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
    readonly Dictionary<string,ImageSource> imageCache=new Dictionary<string,ImageSource>();
    readonly System.Windows.Threading.DispatcherTimer viewSaveTimer=new System.Windows.Threading.DispatcherTimer();
    bool updatingView;
    Game selected;bool smoke;bool closing;
    T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public Launcher(Store data,bool test) {
      store=data;smoke=test;library=store.Load();artwork=new ArtworkService(store);
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.MainWindow.xaml")){Window=(Window)XamlReader.Load(stream);}
      Window.SourceInitialized+=delegate{int value=1;DwmSetWindowAttribute(new WindowInteropHelper(Window).Handle,20,ref value,4);};
      Window.Closed+=delegate{closing=true;viewSaveTimer.Stop();if(!smoke)Save();shutdown.Cancel();artwork.Dispose();};
      Find<Button>("AddButton").Click+=delegate{Add();};Find<Button>("EditButton").Click+=delegate{Edit();};Find<Button>("PlayButton").Click+=delegate{Launch();};Find<Button>("FavoriteButton").Click+=delegate{if(selected!=null){selected.Favorite=!selected.Favorite;Save();Refresh();}};
      Find<Button>("ConfigureButton").Click+=delegate{if(selected==null)return;try{if(GameContent.Supports(selected)){ConfigureContent();return;}Process.Start(Ports.ConfigurationStartInfo(selected));Status("Configuração aberta: "+selected.Name);}catch(Exception ex){MessageBox.Show(Window,ex.Message,"Configurar jogo",MessageBoxButton.OK,MessageBoxImage.Information);}};
      Find<Button>("SettingsButton").Click+=delegate{Settings();};Find<Button>("DetectButton").Click+=delegate{Detect(true);};
      Find<Button>("DownloadPortsButton").Click+=delegate{new CatalogWindow(Window,store.Root,library.Games,delegate(Game game){library.Games.Add(game);selected=game;Save();Refresh();ResolveArtwork(game);}).ShowDialog();};
      Find<TextBox>("SearchBox").TextChanged+=delegate{RefreshGrid();};Find<CheckBox>("FavoritesOnly").Checked+=delegate{RefreshGrid();};Find<CheckBox>("FavoritesOnly").Unchecked+=delegate{RefreshGrid();};
      viewSaveTimer.Interval=TimeSpan.FromMilliseconds(400);viewSaveTimer.Tick+=delegate{viewSaveTimer.Stop();if(!smoke)Save();};
      Find<Button>("GridViewButton").Click+=delegate{SetView("grid");};Find<Button>("ListViewButton").Click+=delegate{SetView("list");};
      Find<Button>("SizeDownButton").Click+=delegate{Find<Slider>("ViewSizeSlider").Value-=5;};Find<Button>("SizeUpButton").Click+=delegate{Find<Slider>("ViewSizeSlider").Value+=5;};
      Find<Slider>("ViewSizeSlider").ValueChanged+=delegate{if(updatingView)return;double value=Find<Slider>("ViewSizeSlider").Value;if(library.ViewMode=="list")library.ListSize=(int)Math.Round(48+value*0.84);else library.GridSize=(int)Math.Round(140+value*2);UpdateViewControls();RefreshGrid();viewSaveTimer.Stop();viewSaveTimer.Start();};
      Find<ComboBox>("PlatformFilter").ItemsSource=Platforms.All;Find<ComboBox>("PlatformFilter").SelectedIndex=0;Find<ComboBox>("PlatformFilter").SelectionChanged+=delegate{RefreshGrid();};
      UpdateViewControls();
      if(!File.Exists(store.FilePath)&&!smoke)Detect(false);
      selected=library.Games.FirstOrDefault();Refresh();
      Window.Loaded+=delegate{if(!smoke){foreach(Game g in library.Games.ToArray())if(String.IsNullOrEmpty(g.Artwork)||!File.Exists(g.Artwork))ResolveArtwork(g);}};
    }
    void Save(){store.Save(library);}
    void Status(string text){if(!closing)Find<TextBlock>("StatusLabel").Text=text;}
    public void Refresh(){
      bool has=selected!=null;Find<Button>("PlayButton").IsEnabled=has;Find<Button>("EditButton").IsEnabled=has;Find<Button>("FavoriteButton").IsEnabled=has;
      Find<Button>("ConfigureButton").IsEnabled=has;Find<Button>("ConfigureButton").Visibility=has&&(GameContent.Supports(selected)||!String.IsNullOrEmpty(Ports.FindConfigurationExecutable(selected)))?Visibility.Visible:Visibility.Collapsed;
      Find<TextBlock>("HeroTitle").Text=has?selected.Name:"Seu próximo universo";
      Find<TextBlock>("HeroDescription").Text=!has?"Adicione seus ports e reúna suas aventuras em um só lugar.":selected.Name=="Bloodborne"?"As ruas de Yharnam estão à sua espera. Sua próxima caçada começa aqui.":selected.Name=="God of War III"?"Enfrente os deuses do Olimpo. Sua próxima aventura começa aqui.":selected.Name=="Shadow of the Colossus"?"Uma terra esquecida. Dezesseis colossos. Uma jornada para revisitar.":"Seu port, suas configurações. Pronto para a próxima aventura.";
      Find<Button>("FavoriteButton").Content=has&&selected.Favorite?"★ Favorito":"☆ Favoritar";
      string meta=has?Platforms.Resolve(selected)+" · Windows":"Windows 11 · Biblioteca local";
      if(has&&!String.IsNullOrEmpty(selected.LastPlayed)){DateTime time;if(DateTime.TryParse(selected.LastPlayed,out time))meta+="   /   Última abertura: "+time.ToLocalTime().ToString("dd/MM/yyyy HH:mm");}
      Find<TextBlock>("HeroMeta").Text=meta;
      Find<Image>("Backdrop").Source=has?LoadImage(selected.Artwork):null;RefreshGrid();
    }
    ImageSource LoadImage(string path){
      if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;
      ImageSource cached;if(imageCache.TryGetValue(path,out cached))return cached;
      try{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=1600;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();imageCache[path]=image;return image;}catch{return null;}
    }
    void SetView(string mode){library.ViewMode=mode;UpdateViewControls();RefreshGrid();if(!smoke)Save();}
    void UpdateViewControls(){
      updatingView=true;bool list=library.ViewMode=="list";
      Find<Slider>("ViewSizeSlider").Value=list?(library.ListSize-48)/0.84:(library.GridSize-140)/2.0;
      Find<TextBlock>("ViewSizeLabel").Text=(list?library.ListSize:library.GridSize)+" px";
      Find<Button>("GridViewButton").Background=new SolidColorBrush(list?Color.FromRgb(40,44,53):Color.FromRgb(180,241,118));Find<Button>("GridViewButton").Foreground=list?Brushes.White:Brushes.Black;
      Find<Button>("ListViewButton").Background=new SolidColorBrush(list?Color.FromRgb(180,241,118):Color.FromRgb(40,44,53));Find<Button>("ListViewButton").Foreground=list?Brushes.Black:Brushes.White;
      Find<Button>("SizeDownButton").IsEnabled=Find<Slider>("ViewSizeSlider").Value>0;Find<Button>("SizeUpButton").IsEnabled=Find<Slider>("ViewSizeSlider").Value<100;updatingView=false;
    }
    void RefreshGrid(){
      bool list=library.ViewMode=="list";Panel grid=list?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");Find<WrapPanel>("GameGrid").Children.Clear();Find<StackPanel>("GameList").Children.Clear();Find<WrapPanel>("GameGrid").Visibility=list?Visibility.Collapsed:Visibility.Visible;Find<StackPanel>("GameList").Visibility=list?Visibility.Visible:Visibility.Collapsed;string search=Find<TextBox>("SearchBox").Text.Trim();bool favorite=Find<CheckBox>("FavoritesOnly").IsChecked==true;
      List<Game> visible=Platforms.Filter(library.Games,Find<ComboBox>("PlatformFilter").SelectedItem as string,search,favorite);
      foreach(Game g in visible){
        var card=new StackPanel();var art=new Grid{Width=list?library.ListSize*1.6:library.GridSize,Height=list?library.ListSize:library.GridSize*135.0/220,Background=new SolidColorBrush(Color.FromRgb(37,43,53))};ImageSource source=LoadImage(g.Artwork);
        if(source!=null)art.Children.Add(new Image{Source=source,Stretch=Stretch.UniformToFill});
        else {var blank=new TextBlock{Text="◈",Foreground=new SolidColorBrush(Color.FromRgb(180,241,118)),FontSize=45,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};art.Children.Add(blank);}
        var shade=new System.Windows.Shapes.Rectangle{Fill=new LinearGradientBrush(Color.FromArgb(0,16,17,21),Color.FromArgb(225,16,17,21),90)};art.Children.Add(shade);
        if(!list)art.Children.Add(new TextBlock{Text=g.Name,TextWrapping=TextWrapping.Wrap,FontSize=library.GridSize<180?14:17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(14),VerticalAlignment=VerticalAlignment.Bottom});
        var border=new Border{CornerRadius=new CornerRadius(8),ClipToBounds=true,Child=art};card.Children.Add(border);
        card.Children.Add(new TextBlock{Text=(g.Favorite?"★  ":"")+Platforms.Resolve(g)+" · Windows",Foreground=new SolidColorBrush(Color.FromRgb(158,168,184)),Margin=new Thickness(2,10,0,0),FontSize=12});
        FrameworkElement content=card;
        if(list){
          card.Children.Remove(border);var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(140)});row.Children.Add(border);
          var details=new StackPanel{Margin=new Thickness(16,0,12,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(details,1);details.Children.Add(new TextBlock{Text=g.Name,FontSize=library.ListSize<60?14:17,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});if(library.ListSize>=60)details.Children.Add(new TextBlock{Text=(g.Favorite?"★  ":"")+Platforms.Resolve(g)+" · Windows",FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(158,168,184)),Margin=new Thickness(0,5,0,0)});row.Children.Add(details);
          DateTime last;string when=!String.IsNullOrEmpty(g.LastPlayed)&&DateTime.TryParse(g.LastPlayed,out last)?"Última abertura\n"+last.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):"Nunca aberto";var played=new TextBlock{Text=when,FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(158,168,184)),TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};Grid.SetColumn(played,2);row.Children.Add(played);content=row;
        }
        var button=new Button{Content=content,HorizontalContentAlignment=list?HorizontalAlignment.Stretch:HorizontalAlignment.Center,HorizontalAlignment=list?HorizontalAlignment.Stretch:HorizontalAlignment.Left,Padding=new Thickness(4),Margin=list?new Thickness(0,0,0,9):new Thickness(0,0,17,18),BorderBrush=new SolidColorBrush(g==selected?Color.FromRgb(180,241,118):Color.FromRgb(48,53,63)),Background=list?new SolidColorBrush(Color.FromArgb(220,24,27,33)):Brushes.Transparent,ToolTip=g.Name};
        AutomationProperties.SetName(button,"Selecionar "+g.Name);Game current=g;button.Click+=delegate{selected=current;Refresh();};grid.Children.Add(button);
      }
      Find<TextBlock>("CountLabel").Text=library.Games.Count+" jogos";
      Find<TextBlock>("EmptyLabel").Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
      Find<TextBlock>("EmptyLabel").Text=library.Games.Count==0?"Sua coleção começa aqui. Adicione um executável ou detecte ports na pasta Downloads.":"Nenhum jogo corresponde à plataforma, busca e favoritos selecionados.";
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
    bool ConfigureContent(){var dialog=new GameContentWindow(Window,selected);dialog.ShowDialog();if(dialog.PreparedPath!=null){Save();Refresh();Status("Arquivos preparados para "+selected.Name);return true;}return !GameContent.NeedsSetup(selected);}
    void Launch(){
      if(selected==null)return;
      try{if(GameContent.NeedsSetup(selected)&&!ConfigureContent())return;Process.Start(Ports.StartInfo(selected));selected.LastPlayed=DateTime.UtcNow.ToString("o");Save();Refresh();Status("Inicialização solicitada: "+selected.Name);if(library.MinimizeOnLaunch)Window.WindowState=WindowState.Minimized;}
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
    public void Capture(string target){if(Environment.GetCommandLineArgs().Contains("--content-smoke")){Game sample=library.Games.FirstOrDefault(GameContent.Supports)??new Game{Name="BLACK",Executable=Path.Combine(store.Root,"BLACK.exe")};var dialog=new GameContentWindow(Window,sample);dialog.Show();dialog.UpdateLayout();CaptureWindow(dialog,target);dialog.Close();return;}if(Environment.GetCommandLineArgs().Contains("--catalog-smoke")){var catalogWindow=new CatalogWindow(Window,store.Root,library.Games,delegate(Game g){}){IsSmoke=true};catalogWindow.Show();catalogWindow.UpdateLayout();catalogWindow.CaptureSmoke(target);catalogWindow.Close();}else CaptureWindow(Window,target);}
    public void PreviewView(string mode,int size){
      Find<Button>(mode=="list"?"ListViewButton":"GridViewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
      if(size>0)Find<Slider>("ViewSizeSlider").Value=mode=="list"?(size-48)/0.84:(size-140)/2.0;
      if(library.ViewMode!=mode)throw new Exception("View switch failed");Panel visible=mode=="list"?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");
      if(visible.Visibility!=Visibility.Visible||visible.Children.Count!=library.Games.Count)throw new Exception("View rendering failed");
      if(library.Games.Count>0){var lastButton=(Button)visible.Children[visible.Children.Count-1];string expected=(string)lastButton.ToolTip;lastButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(selected==null||selected.Name!=expected)throw new Exception("Selection failed");Find<TextBox>("SearchBox").Text=expected;Panel filtered=mode=="list"?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");if(filtered.Children.Count<1)throw new Exception("Search in view failed");Find<TextBox>("SearchBox").Text="";}
      foreach(string platform in Platforms.All){Find<ComboBox>("PlatformFilter").SelectedItem=platform;Panel filtered=mode=="list"?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");if(filtered.Children.Count!=Platforms.Filter(library.Games,platform,"",false).Count)throw new Exception("Platform rendering failed: "+platform);}
      Find<ComboBox>("PlatformFilter").SelectedIndex=0;
      foreach(Game game in library.Games){selected=game;Refresh();bool configured=GameContent.Supports(game)||!String.IsNullOrEmpty(Ports.FindConfigurationExecutable(game));if((Find<Button>("ConfigureButton").Visibility==Visibility.Visible)!=configured)throw new Exception("Optional configuration action failed");}
      int beforeGrid=library.GridSize,beforeList=library.ListSize;SetView(mode=="list"?"grid":"list");SetView(mode);if(library.GridSize!=beforeGrid||library.ListSize!=beforeList)throw new Exception("Independent size restoration failed");
    }
    public static void CaptureWindow(Window window,string target){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));using(var output=File.Create(target))encoder.Save(output);}
  }
  public class GameEditor:Window {
    public Game Result;public bool Remove;public bool RefreshArtwork;
    readonly ComboBox platform;readonly TextBox name,exe,arguments,working,configExe,configArgs,artTitle;readonly Store store;readonly TextBlock artLabel;string picked;
    public GameEditor(Window owner,Game game,Store data,bool existing){
      Result=game;store=data;Owner=owner;Title=existing?"Editar cadastro · NEXUS":"Adicionar jogo · NEXUS";Width=700;Height=830;MinHeight=580;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var scroll=new ScrollViewer();Content=scroll;var p=new StackPanel{Margin=new Thickness(26)};scroll.Content=p;
      p.Children.Add(new TextBlock{Text=existing?"Seu jogo, do seu jeito":"Adicionar à coleção",FontSize=25,Margin=new Thickness(0,0,0,20)});
      p.Children.Add(new TextBlock{Text="Plataforma de origem",Margin=new Thickness(0,0,0,7)});platform=new ComboBox{ItemsSource=Platforms.All.Skip(1).ToArray(),SelectedItem=Platforms.Resolve(game),Foreground=Brushes.Black,Padding=new Thickness(8),Margin=new Thickness(0,0,0,10)};AutomationProperties.SetName(platform,"Plataforma de origem");p.Children.Add(platform);
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
          Game candidate=Result.Copy();candidate.Platform=platform.SelectedItem as string;candidate.Name=name.Text.Trim();candidate.Executable=exe.Text.Trim().Trim('"');candidate.Arguments=arguments.Text;candidate.WorkingDirectory=working.Text.Trim().Trim('"');Ports.StartInfo(candidate,false);
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
