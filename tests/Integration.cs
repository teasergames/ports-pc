using System;
using System.IO;
using System.Threading;
using System.Linq;
using Nexus;

public static class Integration {
  public static int Main(string[] args){
    try{
      string root=Path.GetFullPath(args[0]);var store=new Store(root);var lib=new Library();
      if(args[1]=="platform-packages"){
        using(var catalog=new PortCatalog())foreach(var entry in catalog.Entries.Where(e=>new[]{"opence","unleashed","hwder","b2recomp","gravity-rush","gravity-rush-2"}.Contains(e.Id))){
          var release=PortCatalog.BundledRelease(entry);string source=Path.Combine(args[2],release.AssetName);PortCatalog.VerifyDigest(source,release.Digest);string destination=Path.Combine(root,entry.Id);if(!Directory.Exists(destination)){Directory.CreateDirectory(destination);PortCatalog.Extract(source,destination,null,CancellationToken.None);}var game=PortCatalog.Register(entry,destination,release.Version);if(game.Platform!=entry.Platform||!File.Exists(game.Executable))throw new Exception("Platform package registration failed");if(entry.ConfigNames.Length>0)Ports.ConfigurationStartInfo(game);else if(!String.IsNullOrEmpty(game.ConfigurationExecutable))throw new Exception("Unexpected configuration launcher");Ports.StartInfo(game,false);lib.Games.Add(game);store.Save(lib);Console.WriteLine("PASS real package extract, platform, configuration and play paths: "+entry.Id);
        }return 0;
      }
      if(args[1].StartsWith("catalog")){
        using(var catalog=new PortCatalog()){
          foreach(var entry in catalog.Entries.Where(e=>!e.ProjectOnly)){
            var latest=catalog.Latest(entry,CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("PASS release profile"+(latest.FromCache?" (audited cache)":" (live)")+": "+entry.Repository+" "+latest.Version+" "+latest.AssetName);
            if(args[1]=="catalog-install"&&entry.Id=="last-guardian"||args[1]=="catalog-packages"&&new[]{"driveclub","infamous","last-guardian","ctr","medievil","gt-sport","the-order"}.Contains(entry.Id)){
              string parent=Path.Combine(root,"ports"),existing=Path.Combine(parent,entry.Id+"-"+PortCatalog.VersionFolder(latest.Version));
              var installed=Directory.Exists(existing)?PortCatalog.Register(entry,existing,latest.Version):catalog.Install(entry,latest,parent,null,CancellationToken.None).GetAwaiter().GetResult();if(!File.Exists(installed.Executable)||!File.Exists(installed.ConfigurationExecutable))throw new Exception("Real package installation failed");lib.Games.Add(installed);store.Save(lib);Console.WriteLine("PASS real package validation and library registration: "+entry.Name+" (no execution)");
            }
          }
        }
        return 0;
      }
      using(var service=new ArtworkService(store)){
        if(args[1]=="local"){
          foreach(Game game in Ports.Detect(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"))){
            Ports.StartInfo(game);
            var configuration=Ports.ConfigurationStartInfo(game);
            if(!File.Exists(configuration.FileName))throw new Exception("Missing configuration launcher: "+game.Name);
            var art=service.Resolve(game,false,"",CancellationToken.None).GetAwaiter().GetResult();
            if(String.IsNullOrEmpty(art.File)||!File.Exists(art.File))throw new Exception("No local artwork: "+game.Name);
            game.Artwork=art.File;game.ArtworkSource=art.Source;lib.Games.Add(game);Console.WriteLine("PASS local artwork + launch configuration: "+game.Name);
          }
          if(lib.Games.Count==0)throw new Exception("No known local ports found.");store.Save(lib);
        }else{
          Game game=new Game{Name="Hollow Knight",Executable=Path.Combine(root,"placeholder.exe"),WorkingDirectory=root};
          var art=service.Resolve(game,true,"",CancellationToken.None).GetAwaiter().GetResult();
          if(String.IsNullOrEmpty(art.File)||!File.Exists(art.File)||!art.Source.StartsWith("Wikipedia"))throw new Exception("Online artwork failed: "+art.Message);
          Console.WriteLine("PASS online Wikipedia artwork download, validation and cache");
        }
      }
      return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  }
}
