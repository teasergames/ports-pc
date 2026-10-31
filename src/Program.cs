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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Automation;
using Microsoft.Win32;

[assembly: AssemblyTitle("NEXUS · Ports PC")]
[assembly: AssemblyProduct("NEXUS Ports PC")]
[assembly: AssemblyVersion("1.7.0.0")]
[assembly: AssemblyFileVersion("1.7.0.0")]
[assembly: AssemblyInformationalVersion("1.7.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace Nexus {
  public static class Program {
    [STAThread] public static int Main(string[] args) {
      if(args.Contains("--self-test"))return Tests.Run();
      int helperIndex=Array.IndexOf(args,"--apply-nexus-update");if(helperIndex>=0&&helperIndex+1<args.Length)return SelfUpdater.RunHelper(args[helperIndex+1],args.Contains("--recover-nexus-update"));
      if(!args.Contains("--smoke-test")&&!args.Contains("--update-complete")&&SelfUpdater.RecoverInterrupted())return 0;
      string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NexusPortsPC");
      int index=System.Array.IndexOf(args,"--data-dir");if(index>=0&&index+1<args.Length)root=Path.GetFullPath(args[index+1]);
      Store store=new Store(root);bool smoke=args.Contains("--smoke-test");
      using(var mutex=new Mutex(false,"Local\\NexusPortsPC-"+Ports.Normalize(root))){
        bool owns=false;try{try{owns=mutex.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
          if(!owns){MessageBox.Show("O NEXUS já está aberto.","NEXUS");return 0;}
          var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;app.Resources.MergedDictionaries.Add(LoadTheme());
          app.DispatcherUnhandledException+=(sender,eventArgs)=>{store.Log(eventArgs.Exception.ToString());if(smoke){File.WriteAllText(Path.Combine(root,"smoke-error.txt"),eventArgs.Exception.ToString());eventArgs.Handled=true;app.Shutdown(1);return;}MessageBox.Show(eventArgs.Exception.Message,"Não foi possível concluir",MessageBoxButton.OK,MessageBoxImage.Warning);eventArgs.Handled=true;};
          var controller=new Launcher(store,smoke);app.MainWindow=controller.Window;
          int receiptIndex=Array.IndexOf(args,"--update-complete");if(receiptIndex>=0&&receiptIndex+1<args.Length)controller.Window.Loaded+=delegate{SelfUpdater.Acknowledge(args[receiptIndex+1]);};
          if(smoke){index=System.Array.IndexOf(args,"--smoke-test");string target=index+1<args.Length?args[index+1]:Path.Combine(root,"preview.png");int viewIndex=System.Array.IndexOf(args,"--preview-view"),sizeIndex=System.Array.IndexOf(args,"--preview-size");string view=viewIndex>=0&&viewIndex+1<args.Length?args[viewIndex+1]:null;int size=0;if(sizeIndex>=0&&sizeIndex+1<args.Length)Int32.TryParse(args[sizeIndex+1],out size);controller.Window.Loaded+=delegate{controller.Window.Dispatcher.BeginInvoke(new Action(delegate{if(view!=null)controller.PreviewView(view,size);controller.Capture(target);app.Shutdown();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);};}
          app.Run(controller.Window);return 0;
        }catch(Exception ex){store.Log(ex.ToString());if(smoke)File.WriteAllText(Path.Combine(root,"smoke-error.txt"),ex.ToString());else MessageBox.Show(ex.Message,"NEXUS",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
        finally{if(owns)mutex.ReleaseMutex();}
      }
    }
    // Colors and control styles shared by every window (src/Theme.xaml).
    public static ResourceDictionary LoadTheme(){
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.Theme.xaml"))return (ResourceDictionary)XamlReader.Load(stream);
    }
    // Version shown to people: "1.7.0-beta.2" for pre-releases, otherwise the numeric version.
    public static string DisplayVersion {
      get {
        var attribute=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),typeof(AssemblyInformationalVersionAttribute));
        return attribute!=null&&!String.IsNullOrEmpty(attribute.InformationalVersion)?attribute.InformationalVersion:SelfUpdater.CurrentVersion;
      }
    }
  }

  public enum CatalogTab { All, News, Updates }

  public class Launcher {
    public readonly Window Window;
    readonly Store store;readonly Library library;readonly ArtworkService artwork;readonly CancellationTokenSource shutdown=new CancellationTokenSource();
    readonly SemaphoreSlim artQueue=new SemaphoreSlim(1,1);readonly HashSet<string> pending=new HashSet<string>();
    readonly Dictionary<string,ImageSource> imageCache=new Dictionary<string,ImageSource>();
    readonly Dictionary<string,PortEntry> profiles;
    readonly HashSet<string> updatable=new HashSet<string>();
    readonly List<Game> visible=new List<Game>();
    Game selected;bool smoke;bool closing;bool checkingLauncher;
    string platformFilter=Platforms.All[0];
    Gamepad gamepad;
    static readonly int[] GridSizes={160,220,300},ListSizes={56,76,110};
    static readonly Brush Muted=Frozen(Color.FromRgb(158,168,184));
    static Brush Frozen(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
    T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
    Brush Theme(string key){return (Brush)Application.Current.Resources[key];}
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);

    public Launcher(Store data,bool test) {
      store=data;smoke=test;library=store.Load();artwork=new ArtworkService(store);
      profiles=PortCatalog.LoadEntries().GroupBy(e=>e.Identity).ToDictionary(g=>g.Key,g=>g.First());
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.MainWindow.xaml")){Window=(Window)XamlReader.Load(stream);}
      Window.SourceInitialized+=delegate{int value=1;DwmSetWindowAttribute(new WindowInteropHelper(Window).Handle,20,ref value,4);};
      Window.Closed+=delegate{closing=true;if(gamepad!=null)gamepad.Dispose();if(!smoke)Save();shutdown.Cancel();artwork.Dispose();};

      Find<Button>("PlayButton").Click+=delegate{Launch();};
      Find<Button>("ConfigureButton").Click+=delegate{Configure();};
      Find<Button>("FavoriteButton").Click+=delegate{ToggleFavorite();};
      Find<Button>("MoreButton").Click+=delegate{OpenMenu(Find<Button>("MoreButton"));};
      Find<MenuItem>("EditMenuItem").Click+=delegate{Edit();};
      Find<MenuItem>("OpenFolderMenuItem").Click+=delegate{OpenFolder();};
      Find<MenuItem>("ArtworkMenuItem").Click+=delegate{if(selected!=null)ResolveArtwork(selected);};
      Find<MenuItem>("RemoveMenuItem").Click+=delegate{RemoveFromCollection();};
      Find<MenuItem>("DeletePortMenuItem").Click+=delegate{DeletePort();};
      Find<Button>("AddButton").Click+=delegate{OpenMenu(Find<Button>("AddButton"));};
      Find<MenuItem>("AddExecutableMenuItem").Click+=delegate{Add();};
      Find<MenuItem>("DetectMenuItem").Click+=delegate{Detect(true);};
      Find<Button>("SettingsButton").Click+=delegate{Settings();};
      Find<Button>("DownloadPortsButton").Click+=delegate{OpenCatalog(CatalogTab.All);};
      Find<Button>("UpdatesNavButton").Click+=delegate{OpenCatalog(CatalogTab.Updates);};
      Find<Button>("LauncherUpdateButton").Content="NEXUS "+Program.DisplayVersion+" · Verificar atualização";Find<Button>("LauncherUpdateButton").Click+=delegate{CheckLauncherUpdate(true);};
      Find<TextBox>("SearchBox").TextChanged+=delegate{RefreshGrid();};
      Find<ToggleButton>("FavoritesChip").Click+=delegate{RefreshGrid();};
      Find<ToggleButton>("GridViewButton").Click+=delegate{SetView("grid");};
      Find<ToggleButton>("ListViewButton").Click+=delegate{SetView("list");};
      Find<ToggleButton>("SizeSmallButton").Click+=delegate{SetSize(0);};
      Find<ToggleButton>("SizeMediumButton").Click+=delegate{SetSize(1);};
      Find<ToggleButton>("SizeLargeButton").Click+=delegate{SetSize(2);};
      var sort=Find<ComboBox>("SortBox");sort.SelectedItem=sort.Items.Cast<ComboBoxItem>().First(i=>(string)i.Tag==library.SortMode);
      sort.SelectionChanged+=delegate{library.SortMode=(string)((ComboBoxItem)sort.SelectedItem).Tag;RefreshGrid();if(!smoke)Save();};
      Window.PreviewKeyDown+=OnKeyDown;
      Window.SizeChanged+=delegate{AdaptHero();};

      UpdateViewControls();
      if(!File.Exists(store.FilePath)&&!smoke)Detect(false);
      if(library.SeenCatalogIds==null){library.SeenCatalogIds=PortCatalog.LoadEntries().Select(e=>e.Id).ToList();if(!smoke)Save();}
      UpdateCatalogBadge(PortCatalog.LoadEntries());
      selected=library.Games.FirstOrDefault();Refresh();
      Window.Loaded+=delegate{if(!smoke){foreach(Game g in library.Games.ToArray())if(String.IsNullOrEmpty(g.Artwork)||!File.Exists(g.Artwork))ResolveArtwork(g);}};
      Window.Loaded+=delegate{if(!smoke){if(Environment.GetCommandLineArgs().Contains("--nexus-update-failed"))Status("A atualização não foi concluída. A versão anterior foi restaurada; consulte os logs em launcher-updates.");else if(library.AutoUpdateLauncher&&!Environment.GetCommandLineArgs().Contains("--skip-nexus-update"))CheckLauncherUpdate(false);}};
      Window.Loaded+=delegate{if(!smoke){CheckCatalogInBackground();gamepad=new Gamepad(()=>Window.IsActive&&Window.OwnedWindows.Count==0);gamepad.Pressed+=OnPad;}};
    }

    async void CheckLauncherUpdate(bool manual){
      if(checkingLauncher||closing)return;checkingLauncher=true;Find<Button>("LauncherUpdateButton").IsEnabled=false;
      try{Status("Verificando atualização do NEXUS…");using(var updater=new SelfUpdater()){var release=await updater.Check(shutdown.Token);if(closing)return;if(release==null){Status("NEXUS "+Program.DisplayVersion+" está atualizado.");return;}if(Window.OwnedWindows.Count>0){Status("NEXUS "+release.Version+" disponível. Conclua a operação atual e clique em Verificar atualização.");return;}var dialog=new SelfUpdateWindow(Window,updater,release,store.Root);if(dialog.ShowDialog()!=true)return;Save();var helper=SelfUpdater.StartHelper(dialog.PlanFile,false);if(helper==null)throw new IOException("Não foi possível abrir o auxiliar de atualização.");helper.Dispose();Application.Current.Shutdown();}}
      catch(OperationCanceledException){if(!closing)Status("Atualização do NEXUS adiada. Tente novamente quando quiser.");}
      catch(Exception ex){store.Log("Atualização do NEXUS: "+ex);Status("Não foi possível atualizar o NEXUS: "+ex.Message);if(manual&&!closing)MessageBox.Show(Window,ex.Message,"Atualização do NEXUS",MessageBoxButton.OK,MessageBoxImage.Information);}
      finally{checkingLauncher=false;if(!closing)Find<Button>("LauncherUpdateButton").IsEnabled=true;}
    }

    // Refreshes the online catalog and checks the managed installations once per session to fill
    // the "Catálogo" and "Atualizações" badges. Failures only leave the badges as they were.
    async void CheckCatalogInBackground(){
      try{
        using(var catalog=new PortCatalog()){
          await catalog.RefreshFromRemote(store.Root,shutdown.Token);if(closing)return;
          UpdateCatalogBadge(catalog.Entries);
          foreach(Game game in library.Games.ToArray()){
            PortEntry entry=catalog.Entries.FirstOrDefault(e=>e.Identity==game.Repository);
            if(entry==null||entry.ProjectOnly||entry.ManualPackage||!UpdateManager.Managed(game))continue;
            try{var release=await catalog.Latest(entry,shutdown.Token,true);if(!release.FromCache&&UpdateManager.Available(game,release))updatable.Add(game.Id);else updatable.Remove(game.Id);}
            catch(OperationCanceledException){throw;}catch(Exception){}
            if(closing)return;
          }
          UpdateUpdatesBadge();Refresh();
        }
      }catch(OperationCanceledException){}catch(Exception ex){store.Log("Consulta do catálogo: "+ex.Message);}
    }
    void UpdateCatalogBadge(IEnumerable<PortEntry> entries){
      var seen=new HashSet<string>(library.SeenCatalogIds??new List<string>());int count=entries.Count(e=>!seen.Contains(e.Id));
      Find<Border>("CatalogBadge").Visibility=count>0?Visibility.Visible:Visibility.Collapsed;Find<TextBlock>("CatalogBadgeText").Text=count==1?"1 novo":count+" novos";
    }
    void UpdateUpdatesBadge(){
      int count=library.Games.Count(g=>updatable.Contains(g.Id));
      Find<Border>("UpdatesBadge").Visibility=count>0?Visibility.Visible:Visibility.Collapsed;Find<TextBlock>("UpdatesBadgeText").Text=count.ToString();
    }
    void OpenCatalog(CatalogTab tab){
      var window=new CatalogWindow(Window,store.Root,library,delegate(Game game){int index=library.Games.FindIndex(g=>g.Id==game.Id);Game previous=index<0?null:library.Games[index];if(index<0)library.Games.Add(game);else library.Games[index]=game;try{Save();}catch{if(index<0)library.Games.Remove(game);else library.Games[index]=previous;throw;}updatable.Remove(game.Id);selected=game;Refresh();ResolveArtwork(game);},tab);
      window.ShowDialog();
      foreach(Game game in library.Games)if(window.UpdatesFound.Contains(game.Repository??"")&&UpdateManager.Managed(game))updatable.Add(game.Id);
      if(!smoke)Save();UpdateCatalogBadge(window.Entries);UpdateUpdatesBadge();Refresh();
    }

    void Save(){store.Save(library);}
    void Status(string text){if(!closing)Find<TextBlock>("StatusLabel").Text=text;}
    static void OpenMenu(Button button){var menu=button.ContextMenu;menu.PlacementTarget=button;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;}

    public void Refresh(){
      bool has=selected!=null;
      foreach(string name in new[]{"PlayButton","FavoriteButton","MoreButton"})Find<Button>(name).IsEnabled=has;
      var configure=Find<Button>("ConfigureButton");configure.ToolTip=has&&FolderLaunch.Supports(selected)?"Selecionar a pasta extraída do jogo":"Abrir a configuração do jogo";configure.IsEnabled=has;
      configure.Visibility=has&&(GameContent.Supports(selected)||FolderLaunch.Supports(selected)||!String.IsNullOrEmpty(Ports.FindConfigurationExecutable(selected)))?Visibility.Visible:Visibility.Collapsed;
      Find<TextBlock>("HeroTitle").Text=has?selected.Name:"Seu próximo universo";
      var badges=Find<WrapPanel>("HeroBadges");badges.Children.Clear();
      if(has){
        badges.Children.Add(Badge(Platforms.Resolve(selected),Theme("ControlActive"),Theme("Text"),null));
        PortEntry profile=Profile(selected);if(profile!=null)badges.Children.Add(Badge(profile.ProjectType,Brushes.Transparent,Theme("TextSoft"),Theme("ControlLine")));
        if(updatable.Contains(selected.Id))badges.Children.Add(Badge("Atualização disponível",Theme("Warning"),Theme("OnWarning"),null));
      }
      Find<TextBlock>("HeroMeta").Text=has?HeroMeta(selected):"Adicione seus ports e reúna suas aventuras em um só lugar.";
      bool favorite=has&&selected.Favorite;Find<TextBlock>("FavoriteIcon").Text=favorite?"":"";Find<TextBlock>("FavoriteIcon").Foreground=favorite?Theme("Favorite"):Theme("Text");
      Find<Button>("FavoriteButton").ToolTip=favorite?"Remover dos favoritos":"Favoritar";AutomationProperties.SetName(Find<Button>("FavoriteButton"),favorite?"Remover dos favoritos":"Favoritar");
      Find<Image>("Backdrop").Source=has?LoadImage(selected.Artwork):null;RefreshGrid();
    }
    static string HeroMeta(Game game){
      string when=Platforms.LastPlayed(game)==DateTime.MinValue?"Nunca aberto":"Aberto pela última vez em "+Platforms.LastPlayed(game).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
      return String.IsNullOrEmpty(game.InstalledVersion)?when:when+"   ·   Versão "+game.InstalledVersion;
    }
    PortEntry Profile(Game game){PortEntry entry;return !String.IsNullOrEmpty(game.Repository)&&profiles.TryGetValue(game.Repository,out entry)?entry:null;}
    static Border Badge(string text,Brush background,Brush foreground,Brush border){
      var badge=new Border{Style=(Style)Application.Current.Resources["Badge"],Background=background};
      if(border!=null){badge.BorderBrush=border;badge.BorderThickness=new Thickness(1);}
      badge.Child=new TextBlock{Text=text,Style=(Style)Application.Current.Resources["BadgeText"],Foreground=foreground};return badge;
    }
    void AdaptHero(){
      bool compact=Window.ActualHeight<760;Find<TextBlock>("HeroTitle").FontSize=compact?28:36;Find<StackPanel>("Hero").Margin=compact?new Thickness(36,16,36,14):new Thickness(36,26,36,22);
    }

    ImageSource LoadImage(string path){
      if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return null;
      ImageSource cached;if(imageCache.TryGetValue(path,out cached))return cached;
      try{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=1600;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();imageCache[path]=image;return image;}catch{return null;}
    }

    void SetView(string mode){library.ViewMode=mode;UpdateViewControls();RefreshGrid();if(!smoke)Save();}
    void SetSize(int preset){if(library.ViewMode=="list")library.ListSize=ListSizes[preset];else library.GridSize=GridSizes[preset];UpdateViewControls();RefreshGrid();if(!smoke)Save();}
    void UpdateViewControls(){
      bool list=library.ViewMode=="list";
      Find<ToggleButton>("GridViewButton").IsChecked=!list;Find<ToggleButton>("ListViewButton").IsChecked=list;
      int[] presets=list?ListSizes:GridSizes;int current=list?library.ListSize:library.GridSize;
      int nearest=Enumerable.Range(0,presets.Length).OrderBy(i=>Math.Abs(presets[i]-current)).First();
      Find<ToggleButton>("SizeSmallButton").IsChecked=nearest==0;Find<ToggleButton>("SizeMediumButton").IsChecked=nearest==1;Find<ToggleButton>("SizeLargeButton").IsChecked=nearest==2;
    }

    // Platform chips list only the platforms present in the collection, with their counts.
    void RefreshPlatformChips(){
      var panel=Find<WrapPanel>("PlatformChips");panel.Children.Clear();
      var present=library.Games.GroupBy(g=>Platforms.Resolve(g)).ToDictionary(g=>g.Key,g=>g.Count());
      if(platformFilter!=Platforms.All[0]&&!present.ContainsKey(platformFilter))platformFilter=Platforms.All[0];
      AddChip(panel,Platforms.All[0],"Todas",library.Games.Count);
      foreach(string platform in Platforms.All.Skip(1).Where(present.ContainsKey))AddChip(panel,platform,Platforms.ShortName(platform),present[platform]);
    }
    void AddChip(Panel panel,string platform,string label,int count){
      var chip=new ToggleButton{Style=(Style)Application.Current.Resources["Chip"],IsChecked=platformFilter==platform,Tag=platform,ToolTip=platform};
      var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new TextBlock{Text=label});content.Children.Add(new TextBlock{Text=count.ToString(),Opacity=0.7,FontWeight=FontWeights.Normal,Margin=new Thickness(7,0,0,0)});chip.Content=content;
      AutomationProperties.SetName(chip,"Filtrar "+platform);chip.Click+=delegate{platformFilter=platform;RefreshGrid();};panel.Children.Add(chip);
    }

    void RefreshGrid(){
      RefreshPlatformChips();
      bool list=library.ViewMode=="list";var grid=Find<WrapPanel>("GameGrid");var rows=Find<StackPanel>("GameList");grid.Children.Clear();rows.Children.Clear();
      grid.Visibility=list?Visibility.Collapsed:Visibility.Visible;rows.Visibility=list?Visibility.Visible:Visibility.Collapsed;
      string search=Find<TextBox>("SearchBox").Text.Trim();bool favorite=Find<ToggleButton>("FavoritesChip").IsChecked==true;
      visible.Clear();visible.AddRange(Platforms.Filter(library.Games,platformFilter,search,favorite,library.SortMode));
      foreach(Game g in visible)(list?(Panel)rows:grid).Children.Add(list?ListRow(g):GridCard(g));
      Find<TextBlock>("CountLabel").Text=library.Games.Count==1?"1 jogo":library.Games.Count+" jogos";
      var empty=Find<TextBlock>("EmptyLabel");empty.Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
      empty.Text=library.Games.Count==0?"Sua coleção começa aqui. Use Adicionar jogo ou instale um port pelo Catálogo.":"Nenhum jogo corresponde à plataforma, busca e favoritos selecionados.";
    }

    Grid Artwork(Game g,double width,double height,bool badges){
      var art=new Grid{Width=width,Height=height,Background=Frozen(Color.FromRgb(37,43,53))};ImageSource source=LoadImage(g.Artwork);
      if(source!=null)art.Children.Add(new Image{Source=source,Stretch=Stretch.UniformToFill});
      else art.Children.Add(new TextBlock{Text="◈",Foreground=Theme("Accent"),FontSize=Math.Max(24,height/3),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
      if(!badges)return art;
      var platform=Badge(Platforms.ShortName(Platforms.Resolve(g)),Frozen(Color.FromArgb(210,16,17,21)),Theme("Text"),null);platform.HorizontalAlignment=HorizontalAlignment.Left;platform.VerticalAlignment=VerticalAlignment.Top;platform.Margin=new Thickness(8);art.Children.Add(platform);
      Border status=updatable.Contains(g.Id)?Badge("Atualizar",Theme("Warning"),Theme("OnWarning"),null):String.IsNullOrEmpty(g.LastPlayed)?Badge("Nunca aberto",Theme("Accent"),Theme("OnAccent"),null):null;
      if(status!=null){status.HorizontalAlignment=HorizontalAlignment.Right;status.VerticalAlignment=VerticalAlignment.Top;status.Margin=new Thickness(8);art.Children.Add(status);}
      return art;
    }
    string Subtitle(Game g){PortEntry profile=Profile(g);return (g.Favorite?"★  ":"")+Platforms.Resolve(g)+(profile!=null?" · "+profile.ProjectType:"");}

    Button GridCard(Game g){
      var card=new StackPanel{Width=library.GridSize};
      card.Children.Add(new Border{CornerRadius=new CornerRadius(8),ClipToBounds=true,Child=Artwork(g,library.GridSize,library.GridSize*135.0/220,library.GridSize>=180)});
      card.Children.Add(new TextBlock{Text=g.Name,FontWeight=FontWeights.SemiBold,FontSize=library.GridSize<180?13:15,TextWrapping=TextWrapping.Wrap,MaxHeight=44,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(4,10,4,2)});
      card.Children.Add(new TextBlock{Text=Subtitle(g),Foreground=Muted,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(4,0,4,4)});
      return CardButton(g,card,false);
    }
    Button ListRow(Game g){
      var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
      row.Children.Add(new Border{CornerRadius=new CornerRadius(6),ClipToBounds=true,Child=Artwork(g,library.ListSize*1.6,library.ListSize,false)});
      var details=new StackPanel{Margin=new Thickness(16,0,12,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(details,1);
      details.Children.Add(new TextBlock{Text=g.Name,FontSize=library.ListSize<60?14:16,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});
      if(library.ListSize>=60)details.Children.Add(new TextBlock{Text=Subtitle(g),FontSize=12,Foreground=Muted,Margin=new Thickness(0,5,0,0)});row.Children.Add(details);
      var side=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};Grid.SetColumn(side,2);
      if(updatable.Contains(g.Id))side.Children.Add(Badge("Atualizar",Theme("Warning"),Theme("OnWarning"),null));
      DateTime last=Platforms.LastPlayed(g);side.Children.Add(new TextBlock{Text=last==DateTime.MinValue?"Nunca aberto":"Última abertura\n"+last.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),FontSize=12,Foreground=Muted,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center});row.Children.Add(side);
      return CardButton(g,row,true);
    }
    Button CardButton(Game g,FrameworkElement content,bool list){
      var button=new Button{Content=content,Tag="card",HorizontalContentAlignment=list?HorizontalAlignment.Stretch:HorizontalAlignment.Left,HorizontalAlignment=list?HorizontalAlignment.Stretch:HorizontalAlignment.Left,Padding=new Thickness(list?6:5),Margin=list?new Thickness(0,0,0,8):new Thickness(0,0,16,16),BorderBrush=g==selected?Theme("Accent"):Frozen(Color.FromRgb(42,46,55)),Background=list?Frozen(Color.FromArgb(220,24,27,33)):Frozen(Color.FromArgb(160,23,26,32)),ToolTip=g.Name};
      if(g==selected)button.BorderThickness=new Thickness(2);
      AutomationProperties.SetName(button,"Selecionar "+g.Name);Game current=g;
      button.Click+=delegate{Select(current,false);};
      button.MouseDoubleClick+=delegate{Select(current,false);Launch();};
      return button;
    }
    void Select(Game game,bool focus){
      selected=game;Refresh();if(!focus)return;
      Panel panel=library.ViewMode=="list"?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");int index=visible.IndexOf(game);
      if(index>=0&&index<panel.Children.Count){var card=(Button)panel.Children[index];card.Focus();card.BringIntoView();}
    }

    // Keyboard: Ctrl+F search, arrows move the selection, Enter plays, Esc clears the search.
    void OnKeyDown(object sender,KeyEventArgs e){
      var search=Find<TextBox>("SearchBox");
      if(e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){search.Focus();search.SelectAll();e.Handled=true;return;}
      if(Keyboard.FocusedElement==search){if(e.Key==Key.Escape){search.Text="";FocusSelected();e.Handled=true;}else if(e.Key==Key.Down||e.Key==Key.Enter){FocusSelected();e.Handled=true;}return;}
      if(Keyboard.FocusedElement is TextBox||Keyboard.FocusedElement is ComboBox||Keyboard.FocusedElement is ComboBoxItem||Keyboard.FocusedElement is MenuItem)return;
      var focused=Keyboard.FocusedElement as Button;bool onCard=focused!=null&&(focused.Tag as string)=="card";
      switch(e.Key){
        case Key.Left:if(Move(-1))e.Handled=true;break;
        case Key.Right:if(Move(1))e.Handled=true;break;
        case Key.Up:if(Move(-Columns()))e.Handled=true;break;
        case Key.Down:if(Move(Columns()))e.Handled=true;break;
        case Key.Enter:if((onCard||focused==null)&&selected!=null){Launch();e.Handled=true;}break;
      }
    }
    void OnPad(PadButton button){
      switch(button){
        case PadButton.Left:Move(-1);break;
        case PadButton.Right:Move(1);break;
        case PadButton.Up:Move(-Columns());break;
        case PadButton.Down:Move(Columns());break;
        case PadButton.A:Launch();break;
        case PadButton.Y:ToggleFavorite();break;
        case PadButton.B:Find<TextBox>("SearchBox").Text="";platformFilter=Platforms.All[0];Find<ToggleButton>("FavoritesChip").IsChecked=false;RefreshGrid();break;
        case PadButton.Start:if(selected!=null)OpenMenu(Find<Button>("MoreButton"));break;
      }
    }
    void FocusSelected(){if(visible.Count==0)return;Select(selected!=null&&visible.Contains(selected)?selected:visible[0],true);}
    int Columns(){
      if(library.ViewMode=="list")return 1;var grid=Find<WrapPanel>("GameGrid");
      // Card width + padding (2×5) + border (2×1) + right margin (16).
      return Math.Max(1,(int)Math.Floor((grid.ActualWidth+1)/(library.GridSize+28)));
    }
    bool Move(int offset){
      if(visible.Count==0)return false;int index=selected==null?-1:visible.IndexOf(selected);
      int target=index<0?0:Math.Max(0,Math.Min(visible.Count-1,index+offset));Select(visible[target],true);return true;
    }

    void Configure(){
      if(selected==null)return;
      try{if(FolderLaunch.Supports(selected)){ConfigureSourceFolder();return;}if(GameContent.Supports(selected)){ConfigureContent();return;}Process.Start(Ports.ConfigurationStartInfo(selected));Status("Configuração aberta: "+selected.Name);}
      catch(Exception ex){MessageBox.Show(Window,ex.Message,"Configurar jogo",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    void ToggleFavorite(){if(selected==null)return;selected.Favorite=!selected.Favorite;Save();Refresh();}
    void OpenFolder(){
      if(selected==null)return;string folder=!String.IsNullOrEmpty(selected.ManagedInstallationRoot)&&Directory.Exists(selected.ManagedInstallationRoot)?selected.ManagedInstallationRoot:Path.GetDirectoryName(selected.Executable);
      if(String.IsNullOrEmpty(folder)||!Directory.Exists(folder)){Status("A pasta do port não foi encontrada. Atualize o caminho em Editar cadastro.");return;}
      Process.Start(new ProcessStartInfo("explorer.exe","\""+folder+"\""){UseShellExecute=true});
    }
    void RemoveFromCollection(){
      if(selected==null)return;
      if(MessageBox.Show(Window,"Remover "+selected.Name+" da coleção? Os arquivos do port serão preservados.","Remover da coleção",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
      library.Games.Remove(selected);selected=library.Games.FirstOrDefault();Save();Refresh();Status("Removido da coleção. Os arquivos do port foram preservados.");
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
    void DeletePort(){
      if(selected==null)return;Game game=selected;var dialog=new PortRemovalWindow(Window,game,library.Games,store.Root);if(dialog.ShowDialog()!=true)return;int index=library.Games.IndexOf(game);library.Games.Remove(game);
      try{Save();}catch(Exception ex){library.Games.Insert(index,game);store.Log(ex.ToString());MessageBox.Show(Window,"Os arquivos foram excluídos, mas não foi possível salvar a remoção do cadastro. Use Remover da coleção após resolver o problema de gravação.","Cadastro não salvo",MessageBoxButton.OK,MessageBoxImage.Warning);Refresh();return;}
      RemoveCachedArtwork(game,game.Artwork);selected=library.Games.FirstOrDefault();Refresh();Status("Port e arquivos das pastas confirmadas excluídos: "+game.Name);
    }
    void RemoveCachedArtwork(Game game,string file){try{if(String.IsNullOrEmpty(file)||!PortRemoval.Within(store.Cache,file)||!Path.GetFileName(file).StartsWith(game.Id+"-",StringComparison.OrdinalIgnoreCase)||library.Games.Any(g=>String.Equals(g.Artwork,file,StringComparison.OrdinalIgnoreCase)))return;imageCache.Remove(file);if(File.Exists(file))File.Delete(file);}catch(Exception ex){store.Log("Limpeza de artwork: "+ex.Message);}}
    bool ConfigureSourceFolder(){using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Escolha a pasta de "+selected.Name+" com "+FolderLaunch.RequiredFile(selected),SelectedPath=selected.SourceGameDirectory??""}){if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return false;FolderLaunch.Validate(selected,picker.SelectedPath);selected.SourceGameDirectory=picker.SelectedPath;Save();Refresh();Status("Pasta do jogo configurada.");return true;}}
    bool ConfigureContent(){var dialog=new GameContentWindow(Window,selected);dialog.ShowDialog();if(dialog.PreparedPath!=null){Save();Refresh();Status("Arquivos preparados para "+selected.Name);return true;}return !GameContent.NeedsSetup(selected);}
    void Launch(){
      if(selected==null)return;
      try{if(FolderLaunch.NeedsSetup(selected)&&!ConfigureSourceFolder())return;if(GameContent.NeedsSetup(selected)&&!ConfigureContent())return;var start=Ports.StartInfo(selected);FolderLaunch.PrepareLaunch(selected,start);Process.Start(start);selected.LastPlayed=DateTime.UtcNow.ToString("o");Save();Refresh();Status("Inicialização solicitada: "+selected.Name);if(library.MinimizeOnLaunch)Window.WindowState=WindowState.Minimized;}
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
        if(live==null){RemoveCachedArtwork(snapshot,result.File);return;}if(live.Artwork!=snapshot.Artwork||live.Name!=snapshot.Name||live.Executable!=snapshot.Executable||live.ArtworkSearchTitle!=snapshot.ArtworkSearchTitle)return;
        if(!String.IsNullOrEmpty(result.File)){live.Artwork=result.File;live.ArtworkSource=result.Source;Save();Refresh();Status("Artwork pronta: "+live.Name+" · "+result.Source);}else Status(result.Message);
      }catch(OperationCanceledException){}catch(Exception ex){if(!closing){store.Log(ex.ToString());Status("Não foi possível buscar a artwork. Você pode escolher uma imagem local.");}}
      finally{pending.Remove(game.Id);if(entered)artQueue.Release();}
    }
    void Settings(){
      var dialog=new Window{Title="Configurações · NEXUS",Owner=Window,Width=560,Height=530,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Window.Background,Foreground=Window.Foreground,FontFamily=Window.FontFamily};
      dialog.Resources=Window.Resources;var panel=new StackPanel{Margin=new Thickness(26)};dialog.Content=panel;
      panel.Children.Add(new TextBlock{Text="Artworks e biblioteca",FontSize=23,Margin=new Thickness(0,0,0,14)});
      var online=new CheckBox{Content="Buscar imagens online automaticamente",IsChecked=library.OnlineArtwork};panel.Children.Add(online);
      var minimize=new CheckBox{Content="Minimizar NEXUS ao abrir um jogo",IsChecked=library.MinimizeOnLaunch};panel.Children.Add(minimize);
      var autoUpdate=new CheckBox{Content="Atualizar e reiniciar o NEXUS automaticamente ao abrir",IsChecked=library.AutoUpdateLauncher};panel.Children.Add(autoUpdate);
      panel.Children.Add(new TextBlock{Text="SteamGridDB · chave de API opcional",Margin=new Thickness(0,14,0,8)});
      var key=new PasswordBox{Password=library.ApiKey(),Padding=new Thickness(10),Background=new SolidColorBrush(Color.FromRgb(29,32,39)),Foreground=Brushes.White};panel.Children.Add(key);
      panel.Children.Add(new TextBlock{Text="Sem chave: busca a imagem da página exata na Wikipedia. Com SteamGridDB: prioriza fundos panorâmicos. Só o nome do jogo é enviado; caminhos locais ficam no PC.",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(163,173,189)),Margin=new Thickness(0,12,0,18),FontSize=12});
      var link=new Button{Content="Obter chave do SteamGridDB"};link.Click+=delegate{Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api"){UseShellExecute=true});};panel.Children.Add(link);
      var actions=new WrapPanel{Margin=new Thickness(0,22,0,0)};panel.Children.Add(actions);var save=new Button{Content="Salvar",Style=(Style)Application.Current.Resources["PrimaryButton"]};var folder=new Button{Content="Abrir dados locais"};actions.Children.Add(save);actions.Children.Add(folder);
      folder.Click+=delegate{Process.Start(new ProcessStartInfo("explorer.exe", "\""+store.Root+"\""){UseShellExecute=true});};
      save.Click+=delegate{library.AutoUpdateLauncher=autoUpdate.IsChecked==true;library.OnlineArtwork=online.IsChecked==true;library.MinimizeOnLaunch=minimize.IsChecked==true;library.SetApiKey(key.Password);Save();dialog.DialogResult=true;};dialog.ShowDialog();
    }

    public void Capture(string target){
      var args=Environment.GetCommandLineArgs();
      if(args.Contains("--removal-smoke")){var dialog=new PortRemovalWindow(Window,new Game{Name="Port de exemplo",Executable=Path.Combine(store.Root,"Example.exe")},library.Games,store.Root,true);dialog.Show();dialog.UpdateLayout();CaptureWindow(dialog,target);dialog.Close();return;}
      if(args.Contains("--self-update-smoke")){var dialog=new SelfUpdateWindow(Window,null,new PortRelease{Version="v1.6.4"},store.Root,true);dialog.Show();dialog.UpdateLayout();CaptureWindow(dialog,target);dialog.Close();return;}
      if(args.Contains("--package-smoke")){var dialog=new PackageChoiceWindow(Window,"Exemplo de pacote",new[]{"Game.exe","Launcher.exe","bin\\\\Runtime.exe"},null);dialog.Show();dialog.UpdateLayout();CaptureWindow(dialog,target);dialog.Close();return;}
      if(args.Contains("--content-smoke")){Game sample=library.Games.FirstOrDefault(GameContent.Supports)??new Game{Name="BLACK",Executable=Path.Combine(store.Root,"BLACK.exe")};var dialog=new GameContentWindow(Window,sample);dialog.Show();dialog.UpdateLayout();CaptureWindow(dialog,target);dialog.Close();return;}
      if(args.Contains("--catalog-smoke")||args.Contains("--updates-smoke")){var catalogWindow=new CatalogWindow(Window,store.Root,library,delegate(Game g){},CatalogTab.All){IsSmoke=true};catalogWindow.Show();catalogWindow.UpdateLayout();if(args.Contains("--updates-smoke"))catalogWindow.CaptureUpdateSmoke(target);else catalogWindow.CaptureSmoke(target);catalogWindow.Close();return;}
      CaptureWindow(Window,target);
    }
    public void PreviewView(string mode,int size){
      Find<ToggleButton>(mode=="list"?"ListViewButton":"GridViewButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
      if(size>0){if(mode=="list")library.ListSize=size;else library.GridSize=size;library.NormalizeView();UpdateViewControls();RefreshGrid();}
      if(library.ViewMode!=mode)throw new Exception("View switch failed");Panel shown=mode=="list"?(Panel)Find<StackPanel>("GameList"):Find<WrapPanel>("GameGrid");
      if(shown.Visibility!=Visibility.Visible||shown.Children.Count!=library.Games.Count)throw new Exception("View rendering failed");
      if(library.Games.Count>0){var lastButton=(Button)shown.Children[shown.Children.Count-1];string expected=(string)lastButton.ToolTip;lastButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));if(selected==null||selected.Name!=expected)throw new Exception("Selection failed");Find<TextBox>("SearchBox").Text=expected;if(shown.Children.Count<1)throw new Exception("Search in view failed");Find<TextBox>("SearchBox").Text="";}
      var chips=Find<WrapPanel>("PlatformChips").Children.Cast<ToggleButton>().Select(c=>(string)c.Tag).ToList();
      if(chips.Count!=1+library.Games.Select(g=>Platforms.Resolve(g)).Distinct().Count())throw new Exception("Platform chips must list only platforms present in the collection");
      foreach(string platform in chips){var chip=Find<WrapPanel>("PlatformChips").Children.Cast<ToggleButton>().First(c=>(string)c.Tag==platform);chip.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));if(shown.Children.Count!=Platforms.Filter(library.Games,platform,"",false).Count)throw new Exception("Platform rendering failed: "+platform);}
      platformFilter=Platforms.All[0];RefreshGrid();
      if(visible.Count>1){Select(visible[0],true);Move(1);if(selected!=visible[1])throw new Exception("Keyboard navigation failed");}
      foreach(Game game in library.Games){selected=game;Refresh();bool configured=GameContent.Supports(game)||FolderLaunch.Supports(game)||!String.IsNullOrEmpty(Ports.FindConfigurationExecutable(game));if((Find<Button>("ConfigureButton").Visibility==Visibility.Visible)!=configured)throw new Exception("Optional configuration action failed");}
      int beforeGrid=library.GridSize,beforeList=library.ListSize;SetView(mode=="list"?"grid":"list");SetView(mode);if(library.GridSize!=beforeGrid||library.ListSize!=beforeList)throw new Exception("Independent size restoration failed");
      foreach(string sortMode in new[]{"name","recent","platform","favorites"}){library.SortMode=sortMode;RefreshGrid();if(shown.Children.Count!=library.Games.Count)throw new Exception("Sort failed: "+sortMode);}
    }
    // Renders only the client area, so screenshots no longer carry the empty strip of the window frame.
    public static void CaptureWindow(Window window,string target){
      window.UpdateLayout();var content=(FrameworkElement)window.Content;int width=(int)Math.Ceiling(content.ActualWidth),height=(int)Math.Ceiling(content.ActualHeight);
      var background=new DrawingVisual();using(var context=background.RenderOpen())context.DrawRectangle(window.Background??Brushes.Black,null,new Rect(0,0,width,height));
      var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(background);image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));using(var output=File.Create(target))encoder.Save(output);
    }
  }
  public class GameEditor:Window {
    public Game Result;public bool Remove;public bool RefreshArtwork;
    readonly ComboBox platform;readonly TextBox name,exe,arguments,working,configExe,configArgs,artTitle;readonly Store store;readonly TextBlock artLabel;string picked;
    public GameEditor(Window owner,Game game,Store data,bool existing){
      Result=game;store=data;Owner=owner;Title=existing?"Editar cadastro · NEXUS":"Adicionar jogo · NEXUS";Width=700;Height=830;MinHeight=580;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var scroll=new ScrollViewer();Content=scroll;var p=new StackPanel{Margin=new Thickness(26)};scroll.Content=p;
      p.Children.Add(new TextBlock{Text=existing?"Seu jogo, do seu jeito":"Adicionar à coleção",FontSize=25,Margin=new Thickness(0,0,0,20)});
      p.Children.Add(new TextBlock{Text="Plataforma de origem",Margin=new Thickness(0,0,0,7)});platform=new ComboBox{ItemsSource=Platforms.All.Skip(1).ToArray(),SelectedItem=Platforms.Resolve(game),Margin=new Thickness(0,0,0,10)};AutomationProperties.SetName(platform,"Plataforma de origem");p.Children.Add(platform);
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
      var actions=new WrapPanel{Margin=new Thickness(0,26,0,0)};p.Children.Add(actions);var save=new Button{Content="Salvar",Style=(Style)Application.Current.Resources["PrimaryButton"],IsDefault=true};var cancel=new Button{Content="Cancelar",IsCancel=true};actions.Children.Add(save);actions.Children.Add(cancel);
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
