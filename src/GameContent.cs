using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Nexus {
  public static class GameContent {
    public static bool Supports(Game game){if(game==null)return false;string file=Path.GetFileName(game.Executable);return file.Equals("BLACK.exe",StringComparison.OrdinalIgnoreCase)&&(String.IsNullOrEmpty(game.Repository)||game.Repository=="TGP482/BLACK-XBERecomp")||file.Equals("Darkwatch.exe",StringComparison.OrdinalIgnoreCase)&&(String.IsNullOrEmpty(game.Repository)||game.Repository=="Clergyman0013/arizona-vamp-recomp");}
    public static string Assets(Game game){return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(game.Executable)),"assets");}
    public static bool HasData(string folder){return Directory.Exists(folder)&&(File.Exists(Path.Combine(folder,"default.xbe"))||Directory.GetFiles(folder,"*.iso").Length>0);}
    public static bool NeedsSetup(Game game){
      if(!Supports(game))return false;string dir=Path.GetDirectoryName(Path.GetFullPath(game.Executable));for(int i=0;i<6;i++){string assets=Path.Combine(dir,"assets");if(Directory.Exists(assets))return !HasData(assets);string parent=Path.GetDirectoryName(dir);if(String.IsNullOrEmpty(parent)||parent==dir)break;dir=parent;}return true;
    }
    static void NoLink(string path){if((File.GetAttributes(PortCatalog.FsPath(path))&FileAttributes.ReparsePoint)!=0)throw new IOException("A seleção contém um link de pasta/arquivo. Escolha os arquivos reais do jogo.");}
    static List<string> Files(string root,CancellationToken token){var result=new List<string>();var queue=new Stack<string>();queue.Push(root);while(queue.Count>0){token.ThrowIfCancellationRequested();string folder=queue.Pop();NoLink(folder);foreach(string file in Directory.GetFiles(PortCatalog.FsPath(folder))){NoLink(file);result.Add(file);}foreach(string child in Directory.GetDirectories(PortCatalog.FsPath(folder)))queue.Push(child);}return result;}
    public static Task<string> Prepare(Game game,string source,bool iso,IProgress<InstallProgress> progress,CancellationToken token){return Task.Run(()=>PrepareCore(game,source,iso,progress,token),token);}
    static string PrepareCore(Game game,string source,bool iso,IProgress<InstallProgress> progress,CancellationToken token){
      if(!Supports(game))throw new InvalidDataException("Este port não usa a preparação integrada.");token.ThrowIfCancellationRequested();source=Path.GetFullPath(source);string target=Assets(game),root=Path.GetDirectoryName(target);NoLink(root);
      if(iso){if(!source.EndsWith(".iso",StringComparison.OrdinalIgnoreCase)||!File.Exists(source)||new FileInfo(source).Length==0)throw new InvalidDataException("Selecione uma ISO Xbox existente.");NoLink(source);}
      else {if(!Directory.Exists(source)||!File.Exists(Path.Combine(source,"default.xbe")))throw new InvalidDataException("Escolha a pasta extraída que contém default.xbe e os demais arquivos do Xbox.");NoLink(source);if(source.TrimEnd('\\').Equals(target.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return target;}
      string sourcePrefix=source.TrimEnd('\\')+"\\";if(!iso&&(target+"\\").StartsWith(sourcePrefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A pasta escolhida contém a instalação do port. Escolha somente os arquivos extraídos do jogo.");
      if(source.StartsWith(target.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Os arquivos escolhidos já estão dentro de assets. Escolha a pasta assets completa ou uma ISO fora dela.");
      var files=iso?new List<string>{source}:Files(source,token);long total=files.Sum(f=>new FileInfo(PortCatalog.FsPath(f)).Length);if(total==0)throw new InvalidDataException("A seleção está vazia.");
      string staging=PortCatalog.ChildPath(root,".nexus-assets-"+Guid.NewGuid().ToString("N")),backup=null;Directory.CreateDirectory(PortCatalog.FsPath(staging));long done=0;DateTime lastReport=DateTime.MinValue;
      try{
        foreach(string file in files){token.ThrowIfCancellationRequested();string relative=iso?Path.GetFileName(file):file.Substring(PortCatalog.FsPath(source).Length).TrimStart('\\');string destination=PortCatalog.ChildPath(staging,relative);Directory.CreateDirectory(PortCatalog.FsPath(Path.GetDirectoryName(destination)));
          using(var input=new FileStream(PortCatalog.FsPath(file),FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new FileStream(PortCatalog.FsPath(destination),FileMode.CreateNew,FileAccess.Write,FileShare.None)){
            byte[] buffer=new byte[1024*1024];int read;while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,read);done+=read;if(progress!=null&&(DateTime.UtcNow-lastReport).TotalMilliseconds>=150){lastReport=DateTime.UtcNow;progress.Report(new InstallProgress{Message="Copiando "+(done/1048576.0).ToString("0.0")+" / "+(total/1048576.0).ToString("0.0")+" MB",Percent=done*95.0/total});}}
          }
        }
        token.ThrowIfCancellationRequested();if(Directory.Exists(target)){NoLink(target);backup=PortCatalog.ChildPath(root,"assets.nexus-backup-"+Guid.NewGuid().ToString("N"));Directory.Move(PortCatalog.FsPath(target),PortCatalog.FsPath(backup));}
        try{Directory.Move(PortCatalog.FsPath(staging),PortCatalog.FsPath(target));}catch{if(backup!=null&&!Directory.Exists(target))Directory.Move(PortCatalog.FsPath(backup),PortCatalog.FsPath(target));throw;}
        if(progress!=null)progress.Report(new InstallProgress{Message=backup==null?"Arquivos preparados.":"Arquivos preparados. Backup preservado em "+backup,Percent=100});return target;
      }finally{string safe=PortCatalog.ChildPath(root,Path.GetFileName(staging));if(safe==staging&&Path.GetFileName(safe).StartsWith(".nexus-assets-")&&Directory.Exists(PortCatalog.FsPath(safe)))Directory.Delete(PortCatalog.FsPath(safe),true);}
    }
  }
}
