using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace Nexus {
  public class SelfUpdateWindow:Window {
    public string PlanFile;readonly CancellationTokenSource cancel=new CancellationTokenSource();bool finished;
    public SelfUpdateWindow(Window owner,SelfUpdater updater,PortRelease release,string dataRoot,bool preview=false){
      Owner=owner;Title="Atualizando NEXUS";Width=550;Height=290;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var panel=new StackPanel{Margin=new Thickness(26)};Content=panel;panel.Children.Add(new TextBlock{Text="Atualizando para "+release.Version,FontSize=23,Margin=new Thickness(0,0,0,14)});var text=new TextBlock{Text="Preparando download…",TextWrapping=TextWrapping.Wrap};panel.Children.Add(text);var bar=new ProgressBar{Minimum=0,Maximum=100,Height=8,Margin=new Thickness(0,16,0,14)};panel.Children.Add(bar);panel.Children.Add(new TextBlock{Text="Ao concluir, o NEXUS será reiniciado automaticamente. Sua biblioteca e os ports serão preservados.",TextWrapping=TextWrapping.Wrap,FontSize=12});var stop=new Button{Content="Cancelar atualização",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,18,0,0)};panel.Children.Add(stop);stop.Click+=delegate{if(finished){DialogResult=false;return;}cancel.Cancel();stop.IsEnabled=false;};Closed+=delegate{cancel.Dispose();};Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!finished){cancel.Cancel();e.Cancel=true;}};
      Loaded+=async delegate{if(preview){finished=true;text.Text="Baixando NEXUS "+release.Version+"…";bar.Value=58;return;}try{PlanFile=await updater.Download(release,dataRoot,new Progress<InstallProgress>(p=>{text.Text=p.Message;bar.Value=p.Percent;}),cancel.Token);cancel.Token.ThrowIfCancellationRequested();finished=true;DialogResult=true;}catch(OperationCanceledException){SelfUpdater.DiscardPrepared(PlanFile);finished=true;DialogResult=false;}catch(Exception ex){finished=true;text.Text="Não foi possível preparar: "+ex.Message;bar.Value=0;stop.Content="Fechar";stop.IsEnabled=true;}};
    }
  }
}
