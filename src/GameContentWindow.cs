using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Nexus {
  public class GameContentWindow:Window {
    readonly Game game;readonly TextBox path;readonly TextBlock status;readonly Button folder,iso,prepare,close;readonly ProgressBar progress;bool isIso,busy;CancellationTokenSource cancellation;public string PreparedPath;
    public GameContentWindow(Window owner,Game selected){
      game=selected;Owner=owner;Title="Preparar arquivos do jogo · NEXUS";Width=710;Height=540;MinHeight=480;MinWidth=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var scroll=new ScrollViewer();Content=scroll;var panel=new StackPanel{Margin=new Thickness(26)};scroll.Content=panel;
      panel.Children.Add(new TextBlock{Text="Preparar "+game.Name,FontSize=25,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
      panel.Children.Add(new TextBlock{Text="Escolha sua ISO do Xbox original ou a pasta extraída com default.xbe. O NEXUS copia os arquivos para o local esperado pelo port. A região e a versão do jogo serão verificadas pelo próprio port.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
      var choices=new WrapPanel();panel.Children.Add(choices);folder=new Button{Content="Escolher pasta do jogo"};iso=new Button{Content="Escolher ISO…"};choices.Children.Add(folder);choices.Children.Add(iso);
      path=new TextBox{Text=game.GameContentSource??"",IsReadOnly=true,Margin=new Thickness(0,12,0,14)};System.Windows.Automation.AutomationProperties.SetName(path,"Pasta ou ISO do jogo");panel.Children.Add(path);
      panel.Children.Add(new TextBlock{Text="Destino: "+GameContent.Assets(game)+"\n\nA cópia pode ocupar vários GB. Os arquivos de origem são preservados. Se assets já existir, ela será guardada como assets.nexus-backup-… ao lado do port.",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(167,178,194)),FontSize=12,Margin=new Thickness(0,0,0,18)});
      isIso=game.GameContentIsIso;
      folder.Click+=delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Escolha a pasta com default.xbe e os arquivos do jogo"})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK){path.Text=picker.SelectedPath;isIso=false;}};
      iso.Click+=delegate{var picker=new Microsoft.Win32.OpenFileDialog{Title="Escolher sua ISO do Xbox",Filter="Imagem de disco Xbox|*.iso",CheckFileExists=true};if(picker.ShowDialog(this)==true){path.Text=picker.FileName;isIso=true;}};
      var actions=new WrapPanel();panel.Children.Add(actions);prepare=new Button{Content="Preparar arquivos",Background=new SolidColorBrush(Color.FromRgb(180,241,118)),Foreground=Brushes.Black};close=new Button{Content="Fechar"};actions.Children.Add(prepare);actions.Children.Add(close);close.Click+=delegate{if(busy)cancellation.Cancel();else Close();};prepare.Click+=delegate{Prepare();};
      progress=new ProgressBar{Height=6,Minimum=0,Maximum=100,Margin=new Thickness(0,18,0,0)};panel.Children.Add(progress);status=new TextBlock{Text=GameContent.NeedsSetup(game)?"Selecione os arquivos para começar.":"O port já tem arquivos em assets. Você pode trocar a seleção aqui.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0)};panel.Children.Add(status);
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;cancellation.Cancel();status.Text="Cancelando a cópia e limpando os arquivos temporários…";}};
    }
    async void Prepare(){
      if(busy)return;if(String.IsNullOrWhiteSpace(path.Text)){status.Text="Escolha uma pasta ou ISO antes de preparar.";return;}busy=true;folder.IsEnabled=false;iso.IsEnabled=false;prepare.IsEnabled=false;close.Content="Cancelar cópia";cancellation=new CancellationTokenSource();
      try{PreparedPath=await GameContent.Prepare(game,path.Text,isIso,new Progress<InstallProgress>(p=>{progress.Value=p.Percent;status.Text=p.Message;}),cancellation.Token);game.GameContentSource=path.Text;game.GameContentIsIso=isIso;prepare.Content="Arquivos preparados";}
      catch(OperationCanceledException){status.Text="Cópia cancelada. Seus arquivos anteriores foram preservados.";}
      catch(Exception ex){status.Text="Não foi possível preparar: "+ex.Message;}
      finally{busy=false;folder.IsEnabled=true;iso.IsEnabled=true;prepare.IsEnabled=true;close.Content="Concluir";cancellation.Dispose();cancellation=null;}
    }
  }
}
