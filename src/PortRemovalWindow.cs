using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Nexus {
  public class PortRemovalWindow:Window {
    readonly Game game;readonly List<Game> library;readonly string dataRoot;readonly bool preview;
    readonly TextBlock summary;readonly TextBox paths;readonly Button choose,erase,cancel;readonly CheckBox backups,accept;readonly ProgressBar progress;RemovalPlan plan;string folder;bool scanning,deleting,closed;
    public PortRemovalWindow(Window owner,Game selected,List<Game> games,string root,bool smoke=false){
      game=selected.Copy();library=games.Select(g=>g.Copy()).ToList();dataRoot=root;preview=smoke;Owner=owner;Title="Excluir port · NEXUS";Width=700;Height=630;MinWidth=550;MinHeight=540;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var outer=new Grid();outer.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});outer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=outer;var scroll=new ScrollViewer();outer.Children.Add(scroll);var panel=new StackPanel{Margin=new Thickness(26,26,26,0)};scroll.Content=panel;panel.Children.Add(new TextBlock{Text="Excluir "+game.Name+"?",FontSize=24,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
      panel.Children.Add(new TextBlock{Text="A exclusão é permanente e não usa a Lixeira. Todos os arquivos das pastas abaixo, inclusive dumps, saves e configurações que estiverem dentro delas, serão apagados.",Foreground=new SolidColorBrush(Color.FromRgb(255,174,174)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
      panel.Children.Add(new TextBlock{Text="Faça um backup do que deseja guardar antes de continuar. Arquivos em pastas externas, como o dump de origem e saves em Documentos/AppData, não entram nessa exclusão.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,0,0,12)});
      choose=new Button{Content="Escolher a pasta raiz do port…",HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(choose);choose.Click+=delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Selecione somente a pasta dedicada a este port, contendo o executável e todos os arquivos que deseja excluir.",SelectedPath=folder??Path.GetDirectoryName(game.Executable)})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK){folder=picker.SelectedPath;RefreshPlan();}};
      backups=new CheckBox{Content="Incluir backups de atualização e cache de instalação identificados pelo NEXUS",IsChecked=true};panel.Children.Add(backups);backups.Checked+=delegate{RefreshPlan();};backups.Unchecked+=delegate{RefreshPlan();};
      paths=new TextBox{IsReadOnly=true,Height=110,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,8,0,8)};System.Windows.Automation.AutomationProperties.SetName(paths,"Pastas que serão excluídas");panel.Children.Add(paths);summary=new TextBlock{Text="Escolha a pasta do port para conferir a exclusão.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};panel.Children.Add(summary);
      progress=new ProgressBar{Height=7,Minimum=0,Maximum=100,Margin=new Thickness(0,6,0,10)};panel.Children.Add(progress);accept=new CheckBox{Content="Conferi as pastas e confirmo a exclusão permanente dos arquivos.",IsEnabled=false};panel.Children.Add(accept);accept.Checked+=delegate{UpdateAction();};accept.Unchecked+=delegate{UpdateAction();};
      var actions=new WrapPanel{Margin=new Thickness(26,12,26,24)};Grid.SetRow(actions,1);outer.Children.Add(actions);erase=new Button{Content="Excluir port e arquivos",IsEnabled=false,Foreground=new SolidColorBrush(Color.FromRgb(255,174,174))};cancel=new Button{Content="Cancelar",IsCancel=true};actions.Children.Add(cancel);actions.Children.Add(erase);erase.Click+=async delegate{
        if(plan==null||accept.IsChecked!=true||deleting||preview)return;deleting=true;UpdateAction();choose.IsEnabled=false;backups.IsEnabled=false;accept.IsEnabled=false;cancel.IsEnabled=false;
        try{var reporter=new Progress<InstallProgress>(p=>{summary.Text=p.Message;progress.Value=p.Percent;});await Task.Run(()=>PortRemoval.Execute(plan,reporter));DialogResult=true;}
        catch(Exception ex){summary.Text=ex.Message;plan=null;accept.IsChecked=false;deleting=false;choose.IsEnabled=true;backups.IsEnabled=true;cancel.IsEnabled=true;UpdateAction();}
      };
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(deleting&&DialogResult!=true)e.Cancel=true;};Closed+=delegate{closed=true;};Loaded+=delegate{if(preview){paths.Text="Exemplo: C:\\Games\\MeuPort";summary.Text="Confira as pastas antes de excluir. Esta prévia não remove arquivos.";return;}if(game.InstallationKind!="Installer"&&UpdateManager.Managed(game)){choose.Visibility=Visibility.Collapsed;RefreshPlan();}};
    }
    void UpdateAction(){erase.IsEnabled=plan!=null&&accept.IsChecked==true&&!scanning&&!deleting;}
    async void RefreshPlan(){if(scanning||deleting||closed||preview)return;scanning=true;plan=null;accept.IsChecked=false;accept.IsEnabled=false;choose.IsEnabled=false;backups.IsEnabled=false;UpdateAction();summary.Text="Conferindo pastas e arquivos…";bool include=backups.IsChecked==true;
      try{var result=await Task.Run(()=>PortRemoval.Build(game,library,dataRoot,folder,include));if(closed)return;plan=result;paths.Text=String.Join(Environment.NewLine,result.Roots);summary.Text=result.FileCount+" arquivos · "+(result.Bytes/1048576.0).ToString("0.0")+" MB. Confira todos os destinos acima.";accept.IsEnabled=true;}
      catch(Exception ex){if(!closed){paths.Text=folder??game.ManagedInstallationRoot??"";summary.Text=ex.Message;}}
      finally{scanning=false;if(!closed){choose.IsEnabled=true;backups.IsEnabled=true;UpdateAction();}}
    }
  }
}
