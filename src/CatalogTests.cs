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
      checks=0;var entries=PortCatalog.LoadEntries();Check(entries.Count==12,"12 catalog profiles");Check(entries.Select(e=>e.Repository).Distinct().Count()==12,"distinct sources including Shadow forks");
      var entry=entries.First(e=>e.Id=="gow3");string url="https://github.com/"+entry.Repository+"/releases/download/test/gow3-windows.zip";
      var payload=new {tag_name="test",html_url="https://github.com/"+entry.Repository+"/releases/tag/test",draft=false,prerelease=false,assets=new[]{new{name="gow3-windows.zip",size=100,browser_download_url=url,digest=(string)null},new{name="source.zip",size=100,browser_download_url=url,digest=(string)null},new{name="linux.AppImage",size=100,browser_download_url=url,digest=(string)null}}};
      string json=new JavaScriptSerializer().Serialize(payload);Check(PortCatalog.ParseRelease(entry,json).AssetName=="gow3-windows.zip","select Windows asset without source/linux files");
      Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("\"prerelease\":false","\"prerelease\":true")),"ignore prerelease");Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("gow3-windows.zip","different.zip")),"fail closed when asset changes");
      Reject(()=>PortCatalog.ParseRelease(entry,json.Replace("source.zip","gow3-windows.zip")),"reject ambiguous packages");
      Reject(()=>PortCatalog.ValidateDownloadUrl(entry,"https://github.com/other/project/releases/download/v1/gow3-windows.zip"),"reject different repository");Reject(()=>PortCatalog.ValidateDownloadUrl(entry,"http://github.com/"+entry.Repository+"/releases/download/v1/a.zip"),"require HTTPS");
      Reject(()=>PortCatalog.ChildPath(root,@"..\escape.exe"),"path traversal");Reject(()=>PortCatalog.ChildPath(root,Path.GetPathRoot(root)+"outside.exe"),"absolute path");Check(PortCatalog.ChildPath(root,"inside")!=root,"subdirectory accepted");
      string bad=Path.Combine(root,"zip-slip.zip"),dest=Path.Combine(root,"zip-slip-target");Directory.CreateDirectory(dest);using(var zip=ZipFile.Open(bad,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("../escaped.txt").Open()))writer.Write("no");Reject(()=>PortCatalog.Extract(bad,dest,null,CancellationToken.None),"ZIP traversal rejected");Check(!File.Exists(Path.Combine(root,"escaped.txt")),"no file escaped extraction");
      foreach(var e in entries){string folder=Path.Combine(root,"fixture-"+e.Id);string nested=Path.Combine(folder,"package","nested");Directory.CreateDirectory(nested);foreach(string name in e.PlayNames.Concat(e.ConfigNames).Distinct())File.WriteAllText(Path.Combine(nested,name),"fixture");var g=PortCatalog.Register(e,folder,"test");Check(g.Name==e.Name&&g.Repository==e.Repository&&g.InstalledVersion=="test"&&File.Exists(g.Executable)&&File.Exists(g.ConfigurationExecutable),"register nested package "+e.Id);}
      string empty=Path.Combine(root,"empty-port");Directory.CreateDirectory(empty);Reject(()=>PortCatalog.Register(entry,empty,"test"),"reject missing expected executables");
      string package=Path.Combine(root,"valid-package.zip");using(var zip=ZipFile.Open(package,ZipArchiveMode.Create)){foreach(string name in entry.PlayNames.Concat(entry.ConfigNames).Distinct())using(var writer=new StreamWriter(zip.CreateEntry("nested/"+name).Open()))writer.Write("fixture");}
      string digest;using(var sha=SHA256.Create())digest="sha256:"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(package))).Replace("-","").ToLowerInvariant();PortCatalog.VerifyDigest(package,digest);checks++;
      Reject(()=>PortCatalog.VerifyDigest(package,"sha256:"+new string('0',64)),"checksum mismatch");Reject(()=>PortCatalog.VerifyDigest(package,"invalid"),"invalid checksum");
      foreach(var e in entries){var cache=PortCatalog.BundledRelease(e);Check(cache!=null&&cache.FromCache&&!String.IsNullOrEmpty(cache.CheckedUtc),"audited fallback is dated "+e.Id);}
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
