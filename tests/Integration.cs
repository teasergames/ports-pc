using System;
using System.IO;
using System.Threading;
using System.Linq;
using Nexus;

public static class Integration {
  public static int Main(string[] args){
    try{
      string root=Path.GetFullPath(args[0]);var store=new Store(root);var lib=new Library();
      if(args[1]=="managed-packages"){
        foreach(var entry in PortCatalog.LoadEntries().Where(e=>new[]{"rl-snap64recomp","rl-perfect-dark"}.Contains(e.Id))){
          var release=PortCatalog.BundledRelease(entry);string package=Path.Combine(args[2],release.AssetName);using(var catalog=new PortCatalog(new PackageHandler(File.ReadAllBytes(package)))){string final=Path.Combine(root,"ports",entry.Id+"-"+PortCatalog.VersionFolder(release.Version));Game game;if(Directory.Exists(final)){PortCatalog.VerifyDigest(package,release.Digest);game=PackageSupport.Register(entry,final,release.Version,new InstallInteraction{Choose=delegate(string folder,string[] candidates,Game old){return new LaunchSelection{Play=candidates.Single(p=>Path.GetFileName(p)==(entry.Id=="rl-perfect-dark"?"pd.x86_64.exe":"Snap64Recomp.exe"))};}});}else game=catalog.Install(entry,release,Path.Combine(root,"ports"),null,CancellationToken.None,false,new InstallInteraction{Choose=delegate(string folder,string[] candidates,Game old){string play=candidates.Single(p=>Path.GetFileName(p)==(entry.Id=="rl-perfect-dark"?"pd.x86_64.exe":"Snap64Recomp.exe"));return new LaunchSelection{Play=play};}}).GetAwaiter().GetResult();if(!File.Exists(game.Executable)||!PackageSupport.WindowsExecutable(game.Executable)||!UpdateManager.Managed(game))throw new Exception("Real generic package failed validation");lib.Games.Add(game);store.Save(lib);Console.WriteLine("PASS real generic install, PE, checksum, receipt and library registration: "+entry.Id);}
        }return 0;
      }
      if(args[1]=="oct28-packages"||args[1]=="site-packages"||args[1]=="download-fix"){
        foreach(var entry in PortCatalog.LoadEntries().Where(e=>args[1]=="download-fix"?new[]{"rl-zeldawwhdrecomp","rl-botw-native","rl-nsmbu-recomp","rl-smash4pc","rl-dusklight","rl-strikers","rl-luigis-mansion-recompiled","rl-letsgo-recomp","rl-killzone-shadowfall-pc","rl-dragons-crown-pro-pc","rl-ufc-3-ps4-port-linux-windows","rl-unicorn-overlord-pc","rl-infamous-first-light-pc","rl-metroidprimeport","rl-nsmb-wii-multiplayer-recompilation","rl-recharged","rl-smm2recomp"}.Contains(e.Id):args[1]=="site-packages"?new[]{"rl-zelda64recomp","rl-shipwright","rl-spaghettikart","rl-2ship2harkinian","rl-starship","rl-banjorecomp","rl-ghostship","rl-donkey-kong-64-recompiled","rl-fable-2-recomp","rl-piratesatworldsendrecomp"}.Contains(e.Id):new[]{"knack","knack2","wipeout","code-veronica","drakengard3","lost-odyssey","blue-dragon","pgr4","conker-lr","las-recomp"}.Contains(e.Id))){
          var release=PortCatalog.BundledRelease(entry);string package=Path.Combine(args[2],release.AssetName),parent=Path.Combine(root,"ports"),destination=Path.Combine(parent,entry.Id+"-"+PortCatalog.VersionFolder(release.Version));Game game;
          if(args[1]=="download-fix"){package=Path.Combine(args[2],"wii-packages",release.AssetName);if(!File.Exists(package))package=Path.Combine(args[2],"more-packages",release.AssetName);}
          if(Directory.Exists(destination)){PortCatalog.VerifyDigest(package,release.Digest);game=PortCatalog.Register(entry,destination,release.Version);}else using(var catalog=new PortCatalog(new PackageHandler(File.ReadAllBytes(package))))game=catalog.Install(entry,release,parent,null,CancellationToken.None,entry.AllowPrerelease).GetAwaiter().GetResult();
          if(game.Platform!=entry.Platform||!File.Exists(game.Executable))throw new Exception("Package registration failed");if(entry.ConfigNames.Length>0)Ports.ConfigurationStartInfo(game);Ports.StartInfo(game,false);lib.Games.Add(game);store.Save(lib);Console.WriteLine("PASS real package integrity, installation, platform and launch/configuration: "+entry.Id);
        }return 0;
      }
      if(args[1]=="xbox-packages"){
        foreach(var entry in PortCatalog.LoadEntries().Where(e=>new[]{"black","darkwatch","forza2","forza3","forza4","forza-horizon","azimuth"}.Contains(e.Id))){
          Game game;string parent=Path.Combine(root,"ports");if(entry.ManualPackage){using(var catalog=new PortCatalog())game=catalog.ImportZip(entry,Path.Combine(args[2],"azimuth-windows.zip"),parent,null,CancellationToken.None).GetAwaiter().GetResult();}
          else {var release=PortCatalog.BundledRelease(entry);byte[] bytes=File.ReadAllBytes(Path.Combine(args[2],release.AssetName));using(var catalog=new PortCatalog(new PackageHandler(bytes)))game=catalog.Install(entry,release,parent,null,CancellationToken.None,entry.AllowPrerelease).GetAwaiter().GetResult();}
          if(game.Platform!=entry.Platform||!File.Exists(game.Executable))throw new Exception("Package registration failed");if(entry.ConfigNames.Length>0)Ports.ConfigurationStartInfo(game);Ports.StartInfo(game,false);lib.Games.Add(game);store.Save(lib);Console.WriteLine("PASS real package install, configuration and platform: "+entry.Id);
        }return 0;
      }
      if(args[1]=="platform-packages"){
        using(var catalog=new PortCatalog())foreach(var entry in catalog.Entries.Where(e=>new[]{"opence","unleashed","hwder","b2recomp","gravity-rush","gravity-rush-2"}.Contains(e.Id))){
          var release=PortCatalog.BundledRelease(entry);string source=Path.Combine(args[2],release.AssetName);PortCatalog.VerifyDigest(source,release.Digest);string destination=Path.Combine(root,entry.Id);if(!Directory.Exists(destination)){Directory.CreateDirectory(destination);PortCatalog.Extract(source,destination,null,CancellationToken.None);}var game=PortCatalog.Register(entry,destination,release.Version);if(game.Platform!=entry.Platform||!File.Exists(game.Executable))throw new Exception("Platform package registration failed");if(entry.ConfigNames.Length>0)Ports.ConfigurationStartInfo(game);else if(!String.IsNullOrEmpty(game.ConfigurationExecutable))throw new Exception("Unexpected configuration launcher");Ports.StartInfo(game,false);lib.Games.Add(game);store.Save(lib);Console.WriteLine("PASS real package extract, platform, configuration and play paths: "+entry.Id);
        }return 0;
      }
      if(args[1].StartsWith("catalog")){
        using(var catalog=new PortCatalog()){
          foreach(var entry in catalog.Entries.Where(e=>!e.ProjectOnly&&!e.ManualPackage)){
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
  class PackageHandler:System.Net.Http.HttpMessageHandler {
    readonly byte[] bytes;public PackageHandler(byte[] content){bytes=content;}
    protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token){return System.Threading.Tasks.Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){RequestMessage=request,Content=new System.Net.Http.ByteArrayContent(bytes)});}
  }

}
