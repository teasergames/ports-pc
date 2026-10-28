using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Nexus {
  public static class CatalogTests {
    static int checks;static void Check(bool value,string name){if(!value)throw new Exception("Catalog: "+name);checks++;}
    static void Reject(Action action,string name){try{action();}catch{checks++;return;}throw new Exception("Catalog: expected rejection "+name);}
    public static int Run(string root){
      checks=0;var entries=PortCatalog.LoadEntries();Check(entries.Count==42,"42 catalog profiles");Check(entries.Select(e=>e.Identity).Distinct().Count()==42,"distinct sources including Shadow forks");
      var entry=entries.First(e=>e.Id=="gow3");string url="https://github.com/"+entry.Repository+"/releases/download/test/gow3-windows.zip";
      var payload=new {tag_name="test",html_url="https://github.com/"+entry.Repository+"/releases/tag/test",draft=false,prerelease=false,assets=new[]{new{name="gow3-windows.zip",size=100,browser_download_url=url,digest=(string)null},new{name="source.zip",size=100,browser_download_url=url,digest=(string)null},new{name="linux.AppImage",size=100,browser_download_url=url,digest=(string)null}}};
      string json=new JavaScriptSerializer().Serialize(payload);Check(PortCatalog.ParseRelease(entry,json).AssetName=="gow3-windows.zip","select Windows asset without source/linux files");
      Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("\"prerelease\":false","\"prerelease\":true")),"ignore prerelease");Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("gow3-windows.zip","different.zip")),"fail closed when asset changes");
      Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("source.zip","gow3-windows.zip")),"reject ambiguous packages");
      Reject(()=>PortCatalog.ValidateDownloadUrl(entry,"https://github.com/other/project/releases/download/v1/gow3-windows.zip"),"reject different repository");Reject(()=>PortCatalog.ValidateDownloadUrl(entry,"http://github.com/"+entry.Repository+"/releases/download/v1/a.zip"),"require HTTPS");
      Reject(()=>PortCatalog.ChildPath(root,@"..\escape.exe"),"path traversal");Reject(()=>PortCatalog.ChildPath(root,Path.GetPathRoot(root)+"outside.exe"),"absolute path");Check(PortCatalog.ChildPath(root,"inside")!=root,"subdirectory accepted");
      string bad=Path.Combine(root,"zip-slip.zip"),dest=Path.Combine(root,"zip-slip-target");Directory.CreateDirectory(dest);using(var zip=ZipFile.Open(bad,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("../escaped.txt").Open()))writer.Write("no");Reject(()=>PortCatalog.Extract(bad,dest,null,CancellationToken.None),"ZIP traversal rejected");Check(!File.Exists(Path.Combine(root,"escaped.txt")),"no file escaped extraction");
      foreach(var e in entries.Where(e=>!e.ProjectOnly)){string folder=Path.Combine(root,"fixture-"+e.Id);string nested=Path.Combine(folder,"package","nested");Directory.CreateDirectory(nested);foreach(string name in e.PlayNames.Concat(e.ConfigNames).Distinct())File.WriteAllText(Path.Combine(nested,name),"fixture");var g=PortCatalog.Register(e,folder,"test");Check(g.Name==e.Name&&g.Repository==e.Identity&&g.InstalledVersion=="test"&&g.ConfigurationArguments==e.ConfigurationArguments&&File.Exists(g.Executable)&&(e.ConfigNames.Length==0?String.IsNullOrEmpty(g.ConfigurationExecutable):File.Exists(g.ConfigurationExecutable)),"register nested package "+e.Id);}
      string empty=Path.Combine(root,"empty-port");Directory.CreateDirectory(empty);Reject(()=>PortCatalog.Register(entry,empty,"test"),"reject missing expected executables");
      string package=Path.Combine(root,"valid-package.zip");using(var zip=ZipFile.Open(package,ZipArchiveMode.Create)){foreach(string name in entry.PlayNames.Concat(entry.ConfigNames).Distinct())using(var writer=new StreamWriter(zip.CreateEntry("nested/"+name).Open()))writer.Write("fixture");}
      string digest;using(var sha=SHA256.Create())digest="sha256:"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(package))).Replace("-","").ToLowerInvariant();PortCatalog.VerifyDigest(package,digest);checks++;
      Reject(()=>PortCatalog.VerifyDigest(package,"sha256:"+new string('0',64)),"checksum mismatch");Reject(()=>PortCatalog.VerifyDigest(package,"invalid"),"invalid checksum");
      foreach(var e in entries.Where(e=>!e.ProjectOnly&&!e.ManualPackage)){var cache=PortCatalog.BundledRelease(e);Check(cache!=null&&cache.FromCache&&!String.IsNullOrEmpty(cache.CheckedUtc),"audited fallback is dated "+e.Id);}
      var limited=new RateLimitHandler();using(var catalog=new PortCatalog(limited)){Check(catalog.Latest(entry,CancellationToken.None).GetAwaiter().GetResult().FromCache,"rate limit uses explicitly marked cache");catalog.Latest(entry,CancellationToken.None).GetAwaiter().GetResult();Check(limited.Count==1,"repeat selection reuses cache");catalog.Latest(entry,CancellationToken.None,true).GetAwaiter().GetResult();Check(limited.Count==2,"explicit refresh queries again");}
      string longZip=Path.Combine(root,"long-path.zip"),longName=String.Join("/",Enumerable.Repeat("long-dependency-folder",14))+"/file.txt";using(var zip=ZipFile.Open(longZip,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry(longName).Open()))writer.Write("long path");string longTarget=Path.Combine(root,"long-path-extract");Directory.CreateDirectory(longTarget);PortCatalog.Extract(longZip,longTarget,null,CancellationToken.None);Check(File.Exists(PortCatalog.FsPath(Path.Combine(longTarget,longName.Replace('/',Path.DirectorySeparatorChar)))),"long dependency paths without registry changes");
      var release=new PortRelease{Version="test",DownloadUrl=url,AssetName="gow3-windows.zip",Size=new FileInfo(package).Length,Digest=digest};string installRoot=Path.Combine(root,"install");
      using(var catalog=new PortCatalog(new FixtureHandler(File.ReadAllBytes(package)))){
        var game=catalog.Install(entry,release,installRoot,null,CancellationToken.None).GetAwaiter().GetResult();Check(File.Exists(game.Executable)&&game.ManagedInstallationRoot==Path.Combine(installRoot,"gow3-test"),"download extract and register atomically");Check(File.Exists(Path.Combine(game.ManagedInstallationRoot,".nexus-install.json")),"installation receipt");
        Reject(()=>catalog.Install(entry,release,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"never overwrite existing installation");
        release.Version="bad-hash";release.Digest="sha256:"+new string('0',64);Reject(()=>catalog.Install(entry,release,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"failed integrity rolls back");Check(!Directory.Exists(Path.Combine(installRoot,"gow3-bad-hash"))&&!Directory.GetFileSystemEntries(installRoot,".nexus-install-*").Any(),"failure cleans staging without touching installed game");
        release.Version="cancelled";release.Digest=digest;var cancelled=new CancellationTokenSource();cancelled.Cancel();Reject(()=>catalog.Install(entry,release,installRoot,null,cancelled.Token).GetAwaiter().GetResult(),"cancelled install");Check(!Directory.Exists(Path.Combine(installRoot,"gow3-cancelled"))&&!Directory.GetFileSystemEntries(installRoot,".nexus-install-*").Any(),"cancel cleans temporary files");
        release.Version="new-version";var newer=catalog.Install(entry,release,installRoot,null,CancellationToken.None).GetAwaiter().GetResult();Check(newer.ManagedInstallationRoot!=game.ManagedInstallationRoot&&File.Exists(game.Executable),"versions install side by side");
      }
      var noConfig=entries.First(e=>e.Id=="opence");string optionalZip=Path.Combine(root,"optional-config.zip");using(var zip=ZipFile.Open(optionalZip,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("halo.exe").Open()))writer.Write("fixture");
      using(var catalog=new PortCatalog(new FixtureHandler(File.ReadAllBytes(optionalZip)))){
        var optionalRelease=new PortRelease{Version="optional",DownloadUrl="https://github.com/"+noConfig.Repository+"/releases/download/optional/halo-windows-release.zip",AssetName="halo-windows-release.zip",Size=new FileInfo(optionalZip).Length};
        var installed=catalog.Install(noConfig,optionalRelease,installRoot,null,CancellationToken.None).GetAwaiter().GetResult();Check(File.Exists(installed.Executable)&&String.IsNullOrEmpty(installed.ConfigurationExecutable)&&installed.Platform=="Xbox","complete install with no separate configuration executable");
      }
      var darker=entries.First(e=>e.Id=="darkwatch");Check(darker.Repository=="linkssy2/DarkwatchDarkerEdition"&&!darker.AllowPrerelease&&darker.PlayNames.Single()=="Play Darkwatch.exe"&&darker.ConfigNames.Single()=="Darkwatch Darker Edition.exe","correct Darkwatch source and distinct launchers");Check(!GameContent.Supports(new Game{Name=darker.Name,Executable="Play Darkwatch.exe",Repository=darker.Repository}),"Darker Edition uses original importer instead of legacy assets copy");
      var beta=new PortEntry{Id="beta-fixture",Repository="test/beta",AllowPrerelease=true,AssetPattern=@"^ArizonaVampRecomp-.*-windows-x64\.zip$"};var betaPayload=new {tag_name="beta-test",html_url=beta.ProjectUrl+"/releases/tag/beta-test",draft=false,prerelease=true,assets=new[]{new{name="ArizonaVampRecomp-beta-windows-x64.zip",size=100,browser_download_url=beta.ProjectUrl+"/releases/download/beta-test/ArizonaVampRecomp-beta-windows-x64.zip",digest=(string)null}}};
      string betaJson=new JavaScriptSerializer().Serialize(betaPayload);var betaRelease=PortCatalog.ParseRelease(beta,betaJson);Check(betaRelease.IsPrerelease,"beta flag survives parsing and cache");
      Reject(()=>PortCatalog.ParseRelease(beta,betaJson.Replace("\"draft\":false","\"draft\":true")),"beta profile still rejects drafts");
      Check(PortCatalog.ParseReleaseList(beta,"["+betaJson.Replace("\"draft\":false","\"draft\":true")+","+betaJson+"]").IsPrerelease,"beta listing skips drafts");
      using(var catalog=new PortCatalog(new RateLimitHandler()))Reject(()=>catalog.Install(beta,betaRelease,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"beta install requires explicit acceptance before download");
      var azimuth=entries.First(e=>e.Id=="azimuth");string azZip=Path.Combine(root,"azimuth-windows (4).zip");using(var zip=ZipFile.Open(azZip,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("azimuth.exe").Open()))writer.Write("fixture");
      using(var catalog=new PortCatalog(new RateLimitHandler())){
        var imported=catalog.ImportZip(azimuth,azZip,installRoot,null,CancellationToken.None).GetAwaiter().GetResult();Check(imported.Platform=="Xbox One"&&imported.Repository==azimuth.SourceUrl&&File.Exists(imported.ConfigurationExecutable)&&imported.InstalledVersion.StartsWith("zip-"),"official-site ZIP import maps source, platform and optional launcher");
        Check(File.Exists(azZip),"import preserves original ZIP");Reject(()=>catalog.ImportZip(azimuth,azZip,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"identical ZIP cannot overwrite installation");
        Reject(()=>catalog.ImportZip(entry,azZip,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"GitHub profile cannot be silently imported as external ZIP");
        Reject(()=>catalog.ImportZip(azimuth,package,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"wrong ZIP name rejected");
        string badLocal=Path.Combine(root,"bad-local");Directory.CreateDirectory(badLocal);string badLocalZip=Path.Combine(badLocal,"azimuth-windows.zip");using(var zip=ZipFile.Open(badLocalZip,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("not-the-launcher.exe").Open()))writer.Write("fixture");Reject(()=>catalog.ImportZip(azimuth,badLocalZip,installRoot,null,CancellationToken.None).GetAwaiter().GetResult(),"external ZIP requires expected executable");
        var token=new CancellationTokenSource();token.Cancel();Reject(()=>catalog.ImportZip(azimuth,azZip,Path.Combine(root,"import-cancel"),null,token.Token).GetAwaiter().GetResult(),"ZIP import cancellation");Check(!Directory.GetFileSystemEntries(installRoot,".nexus-install-*").Any()&&!Directory.GetFileSystemEntries(Path.Combine(root,"import-cancel"),".nexus-install-*").Any(),"failed and cancelled imports clean temporary files");
      }
      string folderGame=Path.Combine(root,"folder-game ç");Directory.CreateDirectory(folderGame);File.WriteAllText(Path.Combine(folderGame,"default.xex"),"fixture");File.WriteAllText(Path.Combine(folderGame,"default.xbe"),"fixture");
      var pgr=entries.First(e=>e.Id=="pgr4");var pgrGame=new Game{Name=pgr.Name,Repository=pgr.Repository,Executable=Path.Combine(folderGame,pgr.PlayNames[0]),Arguments=pgr.Arguments};File.WriteAllText(pgrGame.Executable,"fixture");Check(FolderLaunch.NeedsSetup(pgrGame),"PGR4 requests folder before play");pgrGame.SourceGameDirectory=folderGame;var pgrStart=Ports.StartInfo(pgrGame);Check(pgrStart.Arguments.Contains("--game_data_root "+Uncharted.Quote(folderGame))&&pgrStart.Arguments.Contains("--gpu_plugin=\"xenos\""),"PGR4 verified package flags and quoted source folder");
      var conker=entries.First(e=>e.Id=="conker-lr");var conkerGame=new Game{Name=conker.Name,Repository=conker.Repository,Executable=Path.Combine(folderGame,"clx.exe"),SourceGameDirectory=folderGame};File.WriteAllText(conkerGame.Executable,"fixture");string beforeConker=Environment.GetEnvironmentVariable("NFSU2_GAME_DIR");var conkerStart=Ports.StartInfo(conkerGame);Check(!conkerStart.UseShellExecute&&!String.IsNullOrEmpty(conkerStart.EnvironmentVariables["RECOMP_CACHE_DIR"])&&conkerStart.EnvironmentVariables["NFSU2_GAME_DIR"]==folderGame,"Conker launches native EXE with selected disc environment");Check(Environment.GetEnvironmentVariable("NFSU2_GAME_DIR")==beforeConker,"Conker launch does not alter parent environment");
      Reject(()=>FolderLaunch.Validate(pgrGame,Path.Combine(root,"missing-folder")),"folder selector rejects missing data");var sourceLibrary=new Library();sourceLibrary.Games.Add(conkerGame);var sourceStore=new Store(Path.Combine(root,"source-store"));sourceStore.Save(sourceLibrary);Check(sourceStore.Load().Games[0].SourceGameDirectory==folderGame,"native source folder persists across restart");
      foreach(string id in new[]{"knack2","drakengard3","conker-lr","las-recomp"})Check(PortCatalog.BundledRelease(entries.First(e=>e.Id==id)).IsPrerelease,"experimental release stays visibly marked "+id);
      var stableBlue=entries.First(e=>e.Id=="blue-dragon");Check(!PortCatalog.BundledRelease(stableBlue).IsPrerelease&&!PortCatalog.BundledRelease(stableBlue).AssetName.Contains("symbols"),"Blue Dragon selects stable game package without nightly or symbols");
      var gameA=new Game{Name="Halo",Executable="halo.exe",Platform="Xbox",Favorite=true};var gameB=new Game{Name="Sonic",Executable="sonic.exe",Platform="Xbox 360"};
      Check(Platforms.Filter(new[]{gameA,gameB},"Xbox","",false).Count==1,"exact platform filter does not mix Xbox and Xbox 360");
      Check(Platforms.Filter(new[]{gameA,gameB},Platforms.All[0],"sonic",false).Single()==gameB,"search combines with platform");
      Check(Platforms.Filter(new[]{gameA,gameB},"Xbox 360","",true).Count==0,"favorites combines with platform");
      var old=Store.Parse("{\"Version\":1,\"Games\":[{\"Id\":\"old\",\"Name\":\"God of War III\",\"Executable\":\"C:\\\\gow3\\\\Play God of War III.exe\"}]}");Check(old.Games[0].Platform=="PlayStation 4","legacy known ports retain original platform");
      Check(Platforms.Resolve(new Game{Name="Unknown",Executable="custom.exe"})=="Outras","unknown manual games not mislabeled PlayStation");
      var manual=entries.First(e=>e.ProjectOnly);using(var catalog=new PortCatalog(new RateLimitHandler()))Reject(()=>catalog.Latest(manual,CancellationToken.None).GetAwaiter().GetResult(),"project-only entries never invoke installer query");
      return checks;
    }
    class FixtureHandler:HttpMessageHandler {
      readonly byte[] bytes;public FixtureHandler(byte[] content){bytes=content;}
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(bytes)});}
    }
    class RateLimitHandler:HttpMessageHandler {
      public int Count;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Count++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden){RequestMessage=request,Content=new StringContent("rate limited")});}
    }
  }
}
