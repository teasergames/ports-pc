using System;
using System.IO;
using System.Threading;
using System.Linq;
using Nexus;

public static class Integration {
  public static int Main(string[] args){
    try{
      string root=Path.GetFullPath(args[0]);var store=new Store(root);var lib=new Library();
      if(args[1].StartsWith("catalog")){
        using(var catalog=new PortCatalog()){
          foreach(var entry in catalog.Entries){
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
