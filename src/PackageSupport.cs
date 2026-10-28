using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Nexus {
  public class LaunchSelection {public string Play;public string Configuration;public string Arguments;public string ConfigurationArguments;}
  public class InstallInteraction {
    public Game Previous;
    public bool UpdateInPlace;
    public Func<string,string[],Game,LaunchSelection> Choose;
    public Func<string,Game,Task<LaunchSelection>> RunInstaller;
  }
  public static class PackageSupport {
    static readonly string[] Helpers={"python.exe","pythonw.exe","clang.exe","clang++.exe","lld.exe","lld-link.exe","zig.exe","ffmpeg.exe","ffprobe.exe","crashpad_handler.exe","7z.exe","7za.exe","hactool.exe","hactoolnet.exe","vc_redist.x64.exe","vc_redist.x86.exe","unins000.exe"};
    public static bool WindowsExecutable(string path){
      try{using(var f=File.OpenRead(PortCatalog.FsPath(path)))using(var r=new BinaryReader(f)){if(f.Length<64||r.ReadUInt16()!=0x5a4d)return false;f.Position=60;long offset=r.ReadUInt32();if(offset<64||offset>f.Length-24)return false;f.Position=offset;if(r.ReadUInt32()!=0x00004550)return false;ushort machine=r.ReadUInt16();f.Position=offset+22;ushort flags=r.ReadUInt16();return (machine==0x8664||machine==0x14c)&&(flags&0x2000)==0;}}catch{return false;}
    }
    public static string[] Candidates(string root){
      string full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
      return Directory.GetFiles(PortCatalog.FsPath(root),"*.exe",SearchOption.AllDirectories).Select(Display).Where(p=>!Helpers.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase)&&!p.Substring(full.Length).Split(Path.DirectorySeparatorChar).Any(x=>new[]{"_internal","python","venv",".venv","llvm","toolchain","redist"}.Contains(x,StringComparer.OrdinalIgnoreCase))&&WindowsExecutable(p)).Select(p=>p.Substring(full.Length)).OrderBy(p=>p.Count(c=>c==Path.DirectorySeparatorChar)).ThenBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    static string Display(string p){return p.StartsWith(@"\\?\UNC\")?@"\\"+p.Substring(8):p.StartsWith(@"\\?\")?p.Substring(4):p;}
    public static LaunchSelection Select(string root,InstallInteraction interaction){
      var candidates=Candidates(root);if(candidates.Length==0)throw new InvalidDataException("O pacote não contém aplicativo Windows x64/x86 compatível. Nenhum jogo foi cadastrado.");
      var previous=interaction==null?null:interaction.Previous;
      if(previous!=null&&!String.IsNullOrEmpty(previous.ManagedInstallationRoot)){
        string relative=Relative(previous.ManagedInstallationRoot,previous.Executable);string config=String.IsNullOrEmpty(previous.ConfigurationExecutable)?null:Relative(previous.ManagedInstallationRoot,previous.ConfigurationExecutable);
        if(!candidates.Contains(relative,StringComparer.OrdinalIgnoreCase)){var matches=candidates.Where(p=>Path.GetFileName(p).Equals(Path.GetFileName(relative),StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length==1)relative=matches[0];}if(config!=null&&!candidates.Contains(config,StringComparer.OrdinalIgnoreCase)){var matches=candidates.Where(p=>Path.GetFileName(p).Equals(Path.GetFileName(config),StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length==1)config=matches[0];}
        if(candidates.Contains(relative,StringComparer.OrdinalIgnoreCase)&&(config==null||candidates.Contains(config,StringComparer.OrdinalIgnoreCase)))return new LaunchSelection{Play=relative,Configuration=config,Arguments=previous.Arguments,ConfigurationArguments=previous.ConfigurationArguments};
      }
      LaunchSelection choice=candidates.Length==1?new LaunchSelection{Play=candidates[0]}:interaction!=null&&interaction.Choose!=null?interaction.Choose(root,candidates,previous):null;
      if(choice==null)throw new OperationCanceledException("Escolha do aplicativo cancelada.");
      if(!candidates.Contains(choice.Play,StringComparer.OrdinalIgnoreCase)||!String.IsNullOrEmpty(choice.Configuration)&&!candidates.Contains(choice.Configuration,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Escolha um aplicativo da lista do pacote.");return choice;
    }
    public static string Relative(string root,string path){string full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string file=Path.GetFullPath(path);if(!file.StartsWith(full,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Aplicativo fora da instalação gerenciada.");return file.Substring(full.Length);}
    public static Game Register(PortEntry entry,string root,string version,InstallInteraction interaction){var choice=Select(root,interaction);return new Game{Name=entry.Name,Platform=entry.Platform,Executable=PortCatalog.ChildPath(root,choice.Play),Arguments=choice.Arguments??"",ConfigurationExecutable=String.IsNullOrEmpty(choice.Configuration)?null:PortCatalog.ChildPath(root,choice.Configuration),ConfigurationArguments=choice.ConfigurationArguments??entry.ConfigurationArguments,WorkingDirectory=Path.GetDirectoryName(PortCatalog.ChildPath(root,choice.Play)),ArtworkSearchTitle=entry.ArtworkTitle,Repository=entry.Identity,InstalledVersion=version,ManagedInstallationRoot=root};}
    public static void PreserveMetadata(Game next,Game previous){next.Id=previous.Id;next.Name=previous.Name;next.Favorite=previous.Favorite;next.LastPlayed=previous.LastPlayed;next.Artwork=previous.Artwork;next.ArtworkSource=previous.ArtworkSource;next.ArtworkSearchTitle=previous.ArtworkSearchTitle;next.SourceGameDirectory=previous.SourceGameDirectory;next.GameContentSource=previous.GameContentSource;next.GameContentIsIso=previous.GameContentIsIso;if(!String.IsNullOrEmpty(previous.Arguments))next.Arguments=previous.Arguments;if(!String.IsNullOrEmpty(previous.ConfigurationArguments)&&next.ConfigurationExecutable!=null)next.ConfigurationArguments=previous.ConfigurationArguments;if(String.Equals(next.Executable,previous.Executable,StringComparison.OrdinalIgnoreCase)){next.Arguments=previous.Arguments;if(!String.IsNullOrEmpty(previous.WorkingDirectory)&&Directory.Exists(previous.WorkingDirectory))next.WorkingDirectory=previous.WorkingDirectory;}if(String.Equals(next.ConfigurationExecutable,previous.ConfigurationExecutable,StringComparison.OrdinalIgnoreCase))next.ConfigurationArguments=previous.ConfigurationArguments;}
  }
}
