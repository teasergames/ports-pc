using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace Nexus {
  public static class GameContentTests {
    static int checks;static void Check(bool value,string label){if(!value)throw new Exception("Content: "+label);checks++;}static void Reject(Action action,string label){try{action();}catch{checks++;return;}throw new Exception("Content: expected rejection "+label);}
    public static int Run(string root){
      checks=0;string port=Path.Combine(root,"black port"),source=Path.Combine(root,"meu jogo ç");Directory.CreateDirectory(port);Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(port,"BLACK.exe"),"");File.WriteAllText(Path.Combine(source,"default.xbe"),"fixture");File.WriteAllText(Path.Combine(source,"GlobData.bin"),"fixture");Directory.CreateDirectory(Path.Combine(source,"levels"));File.WriteAllText(Path.Combine(source,"levels","level.bin"),"fixture");
      var game=new Game{Name="BLACK",Executable=Path.Combine(port,"BLACK.exe"),Arguments="--custom",Repository="TGP482/BLACK-XBERecomp"};Check(GameContent.Supports(game)&&GameContent.NeedsSetup(game),"supported missing-data port");
      Check(!GameContent.Supports(new Game{Name="Black",Executable="BLACK.exe",Repository="other/fork"}),"other runtime fork not changed");
      Directory.CreateDirectory(GameContent.Assets(game));File.WriteAllText(Path.Combine(GameContent.Assets(game),"old-file.txt"),"preserved");
      GameContent.Prepare(game,source,false,null,CancellationToken.None).GetAwaiter().GetResult();Check(!GameContent.NeedsSetup(game)&&File.ReadAllText(Path.Combine(GameContent.Assets(game),"levels","level.bin"))=="fixture","folder prepared at runtime assets path");Check(File.Exists(Path.Combine(source,"default.xbe")),"source folder preserved");Check(Directory.GetDirectories(port,"assets.nexus-backup-*").Any(d=>File.Exists(Path.Combine(d,"old-file.txt"))),"existing assets retained as backup");Check(Ports.StartInfo(game).Arguments=="--custom","custom launch arguments unchanged");
      int backups=Directory.GetDirectories(port,"assets.nexus-backup-*").Length;GameContent.Prepare(game,GameContent.Assets(game),false,null,CancellationToken.None).GetAwaiter().GetResult();Check(Directory.GetDirectories(port,"assets.nexus-backup-*").Length==backups,"already prepared folder is reused");
      string iso=Path.Combine(root,"jogo ç.ISO");File.WriteAllText(iso,"fixture disc, not a real game");GameContent.Prepare(game,iso,true,null,CancellationToken.None).GetAwaiter().GetResult();Check(!GameContent.NeedsSetup(game)&&File.Exists(Path.Combine(GameContent.Assets(game),Path.GetFileName(iso))),"ISO copied to assets");Check(File.Exists(iso),"source ISO preserved");
      Reject(()=>GameContent.Prepare(game,port,false,null,CancellationToken.None).GetAwaiter().GetResult(),"incomplete folder rejected");Reject(()=>GameContent.Prepare(game,Path.Combine(root,"missing.iso"),true,null,CancellationToken.None).GetAwaiter().GetResult(),"missing ISO rejected");
      string big=Path.Combine(root,"cancel.iso");File.WriteAllBytes(big,new byte[4*1024*1024]);var cancellation=new CancellationTokenSource();Reject(()=>GameContent.Prepare(game,big,true,new CancelProgress(cancellation),cancellation.Token).GetAwaiter().GetResult(),"cancel during copy");Check(File.Exists(Path.Combine(GameContent.Assets(game),Path.GetFileName(iso)))&&!Directory.GetDirectories(port,".nexus-assets-*").Any(),"cancel retains previous data and removes only staging");
      var library=new Library();game.GameContentSource=iso;game.GameContentIsIso=true;library.Games.Add(game);var store=new Store(Path.Combine(root,"content-library"));store.Save(library);Check(store.Load().Games[0].GameContentSource==iso&&store.Load().Games[0].GameContentIsIso,"selection persists across restart");
      string dark=Path.Combine(root,"darkwatch");Directory.CreateDirectory(dark);File.WriteAllText(Path.Combine(dark,"Darkwatch.exe"),"");var dw=new Game{Name="Darkwatch",Executable=Path.Combine(dark,"Darkwatch.exe")};Check(GameContent.Supports(dw)&&GameContent.NeedsSetup(dw),"Darkwatch integrated setup");GameContent.Prepare(dw,iso,true,null,CancellationToken.None).GetAwaiter().GetResult();Check(!GameContent.NeedsSetup(dw),"Darkwatch prepared ISO");
      return checks;
    }
    class CancelProgress:IProgress<InstallProgress>{readonly CancellationTokenSource source;public CancelProgress(CancellationTokenSource value){source=value;}public void Report(InstallProgress value){source.Cancel();}}
  }
}
