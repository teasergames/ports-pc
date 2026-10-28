using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Nexus {
  public class PackageChoiceWindow:Window {
    public LaunchSelection Result;
    public PackageChoiceWindow(Window owner,string title,string[] candidates,Game previous){
      Owner=owner;Title="Aplicativo do port · NEXUS";Width=650;Height=390;MinWidth=540;MinHeight=350;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;FontFamily=owner.FontFamily;Resources=owner.Resources;
      var p=new StackPanel{Margin=new Thickness(24)};Content=p;p.Children.Add(new TextBlock{Text=title,FontSize=23,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});p.Children.Add(new TextBlock{Text="Este pacote tem mais de um aplicativo. Escolha o que abre o port e, se existir, o launcher de configuração.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
      p.Children.Add(new TextBlock{Text="Jogar"});var play=new ComboBox{ItemsSource=candidates,SelectedIndex=0,Foreground=Brushes.Black,Padding=new Thickness(8),Margin=new Thickness(0,6,0,14)};System.Windows.Automation.AutomationProperties.SetName(play,"Aplicativo para jogar");p.Children.Add(play);
      p.Children.Add(new TextBlock{Text="Configurar jogo (opcional)"});var config=new ComboBox{ItemsSource=new[]{"Sem aplicativo separado"}.Concat(candidates).ToArray(),SelectedIndex=0,Foreground=Brushes.Black,Padding=new Thickness(8),Margin=new Thickness(0,6,0,18)};System.Windows.Automation.AutomationProperties.SetName(config,"Aplicativo de configuração");p.Children.Add(config);
      var buttons=new StackPanel{Orientation=Orientation.Horizontal};p.Children.Add(buttons);var ok=new Button{Content="Adicionar ao NEXUS",IsDefault=true};ok.Click+=delegate{Result=new LaunchSelection{Play=(string)play.SelectedItem,Configuration=config.SelectedIndex==0?null:(string)config.SelectedItem,Arguments=previous==null?"":previous.Arguments,ConfigurationArguments=previous==null?"":previous.ConfigurationArguments};DialogResult=true;};buttons.Children.Add(ok);var cancel=new Button{Content="Cancelar",IsCancel=true};buttons.Children.Add(cancel);
    }
  }
}
