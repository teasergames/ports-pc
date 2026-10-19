using System;
using System.IO;
using System.Threading;
using Nexus;

public static class Integration {
  public static int Main(string[] args){
    try{
      string root=Path.GetFullPath(args[0]);var store=new Store(root);var lib=new Library();
      using(var service=new ArtworkService(store)){
        if(args[1]=="local"){
          foreach(Game game in Ports.Detect(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"))){
            Ports.StartInfo(game);
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
