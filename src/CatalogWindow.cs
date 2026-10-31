using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nexus {
  // Badge colors for project types and installation modes: background, then text.
  static class CatalogColors {
    static readonly Dictionary<string,string[]> palette=new Dictionary<string,string[]>{
      {ProjectTypes.Port,new[]{"#1E3350","#A9CCFF"}},{ProjectTypes.Recompilation,new[]{"#33264D","#D4B8FF"}},{ProjectTypes.Decompilation,new[]{"#173B3B","#9DE3DA"}},
      {ProjectTypes.Reimplementation,new[]{"#3B2F17","#F5D58A"}},{ProjectTypes.Compatibility,new[]{"#2C313B","#C2C9D4"}},
      {InstallModes.Integrated,new[]{"#2C3D1C","#C6F59A"}},{InstallModes.Wizard,new[]{"#173B3B","#9DE3DA"}},{InstallModes.OfficialSite,new[]{"#1E3350","#A9CCFF"}},
      {InstallModes.Manual,new[]{"#4A3216","#FFCC8A"}},{InstallModes.InDevelopment,new[]{"#2C313B","#C2C9D4"}}};
    public static Brush Background(string key){string[] value;return Brush(palette.TryGetValue(key??"",out value)?value[0]:"#2C313B");}
    public static Brush Foreground(string key){string[] value;return Brush(palette.TryGetValue(key??"",out value)?value[1]:"#C2C9D4");}
    // Tile behind the platform code in the list: PlayStation blue, Xbox green, Nintendo red.
    public static Brush Platform(string platform){
      string p=platform??"";return Brush(p.StartsWith("PlayStation")||p=="PSP"?"#1E3350":p.StartsWith("Xbox")?"#1F3A22":p=="Outras"||p=="Dreamcast"?"#2C313B":"#3D2026");
    }
    static Brush Brush(string hex){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));brush.Freeze();return brush;}
  }

  public class CatalogWindow:Window {
    readonly PortCatalog catalog=new PortCatalog();readonly ListBox list;readonly TextBlock title,repo,version,details,status,subtitle,resultLabel;readonly TextBox destination;readonly Button download,choose,cancel,refresh;readonly ProgressBar progress;
    readonly CheckBox acceptBeta;readonly Button importZip,checkUpdates,project;readonly TextBox search;readonly Action<Game> added;readonly List<Game> games;readonly Library library;readonly string dataRoot;CancellationTokenSource request,installation,updateChecks;PortEntry selected;PortRelease release;bool busy,closed;
    readonly Dictionary<string,PortRelease> available=new Dictionary<string,PortRelease>();
    readonly StackPanel platformList;readonly WrapPanel typeChips,modeChips,badges;readonly ToggleButton newsTab,updatesTab,allTab;readonly ComboBox sort;
    readonly CatalogQuery query=new CatalogQuery{Platform=Platforms.All[0]};readonly HashSet<string> seenAtOpen;
    CatalogTab tab;
    readonly SolidColorBrush muted=new SolidColorBrush(Color.FromRgb(167,178,194));
    // Identities with a confirmed newer release, read by the main window to refresh its badges.
    public readonly HashSet<string> UpdatesFound=new HashSet<string>();
    public IEnumerable<PortEntry> Entries {get{return catalog.Entries;}}
    public bool IsSmoke;
    Brush Theme(string key){return (Brush)Application.Current.Resources[key];}
    Style ThemeStyle(string key){return (Style)Application.Current.Resources[key];}

    public CatalogWindow(Window owner,string dataRoot,Library data,Action<Game> onAdded,CatalogTab initialTab){
      Owner=owner;Title="Catálogo · NEXUS";Width=1240;Height=800;MinWidth=980;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;FontSize=14;Resources=owner.Resources;
      this.dataRoot=dataRoot;library=data;games=data.Games;added=onAdded;tab=initialTab;seenAtOpen=new HashSet<string>(data.SeenCatalogIds??catalog.Entries.Select(e=>e.Id));
      var root=new Grid();Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});

      // Header: title, catalog size and search.
      var header=new Border{Padding=new Thickness(28,18,28,16),BorderBrush=Theme("Line"),BorderThickness=new Thickness(0,0,0,1)};root.Children.Add(header);
      var headerDock=new DockPanel{LastChildFill=false};header.Child=headerDock;
      var heading=new StackPanel();heading.Children.Add(new TextBlock{Text="Catálogo",FontSize=26,FontWeight=FontWeights.SemiBold});subtitle=new TextBlock{Foreground=muted,Margin=new Thickness(0,4,0,0)};heading.Children.Add(subtitle);headerDock.Children.Add(heading);
      var searchBox=new Grid{Width=340,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(searchBox,Dock.Right);headerDock.Children.Add(searchBox);
      search=new TextBox{Padding=new Thickness(12,9,12,9)};System.Windows.Automation.AutomationProperties.SetName(search,"Buscar jogo, port ou repositório");searchBox.Children.Add(search);
      var placeholder=new TextBlock{Text="Buscar jogo, port ou repositório",Foreground=new SolidColorBrush(Color.FromRgb(149,159,175)),Margin=new Thickness(14,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};searchBox.Children.Add(placeholder);
      search.TextChanged+=delegate{placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(!busy){query.Search=search.Text;FilterEntries();}};

      var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(250)});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(410)});Grid.SetRow(columns,1);root.Children.Add(columns);

      // Left: filters.
      var filterScroll=new ScrollViewer{BorderBrush=Theme("Line")};var filterBorder=new Border{BorderBrush=Theme("Line"),BorderThickness=new Thickness(0,0,1,0),Child=filterScroll};columns.Children.Add(filterBorder);
      var filters=new StackPanel{Margin=new Thickness(24,20,18,24)};filterScroll.Content=filters;
      filters.Children.Add(new TextBlock{Text="PLATAFORMA",Style=ThemeStyle("SectionLabel")});platformList=new StackPanel{Margin=new Thickness(0,0,0,20)};filters.Children.Add(platformList);
      filters.Children.Add(new TextBlock{Text="TIPO DE PROJETO",Style=ThemeStyle("SectionLabel")});typeChips=new WrapPanel{Margin=new Thickness(0,0,0,16)};filters.Children.Add(typeChips);
      foreach(string type in ProjectTypes.All)typeChips.Children.Add(FilterChip(type,query.ProjectTypes));
      filters.Children.Add(new TextBlock{Text="INSTALAÇÃO",Style=ThemeStyle("SectionLabel")});modeChips=new WrapPanel{Margin=new Thickness(0,0,0,16)};filters.Children.Add(modeChips);
      foreach(string mode in InstallModes.All)modeChips.Children.Add(FilterChip(mode,query.InstallModes));
      var clear=new Button{Content="Limpar filtros",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7)};clear.Click+=delegate{ClearFilters();};filters.Children.Add(clear);

      // Middle: tabs and project list.
      var middle=new DockPanel{Margin=new Thickness(22,18,22,18)};Grid.SetColumn(middle,1);columns.Children.Add(middle);
      var toolbar=new DockPanel{Margin=new Thickness(0,0,0,12),LastChildFill=false};DockPanel.SetDock(toolbar,Dock.Top);middle.Children.Add(toolbar);
      var tabGroup=new Border{Style=ThemeStyle("SegmentGroup")};var tabRow=new StackPanel{Orientation=Orientation.Horizontal};tabGroup.Child=tabRow;toolbar.Children.Add(tabGroup);
      newsTab=TabButton("Novidades",CatalogTab.News);allTab=TabButton("Todos",CatalogTab.All);updatesTab=TabButton("Atualizações",CatalogTab.Updates);tabRow.Children.Add(newsTab);tabRow.Children.Add(updatesTab);tabRow.Children.Add(allTab);
      sort=new ComboBox{Width=170,Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center};sort.Items.Add("Ordenar por nome");sort.Items.Add("Ordenar por plataforma");sort.Items.Add("Ordenar por tipo");sort.SelectedIndex=0;sort.SelectionChanged+=delegate{if(!busy)FilterEntries();};DockPanel.SetDock(sort,Dock.Right);toolbar.Children.Add(sort);
      resultLabel=new TextBlock{Foreground=muted,FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0)};toolbar.Children.Add(resultLabel);
      list=new ListBox{Background=Brushes.Transparent,Foreground=Foreground,BorderThickness=new Thickness(0),Padding=new Thickness(0)};
      ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);list.ItemContainerStyle=ListItemStyle();middle.Children.Add(list);

      // Right: selected project.
      var detailBorder=new Border{BorderBrush=Theme("Line"),BorderThickness=new Thickness(1,0,0,0)};Grid.SetColumn(detailBorder,2);columns.Children.Add(detailBorder);
      var scroll=new ScrollViewer();detailBorder.Child=scroll;var right=new StackPanel{Margin=new Thickness(24,20,24,24)};scroll.Content=right;
      badges=new WrapPanel{Margin=new Thickness(0,0,0,12)};right.Children.Add(badges);
      title=new TextBlock{Text="Escolha um port",FontSize=24,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};right.Children.Add(title);
      repo=new TextBlock{Foreground=Theme("Accent"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,14),FontSize=13};right.Children.Add(repo);
      version=new TextBlock{FontSize=16,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};right.Children.Add(version);
      details=new TextBlock{Foreground=muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16),LineHeight=20};right.Children.Add(details);
      var links=new WrapPanel{Margin=new Thickness(0,0,0,18)};right.Children.Add(links);
      project=new Button{Content="Abrir projeto",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,8)};project.Click+=delegate{if(selected!=null)Open(selected.ProjectUrl);};links.Children.Add(project);
      refresh=new Button{Content="Consultar novamente",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,8)};refresh.Click+=delegate{LoadSelected(true);};links.Children.Add(refresh);
      checkUpdates=new Button{Content="Verificar atualizações",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,8)};checkUpdates.Click+=delegate{CheckUpdates();};links.Children.Add(checkUpdates);
      right.Children.Add(new TextBlock{Text="PASTA DE INSTALAÇÃO",Style=ThemeStyle("SectionLabel")});
      var folderRow=new DockPanel{Margin=new Thickness(0,0,0,6)};right.Children.Add(folderRow);
      choose=new Button{Content="Escolher…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};DockPanel.SetDock(choose,Dock.Right);folderRow.Children.Add(choose);
      destination=new TextBox{Text=Path.Combine(dataRoot,"ports")};folderRow.Children.Add(destination);
      choose.Click+=delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Escolha a pasta onde o NEXUS instalará os ports",SelectedPath=Directory.Exists(destination.Text)?destination.Text:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}){if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)destination.Text=picker.SelectedPath;}};
      right.Children.Add(new Border{Background=Theme("SurfaceRaised"),BorderBrush=new SolidColorBrush(Color.FromRgb(46,51,61)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,12,0,16),Child=new TextBlock{Text="O NEXUS baixa apenas o port. Os arquivos do jogo vêm da sua própria cópia dumpada; consulte as instruções acima.",Foreground=Theme("TextSoft"),TextWrapping=TextWrapping.Wrap,FontSize=12}});
      acceptBeta=new CheckBox{Content="Aceito instalar esta versão beta / experimental",Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,14)};acceptBeta.Checked+=delegate{UpdateInstallState();};acceptBeta.Unchecked+=delegate{UpdateInstallState();};right.Children.Add(acceptBeta);
      var actions=new WrapPanel();right.Children.Add(actions);
      download=new Button{Content="Baixar e adicionar",IsEnabled=false,Style=ThemeStyle("PrimaryButton"),Padding=new Thickness(20,11,20,11),Margin=new Thickness(0,0,8,8)};
      importZip=new Button{Content="Importar ZIP e adicionar",Visibility=Visibility.Collapsed,Padding=new Thickness(16,11,16,11),Margin=new Thickness(0,0,8,8)};
      cancel=new Button{Content="Cancelar download",IsEnabled=false,Padding=new Thickness(16,11,16,11),Margin=new Thickness(0,0,8,8)};
      importZip.Click+=delegate{if(selected==null)return;var picker=new Microsoft.Win32.OpenFileDialog{Title="Importar o ZIP oficial de "+selected.Name,Filter="Pacote oficial|"+selected.ManualPackageFile+"|ZIP|*.zip",CheckFileExists=true};if(picker.ShowDialog(this)==true)InstallSelected(picker.FileName);};
      actions.Children.Add(download);actions.Children.Add(importZip);actions.Children.Add(cancel);download.Click+=delegate{InstallSelected();};cancel.Click+=delegate{if(installation!=null)installation.Cancel();};
      progress=new ProgressBar{Minimum=0,Maximum=100,Height=6,Margin=new Thickness(0,14,0,0),Foreground=Theme("Accent"),Background=new SolidColorBrush(Color.FromRgb(38,43,51)),BorderThickness=new Thickness(0)};right.Children.Add(progress);

      status=new TextBlock{Text="Consulte uma versão para começar.",Foreground=muted,TextWrapping=TextWrapping.Wrap};
      var statusBar=new Border{Padding=new Thickness(28,9,28,9),BorderBrush=Theme("Line"),BorderThickness=new Thickness(0,1,0,0),Child=status};Grid.SetRow(statusBar,2);root.Children.Add(statusBar);

      RefreshPlatforms();UpdateTabs();FilterEntries();
      list.SelectionChanged+=delegate{if(!busy&&!IsSmoke)LoadSelected();};
      Closed+=delegate{closed=true;MarkSeen();if(request!=null)request.Cancel();if(installation!=null)installation.Cancel();if(updateChecks!=null)updateChecks.Cancel();catalog.Dispose();};
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;status.Text="Use Cancelar download e aguarde a limpeza antes de fechar.";}};
      Loaded+=delegate{if(!IsSmoke)LoadRemoteAndUpdates();};
    }

    // The online catalog may add profiles published after this NEXUS version; it replaces the bundled list only when valid.
    async void LoadRemoteAndUpdates(){
      if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;
      bool replaced=false;try{replaced=await catalog.RefreshFromRemote(dataRoot,CancellationToken.None);}catch(Exception){}
      if(closed)return;
      if(replaced&&!busy){string keep=selected!=null?selected.Id:null;RefreshPlatforms();UpdateTabs();FilterEntries();var again=list.Items.Cast<ListBoxItem>().FirstOrDefault(i=>((PortEntry)i.Tag).Id==keep);if(again!=null)list.SelectedItem=again;}
      CheckUpdates();
    }
    void MarkSeen(){var seen=new HashSet<string>(library.SeenCatalogIds??new List<string>());seen.UnionWith(catalog.Entries.Select(e=>e.Id));library.SeenCatalogIds=seen.ToList();}

    ToggleButton FilterChip(string value,HashSet<string> target){
      var chip=new ToggleButton{Content=value,Style=ThemeStyle("Chip"),FontSize=12,Padding=new Thickness(11,5,11,5),Tag=value};
      chip.Click+=delegate{if(chip.IsChecked==true)target.Add(value);else target.Remove(value);if(!busy)FilterEntries();};return chip;
    }
    ToggleButton TabButton(string text,CatalogTab value){
      var button=new ToggleButton{Style=ThemeStyle("Segment"),Tag=value,Padding=new Thickness(14,6,14,6)};button.Content=new TextBlock{Text=text};
      button.Click+=delegate{SetTab(value);};return button;
    }
    public void SetTab(CatalogTab value){tab=value;UpdateTabs();if(!busy){FilterEntries();if(value==CatalogTab.Updates)CheckUpdates();}}
    void UpdateTabs(){
      int news=catalog.Entries.Count(e=>!seenAtOpen.Contains(e.Id)),updates=catalog.Entries.Count(HasUpdate);
      ((TextBlock)newsTab.Content).Text=news>0?"Novidades · "+news:"Novidades";((TextBlock)updatesTab.Content).Text=updates>0?"Atualizações · "+updates:"Atualizações";
      newsTab.IsChecked=tab==CatalogTab.News;updatesTab.IsChecked=tab==CatalogTab.Updates;allTab.IsChecked=tab==CatalogTab.All;
      subtitle.Text=catalog.Entries.Count+" projetos · "+catalog.Entries.Select(e=>e.Platform).Distinct().Count()+" plataformas · somente ports para Windows";
    }
    public void SetPlatform(string platform){query.Platform=platform;RefreshPlatforms();if(!busy)FilterEntries();}
    public void SetModes(params string[] modes){query.InstallModes.Clear();query.InstallModes.UnionWith(modes);foreach(ToggleButton chip in modeChips.Children)chip.IsChecked=query.InstallModes.Contains((string)chip.Tag);if(!busy)FilterEntries();}
    void ClearFilters(){query.ProjectTypes.Clear();query.InstallModes.Clear();foreach(ToggleButton chip in typeChips.Children.Cast<ToggleButton>().Concat(modeChips.Children.Cast<ToggleButton>()))chip.IsChecked=false;search.Text="";SetPlatform(Platforms.All[0]);}
    void RefreshPlatforms(){
      platformList.Children.Clear();
      foreach(string name in Platforms.All){
        int count=name==Platforms.All[0]?catalog.Entries.Count:catalog.Entries.Count(e=>e.Platform==name);if(count==0)continue;
        bool active=query.Platform==name;var row=new DockPanel();var counter=new TextBlock{Text=count.ToString(),Foreground=muted,FontSize=12,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(counter,Dock.Right);row.Children.Add(counter);row.Children.Add(new TextBlock{Text=name,VerticalAlignment=VerticalAlignment.Center});
        var button=new ToggleButton{Style=ThemeStyle("FilterRow"),Content=row,IsChecked=active,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,0,2),Tag=name};
        string platform=name;button.Click+=delegate{SetPlatform(platform);};platformList.Children.Add(button);
      }
    }
    Style ListItemStyle(){
      var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0,0,0,6)));style.Setters.Add(new Setter(Control.CursorProperty,System.Windows.Input.Cursors.Hand));
      var border=new FrameworkElementFactory(typeof(Border),"Card");border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.PaddingProperty,new Thickness(10,9,12,9));
      border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(21,23,28)));border.SetValue(Border.BorderBrushProperty,Theme("Line"));border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
      var template=new ControlTemplate(typeof(ListBoxItem)){VisualTree=border};
      var selectedTrigger=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty,Theme("Accent"),"Card"));selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(28,34,25)),"Card"));template.Triggers.Add(selectedTrigger);
      var hoverTrigger=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty,Theme("ControlLine"),"Card"));template.Triggers.Add(hoverTrigger);
      style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
    }

    void FilterEntries(){
      if(list==null)return;if(request!=null){request.Cancel();request=null;}selected=null;release=null;list.Items.Clear();
      IEnumerable<PortEntry> entries=PortCatalog.Filter(catalog.Entries,query);
      if(tab==CatalogTab.News)entries=entries.Where(e=>!seenAtOpen.Contains(e.Id));
      if(tab==CatalogTab.Updates)entries=entries.Where(HasUpdate);
      if(sort!=null&&sort.SelectedIndex==1)entries=entries.OrderBy(e=>Array.IndexOf(Platforms.All,e.Platform)).ThenBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase);
      if(sort!=null&&sort.SelectedIndex==2)entries=entries.OrderBy(e=>Array.IndexOf(ProjectTypes.All,e.ProjectType)).ThenBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase);
      foreach(var entry in entries)list.Items.Add(new ListBoxItem{Tag=entry,Content=ItemContent(entry)});
      if(download==null)return;download.IsEnabled=false;title.Text="Escolha um port";repo.Text="";details.Text="";version.Text="";badges.Children.Clear();acceptBeta.Visibility=Visibility.Collapsed;importZip.Visibility=Visibility.Collapsed;download.Visibility=Visibility.Visible;
      resultLabel.Text=list.Items.Count==1?"1 projeto":list.Items.Count+" projetos";
      status.Text=list.Items.Count>0?list.Items.Count+" projetos com estes filtros.":tab==CatalogTab.News?"Nenhuma novidade desde a sua última visita. Veja Todos para o catálogo completo.":tab==CatalogTab.Updates?"Nenhuma atualização confirmada para os ports da sua coleção.":"Nenhum projeto com estes filtros. Você pode adicionar sua instalação manualmente na coleção.";
      if(list.Items.Count>0&&!IsSmoke)list.SelectedIndex=0;
    }
    FrameworkElement ItemContent(PortEntry entry){
      var row=new DockPanel();
      var tile=new Border{Width=46,Height=46,CornerRadius=new CornerRadius(6),Background=CatalogColors.Platform(entry.Platform),Margin=new Thickness(0,0,12,0),Child=new TextBlock{Text=Platforms.ShortName(entry.Platform),FontSize=11,FontWeight=FontWeights.Bold,Foreground=Theme("Text"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap}};row.Children.Add(tile);
      var tags=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};DockPanel.SetDock(tags,Dock.Right);row.Children.Add(tags);
      tags.Children.Add(Pill(entry.ProjectType));tags.Children.Add(Pill(entry.InstallMode));
      var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};row.Children.Add(text);
      var top=new WrapPanel();top.Children.Add(new TextBlock{Text=entry.Name,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});
      if(!seenAtOpen.Contains(entry.Id))top.Children.Add(Flag("NOVO",Theme("Accent"),Theme("OnAccent")));
      if(Installed(entry)!=null)top.Children.Add(Flag("NA COLEÇÃO",Brushes.Transparent,Theme("TextSoft")));
      text.Children.Add(top);
      text.Children.Add(new TextBlock{Text=Subtitle(entry),Foreground=muted,FontSize=12,Margin=new Thickness(0,4,0,0),TextTrimming=TextTrimming.CharacterEllipsis});
      return row;
    }
    static Border Pill(string text){return new Border{Background=CatalogColors.Background(text),CornerRadius=new CornerRadius(5),Padding=new Thickness(7,3,7,3),Margin=new Thickness(6,0,0,0),Child=new TextBlock{Text=text,FontSize=11,FontWeight=FontWeights.Bold,Foreground=CatalogColors.Foreground(text)}};}
    Border Flag(string text,Brush background,Brush foreground){var flag=new Border{Background=background,CornerRadius=new CornerRadius(4),Padding=new Thickness(6,1,6,1),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock{Text=text,FontSize=10,FontWeight=FontWeights.Bold,Foreground=foreground}};if(background==Brushes.Transparent){flag.BorderBrush=Theme("ControlLine");flag.BorderThickness=new Thickness(1);}return flag;}
    void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}
    void PrepareSelection(PortEntry entry){
      selected=entry;release=null;title.Text=entry.Name;repo.Text=entry.ProjectUrl.Replace("https://","")+"  ↗";
      badges.Children.Clear();badges.Children.Add(new Border{Style=ThemeStyle("Badge"),Child=new TextBlock{Text=entry.Platform,Style=ThemeStyle("BadgeText")}});badges.Children.Add(Pill(entry.ProjectType));badges.Children.Add(Pill(entry.InstallMode));
      acceptBeta.Visibility=Visibility.Collapsed;acceptBeta.IsChecked=false;importZip.Visibility=entry.ManualPackage?Visibility.Visible:Visibility.Collapsed;download.Visibility=Visibility.Visible;download.Content=entry.ManualPackage?"Baixar no site oficial":"Baixar e adicionar";details.Text=entry.Notes;version.Text="Consultando versão…";download.IsEnabled=false;status.Text="Consultando Releases do GitHub…";progress.Value=0;destination.IsEnabled=!entry.ProjectOnly;choose.IsEnabled=!entry.ProjectOnly;refresh.IsEnabled=!entry.ProjectOnly;
    }
    async void LoadSelected(bool force=false){
      if(list.SelectedItem==null)return;if(request!=null){request.Cancel();request.Dispose();}request=new CancellationTokenSource();var current=request;
      PortEntry entry=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;PrepareSelection(entry);
      if(entry.ManualPackage){string host=new Uri(entry.SourceUrl).Host;version.Text="Download oficial em "+host+" · importar ZIP";download.IsEnabled=true;importZip.IsEnabled=true;status.Text="Baixe "+entry.ManualPackageFile+" no site oficial. Depois use Importar ZIP e adicionar; o NEXUS extrai e cadastra "+entry.Name+".";return;}
      if(entry.ProjectOnly){version.Text="Download e preparação no projeto original";download.Content="Ver downloads oficiais";download.IsEnabled=true;status.Text="Abre as Releases oficiais no navegador. A instalação e o cadastro deste perfil ainda são manuais; projetos em desenvolvimento podem não ter pacote pronto.";return;}
      try{var found=await catalog.Latest(entry,current.Token,force);if(closed||current!=request)return;PresentRelease(entry,found);
      }catch(OperationCanceledException){if(current==request&&!closed){version.Text="Consulta cancelada ou tempo esgotado.";status.Text="Use Consultar novamente para tentar outra vez.";}}
      catch(Exception ex){if(current==request&&!closed){version.Text="Download indisponível";status.Text=ex.Message;}}
    }
    Game Installed(PortEntry entry){return games.LastOrDefault(g=>g.Repository==entry.Identity&&UpdateManager.Managed(g));}
    bool HasUpdate(PortEntry entry){PortRelease found;Game installed;if(!available.TryGetValue(entry.Identity,out found)||found.FromCache||(installed=Installed(entry))==null)return false;try{return UpdateManager.Available(installed,found);}catch{return false;}}
    string Subtitle(PortEntry entry){
      string source=entry.Repository??new Uri(entry.SourceUrl).Host;
      return HasUpdate(entry)?entry.Platform+" · Atualização disponível":entry.Platform+" · "+source+(entry.AllowPrerelease?" · beta":"");
    }
    async void CheckUpdates(){
      if(busy||closed||IsSmoke||updateChecks!=null)return;var targets=catalog.Entries.Where(e=>!e.ProjectOnly&&!e.ManualPackage&&Installed(e)!=null).ToArray();if(targets.Length==0){if(tab==CatalogTab.Updates)status.Text="Nenhuma instalação gerenciada pelo NEXUS para consultar.";return;}
      updateChecks=new CancellationTokenSource();checkUpdates.IsEnabled=false;int count=0,failed=0;
      try{foreach(var entry in targets){if(closed||updateChecks.IsCancellationRequested)break;try{var found=await catalog.Latest(entry,updateChecks.Token,true);available[entry.Identity]=found;if(HasUpdate(entry)){count++;UpdatesFound.Add(entry.Identity);}else if(found.FromCache)failed++;}catch(OperationCanceledException){break;}catch{failed++;}if(!closed&&!busy)status.Text="Consultando atualizações da biblioteca…";}
        if(!closed){
          foreach(var item in list.Items.Cast<ListBoxItem>()){var entry=(PortEntry)item.Tag;if(HasUpdate(entry))item.Content=ItemContent(entry);}
          UpdateTabs();if(!busy&&tab==CatalogTab.Updates)FilterEntries();PortRelease current;if(!busy&&selected!=null&&available.TryGetValue(selected.Identity,out current))PresentRelease(selected,current);if(!busy)status.Text=count+" atualização(ões) disponível(is)."+(failed>0?" Não foi possível confirmar "+failed+" fonte(s); tente novamente mais tarde.":"");}
      }finally{updateChecks.Dispose();updateChecks=null;if(!closed)checkUpdates.IsEnabled=!busy;}
    }
    void PresentRelease(PortEntry entry,PortRelease found){
      release=found;available[entry.Identity]=found;version.Text=found.Version+" · "+(found.Size/1048576.0).ToString("0.0")+" MB";details.Text=entry.Notes+"\n\nPacote: "+found.AssetName+"\n"+(String.IsNullOrEmpty(found.Digest)?"Sem SHA-256 publicado nesta release.":"SHA-256 publicado será verificado antes da instalação.");
      if(found.IsPrerelease){version.Text+=" · BETA";acceptBeta.Visibility=Visibility.Visible;}
      var installed=Installed(entry);UpdateInstallState();
      if(installed!=null){version.Text+="\nInstalado: "+installed.InstalledVersion;status.Text=!UpdateManager.Available(installed,found)?"Esta versão já está instalada.":found.FromCache?"Confirme a versão atual no GitHub usando Verificar atualização.":entry.UsesInstaller?"Atualizar abre o assistente do autor. Ele controla a atualização dos arquivos; conclua-o antes de selecionar o aplicativo.":"Atualizar preserva seus arquivos e cria uma cópia completa da versão anterior. Feche o jogo e reserve espaço para essa cópia.";}
      else status.Text=entry.UsesInstaller?"O NEXUS baixará o instalador e abrirá o assistente do autor. Ao concluir, selecione o aplicativo instalado aqui.":entry.DiscoverExecutable?"O pacote será instalado e adicionado aqui. Se houver vários aplicativos, escolha o principal na janela do NEXUS.":"Pronto para baixar, extrair e adicionar à biblioteca.";
      if(found.FromCache){version.Text+=" · cache";DateTime when;string date=DateTime.TryParse(found.CheckedUtc,out when)?when.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):found.CheckedUtc;status.Text="Consulta online indisponível: versão verificada em "+date+". "+status.Text;}
    }
    void UpdateInstallState(){
      if(download==null||selected==null)return;if(selected.ManualPackage||selected.ProjectOnly){download.IsEnabled=!busy;return;}
      var installed=Installed(selected);bool newer=release!=null&&installed!=null&&UpdateManager.Available(installed,release);
      bool same=release!=null&&games.Any(g=>g.Repository==selected.Identity&&g.InstalledVersion==release.Version&&File.Exists(g.Executable));
      download.Content=installed==null?(same?"Já instalado":selected.UsesInstaller?"Baixar e instalar":"Baixar e adicionar"):newer?(release.FromCache?"Verificar atualização":"Atualizar"):release!=null&&installed.InstalledVersion==release.Version?"Já instalado":"Versão instalada mais recente";
      download.IsEnabled=!busy&&release!=null&&(!release.IsPrerelease||acceptBeta.IsChecked==true)&&(installed==null?!same:newer);
      if(installed!=null){destination.Text=installed.InstallationKind=="Installer"?Path.GetDirectoryName(installed.InstallerCacheRoot):Path.GetDirectoryName(installed.ManagedInstallationRoot);choose.IsEnabled=false;destination.IsEnabled=false;}
    }
    async Task<LaunchSelection> RunOriginalInstaller(string installer,Game previous){
      cancel.IsEnabled=false;status.Text="Conclua ou cancele no assistente do autor; depois selecione o aplicativo instalado.";
      using(var process=Process.Start(new ProcessStartInfo(installer){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(installer)})){if(process==null)throw new InvalidOperationException("Não foi possível abrir o instalador.");await Task.Run(()=>process.WaitForExit());if(process.ExitCode!=0&&process.ExitCode!=3010&&process.ExitCode!=1641)throw new IOException("O assistente foi cancelado ou terminou com erro ("+process.ExitCode+").");}
      var picker=new Microsoft.Win32.OpenFileDialog{Title="Após concluir o assistente, selecione o aplicativo principal do port",Filter="Aplicativo Windows|*.exe",CheckFileExists=true,FileName=previous!=null&&File.Exists(previous.Executable)?previous.Executable:""};if(picker.ShowDialog(this)!=true)return null;
      return new LaunchSelection{Play=picker.FileName,Arguments=previous==null?"":previous.Arguments,Configuration=previous!=null&&File.Exists(previous.ConfigurationExecutable)?previous.ConfigurationExecutable:null,ConfigurationArguments=previous==null?"":previous.ConfigurationArguments};
    }
    void SetInteractive(bool enabled){
      foreach(UIElement element in new UIElement[]{checkUpdates,importZip,acceptBeta,search,list,choose,destination,refresh,platformList,typeChips,modeChips,newsTab,updatesTab,allTab,sort})element.IsEnabled=enabled;
    }
    async void InstallSelected(string localZip=null){
      if(selected==null||busy)return;if(selected.ProjectOnly){Open(selected.ProjectUrl+"/releases");return;}if(selected.ManualPackage&&localZip==null){Open(selected.ProjectUrl);return;}if(!selected.ManualPackage&&(release==null||release.IsPrerelease&&acceptBeta.IsChecked!=true))return;
      PortEntry entry=selected;PortRelease package=release;Game previous=Installed(entry);bool updating=previous!=null&&localZip==null;if(updating&&package.FromCache){LoadSelected(true);return;}if(updating&&!UpdateManager.Available(previous,package))return;string parent=updating?(previous.InstallationKind=="Installer"?Path.GetDirectoryName(previous.InstallerCacheRoot):Path.GetDirectoryName(previous.ManagedInstallationRoot)):destination.Text.Trim();
      if(String.IsNullOrWhiteSpace(parent)||!Path.IsPathRooted(parent)){status.Text="Escolha uma pasta de instalação com caminho completo.";return;}
      if(updating){try{UpdateManager.EnsureClosed(previous);}catch(Exception ex){status.Text=ex.Message;return;}}
      busy=true;SetInteractive(false);download.IsEnabled=false;cancel.IsEnabled=true;installation=new CancellationTokenSource();
      try{
        var progressReporter=new Progress<InstallProgress>(p=>{if(!closed){status.Text=p.Message;progress.Value=p.Percent;}});
        var interaction=new InstallInteraction{Previous=previous,UpdateInPlace=updating,Choose=delegate(string root,string[] candidates,Game old){var picker=new PackageChoiceWindow(this,entry.Name,candidates,old);return picker.ShowDialog()==true?picker.Result:null;},RunInstaller=RunOriginalInstaller};
        Game game=localZip==null?await catalog.Install(entry,package,parent,progressReporter,installation.Token,acceptBeta.IsChecked==true,interaction):await catalog.ImportZip(entry,localZip,parent,progressReporter,installation.Token);
        try{added(game);}catch{if(updating&&!entry.UsesInstaller)UpdateManager.Rollback(game,previous);throw;}
        UpdatesFound.Remove(entry.Identity);
        progress.Value=100;status.Text=(updating?"Atualizado: ":"Instalado: ")+entry.Name+(String.IsNullOrEmpty(game.UpdateBackupDirectory)?". Abra Jogar para preparar seus arquivos.":". Cópia anterior preservada em "+game.UpdateBackupDirectory);download.Content="Já instalado";
      }catch(OperationCanceledException){status.Text=entry.UsesInstaller?"Cadastro não concluído. Os arquivos instalados pelo assistente são controlados por ele.":"Operação cancelada. A instalação anterior foi preservada.";}
      catch(Exception ex){status.Text="Não foi possível concluir: "+ex.Message;}
      finally{busy=false;SetInteractive(true);checkUpdates.IsEnabled=updateChecks==null;cancel.IsEnabled=false;UpdateInstallState();installation.Dispose();installation=null;}
    }

    ListBoxItem Item(string id){return list.Items.Cast<ListBoxItem>().First(i=>((PortEntry)i.Tag).Id==id);}
    public void CaptureSmoke(string path){
      SetTab(CatalogTab.All);
      foreach(string name in Platforms.All){SetPlatform(name);int expected=catalog.Entries.Count(e=>name==Platforms.All[0]||e.Platform==name);if(list.Items.Count!=expected)throw new Exception("Catalog platform filter failed");}
      foreach(string type in ProjectTypes.All){query.ProjectTypes.Clear();query.ProjectTypes.Add(type);SetPlatform(Platforms.All[0]);if(list.Items.Count!=catalog.Entries.Count(e=>e.ProjectType==type))throw new Exception("Project type filter failed: "+type);}
      query.ProjectTypes.Clear();
      search.Text="Mario Kart";SetPlatform("Nintendo 64");SetModes(InstallModes.All.Where(InstallModes.Automatic).ToArray());if(list.Items.Count!=1||((PortEntry)((ListBoxItem)list.Items[0]).Tag).Id!="rl-spaghettikart")throw new Exception("Combined catalog search failed");search.Text="";SetModes();
      SetPlatform("PlayStation 3");list.SelectedItem=list.Items.Cast<ListBoxItem>().First(i=>((PortEntry)i.Tag).ProjectOnly);LoadSelected();if(!download.IsEnabled||release!=null||destination.IsEnabled||choose.IsEnabled||refresh.IsEnabled||(string)download.Content!="Ver downloads oficiais")throw new Exception("Manual profile must expose official downloads without claiming automatic installation");
      SetPlatform("Xbox");list.SelectedItem=Item("darkwatch");selected=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;acceptBeta.IsChecked=false;var sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-stable";PresentRelease(selected,sample);
      if(acceptBeta.Visibility!=Visibility.Collapsed||!download.IsEnabled)throw new Exception("Stable Darker Edition profile must not require beta acceptance");
      SetPlatform("PlayStation 4");list.SelectedItem=Item("knack2");selected=(PortEntry)((ListBoxItem)list.SelectedItem).Tag;acceptBeta.IsChecked=false;sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-beta";PresentRelease(selected,sample);if(download.IsEnabled||acceptBeta.Visibility!=Visibility.Visible)throw new Exception("Experimental port requires explicit acceptance");acceptBeta.IsChecked=true;if(!download.IsEnabled)throw new Exception("Accepted experimental port must be installable");
      SetPlatform("Xbox One");list.SelectedIndex=0;LoadSelected();if(importZip.Visibility!=Visibility.Visible||!download.IsEnabled||acceptBeta.IsChecked==true)throw new Exception("Official-site import UI failed");
      foreach(string id in new[]{"sotc-lucas","totk","witch-time","echoes-unbound","metroid-dread","vcs-ps2","hctp","gt3-revisited","brawl-recompiled","007-eon"}){var profile=catalog.Entries.First(e=>e.Id==id);SetPlatform(profile.Platform);list.SelectedItem=Item(id);PrepareSelection((PortEntry)((ListBoxItem)list.SelectedItem).Tag);sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-"+sample.Version;sample.FromCache=false;PresentRelease(selected,sample);string expected=Installed(selected)!=null?"Atualizar":selected.UsesInstaller?"Baixar e instalar":"Baixar e adicionar";if(!download.IsEnabled||(string)download.Content!=expected)throw new Exception("New profile action failed: "+id);}
      SetPlatform("Wii U");SetModes(InstallModes.All.Where(InstallModes.Automatic).ToArray());if(list.Items.Count!=4)throw new Exception("All Wii U downloads must be integrated");foreach(var item in list.Items.Cast<ListBoxItem>()){list.SelectedItem=item;PrepareSelection((PortEntry)item.Tag);sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-"+sample.Version;sample.FromCache=false;PresentRelease(selected,sample);if(!download.IsEnabled||!choose.IsEnabled||!destination.IsEnabled||!refresh.IsEnabled||(string)download.Content!="Baixar e adicionar"&&(string)download.Content!="Atualizar")throw new Exception("Wii U download UI must be usable");}
      list.SelectedItem=Item("rl-botw-native");PrepareSelection((PortEntry)((ListBoxItem)list.SelectedItem).Tag);sample=PortCatalog.BundledRelease(selected);sample.Version="smoke-"+sample.Version;sample.FromCache=false;PresentRelease(selected,sample);Height=900;
      Launcher.CaptureWindow(this,path);}
    public void CaptureUpdateSmoke(string path){
      var entry=catalog.Entries.First(e=>e.Id=="gow3");string parent=Path.GetDirectoryName(Path.GetFullPath(path)),root=PortCatalog.ChildPath(parent,".ui-update-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string exe=PortCatalog.ChildPath(root,entry.PlayNames[0]);File.WriteAllText(exe,"UI fixture only");var previous=new Game{Name=entry.Name,Repository=entry.Identity,Platform=entry.Platform,Executable=exe,ManagedInstallationRoot=root,InstalledVersion="v1.0.0"};UpdateManager.WriteReceipt(entry,new PortRelease{Version=previous.InstalledVersion},root,CancellationToken.None);games.Add(previous);
      try{SetTab(CatalogTab.All);SetPlatform(entry.Platform);list.SelectedItem=Item(entry.Id);PrepareSelection(entry);var sample=PortCatalog.BundledRelease(entry);sample.FromCache=false;sample.Version="v2.0.0";PresentRelease(entry,sample);if(!download.IsEnabled||(string)download.Content!="Atualizar")throw new Exception("New online release must expose Update");sample.FromCache=true;PresentRelease(entry,sample);if((string)download.Content!="Verificar atualização")throw new Exception("Cached release must require online refresh");sample.FromCache=false;sample.Version="v1.0.0";PresentRelease(entry,sample);if(download.IsEnabled)throw new Exception("Same version cannot update");sample.Version="v0.9.0";PresentRelease(entry,sample);if(download.IsEnabled)throw new Exception("Older version cannot update");sample.Version="v2.0.0";PresentRelease(entry,sample);SetTab(CatalogTab.Updates);if(list.Items.Count!=1||((PortEntry)((ListBoxItem)list.Items[0]).Tag).Id!=entry.Id)throw new Exception("Updates filter must show only installed newer online releases");list.SelectedIndex=0;PrepareSelection(entry);PresentRelease(entry,sample);Height=900;Launcher.CaptureWindow(this,path);}
      finally{games.Remove(previous);if(root.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith(".ui-update-fixture-"))Directory.Delete(root,true);}
    }
  }
}
