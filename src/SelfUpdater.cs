using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;

namespace Nexus {
  public class LauncherUpdatePlan {
    public string Target,DataRoot,Version,Token,OldHash,State;
    public int ParentId;public long ParentTicks;
    public Dictionary<string,string> Files,Originals;
  }
  public sealed class SelfUpdater:IDisposable {
    public const string Repository="teasergames/ports-pc";
    public static readonly string[] PackageFiles={"Nexus.exe","Nexus.exe.config","README.md","CATALOG.md","LICENSE"};
    internal static string TestJobsRoot;
    public static string JobsRoot {get{return TestJobsRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NexusPortsPC","launcher-updates");}}
    public static string CurrentExe {get{return Process.GetCurrentProcess().MainModule.FileName;}}
    public static string CurrentVersion {get{return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);}}
    readonly HttpClient client;
    public SelfUpdater():this(new HttpClientHandler()){}
    public SelfUpdater(HttpMessageHandler handler){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("NexusPortsPC/"+CurrentVersion);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");}
    public void Dispose(){client.Dispose();}
    static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1024*1024};}
    public static Version StableVersion(string tag){Version v;string s=(tag??"").TrimStart('v');if(!Regex.IsMatch(s,@"^\d+\.\d+\.\d+(\.\d+)?$")||!Version.TryParse(s,out v))throw new InvalidDataException("Versão estável do NEXUS inválida.");return new Version(v.Major,v.Minor,v.Build,Math.Max(0,v.Revision));}
    public static PortRelease Parse(string json){
      var entry=new PortEntry{Repository=Repository,AssetPattern=@"^Nexus-Ports-PC-v\d+\.\d+\.\d+(\.\d+)?-Windows-x64\.zip$"};var r=PortCatalog.ParseRelease(entry,json);StableVersion(r.Version);
      if(r.Size>100*1024*1024||!Regex.IsMatch(r.Digest??"",@"^sha256:[0-9a-fA-F]{64}$")||r.AssetName!="Nexus-Ports-PC-v"+r.Version.TrimStart('v')+"-Windows-x64.zip")throw new InvalidDataException("Pacote do NEXUS sem tamanho, versão ou SHA-256 válido.");
      Uri uri=new Uri(r.DownloadUrl);if(uri.AbsolutePath!="/"+Repository+"/releases/download/"+r.Version+"/"+r.AssetName||!String.IsNullOrEmpty(uri.Query)||!String.IsNullOrEmpty(uri.Fragment))throw new InvalidDataException("Endereço de atualização inválido.");return r;
    }
    public async Task<PortRelease> Check(CancellationToken token){
      using(var limit=CancellationTokenSource.CreateLinkedTokenSource(token)){limit.CancelAfter(TimeSpan.FromSeconds(20));using(var response=await client.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",HttpCompletionOption.ResponseHeadersRead,limit.Token)){response.EnsureSuccessStatusCode();using(var input=await response.Content.ReadAsStreamAsync())using(var data=new MemoryStream()){var bytes=new byte[16384];int read;while((read=await input.ReadAsync(bytes,0,bytes.Length,limit.Token))>0){if(data.Length+read>1024*1024)throw new InvalidDataException("Resposta de atualização muito grande.");data.Write(bytes,0,read);}var release=Parse(System.Text.Encoding.UTF8.GetString(data.ToArray()));return StableVersion(release.Version)>StableVersion(CurrentVersion)?release:null;}}}
    }
    public async Task<string> Download(PortRelease release,string dataRoot,IProgress<InstallProgress> progress,CancellationToken token){
      PortCatalog.ValidateDownloadUrl(new PortEntry{Repository=Repository},release.DownloadUrl);if(release.Size<1||release.Size>100*1024*1024||!Regex.IsMatch(release.Digest??"",@"^sha256:[0-9a-fA-F]{64}$"))throw new InvalidDataException("Metadados da atualização inválidos.");
      if(StableVersion(release.Version)<=StableVersion(CurrentVersion))throw new InvalidDataException("A atualização precisa ser mais recente.");
      string job=NewJob();try{string zip=Path.Combine(job,"download.zip");using(var limit=CancellationTokenSource.CreateLinkedTokenSource(token)){limit.CancelAfter(TimeSpan.FromMinutes(5));using(var response=await client.GetAsync(release.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,limit.Token)){response.EnsureSuccessStatusCode();var uri=response.RequestMessage.RequestUri;if(uri.Scheme!="https"||!new[]{"github.com","release-assets.githubusercontent.com","objects.githubusercontent.com"}.Contains(uri.Host))throw new InvalidDataException("Servidor de atualização não reconhecido.");if(response.Content.Headers.ContentLength.HasValue&&response.Content.Headers.ContentLength.Value!=release.Size)throw new InvalidDataException("Tamanho do download divergente.");using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){long total=0;int read;byte[] buffer=new byte[65536];while((read=await input.ReadAsync(buffer,0,buffer.Length,limit.Token))>0){total+=read;if(total>release.Size)throw new InvalidDataException("Download acima do tamanho publicado.");await output.WriteAsync(buffer,0,read,limit.Token);if(progress!=null)progress.Report(new InstallProgress{Message="Baixando NEXUS "+release.Version+"…",Percent=total*85.0/release.Size});}if(total!=release.Size)throw new InvalidDataException("Download incompleto.");}}}
        if(progress!=null)progress.Report(new InstallProgress{Message="Verificando e preparando a atualização…",Percent=90});string plan=await Task.Run(()=>Prepare(zip,release,job,CurrentExe,dataRoot,token),token);File.Delete(zip);return plan;
      }catch{try{ValidateJob(job);if(!Directory.Exists(Path.Combine(job,"backup")))Directory.Delete(job,true);}catch{}throw;}
    }
    public static string NewJob(){Directory.CreateDirectory(JobsRoot);NoLinks(JobsRoot);string job=Path.Combine(JobsRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);return job;}
    public static void NoLinks(string path){string p=Path.GetFullPath(path);while(!String.IsNullOrEmpty(p)){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Atualização indisponível em caminho com link ou redirecionamento.");p=Path.GetDirectoryName(p);}}
    public static void ValidateJob(string job){string full=Path.GetFullPath(job);Guid id;if(Path.GetDirectoryName(full)!=Path.GetFullPath(JobsRoot)||!Guid.TryParseExact(Path.GetFileName(full),"N",out id))throw new InvalidDataException("Pasta de atualização inválida.");NoLinks(full);}
    public static string Prepare(string zip,PortRelease release,string job,string target,string dataRoot,CancellationToken token){
      ValidateJob(job);if(!Regex.IsMatch(release.Digest??"",@"^sha256:[0-9a-fA-F]{64}$"))throw new InvalidDataException("SHA-256 obrigatório para atualizar o NEXUS.");PortCatalog.VerifyDigest(zip,release.Digest);string payload=Path.Combine(job,"payload");Directory.CreateDirectory(payload);long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      using(var archive=ZipFile.OpenRead(zip)){foreach(var file in archive.Entries){token.ThrowIfCancellationRequested();string name=file.FullName.Replace('\\','/');if(name=="Nexus/"&&file.Length==0)continue;if(name.StartsWith("Nexus/",StringComparison.Ordinal))name=name.Substring(6);if(!PackageFiles.Contains(name,StringComparer.Ordinal)||!names.Add(name)||file.Length<1||file.Length>80*1024*1024||(file.ExternalAttributes>>16&0xF000)==0xA000||(file.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Conteúdo inesperado no pacote do NEXUS.");total+=file.Length;if(total>120*1024*1024)throw new InvalidDataException("Pacote descompactado muito grande.");using(var input=file.Open())using(var output=File.Create(Path.Combine(payload,name)))input.CopyTo(output);}}
      if(names.Count!=PackageFiles.Length)throw new InvalidDataException("Pacote incompleto do NEXUS.");string next=Path.Combine(payload,"Nexus.exe");if(!PackageSupport.WindowsExecutable(next)||AssemblyName.GetAssemblyName(next).Version!=StableVersion(release.Version))throw new InvalidDataException("Executável não corresponde à versão publicada.");
      var xml=new System.Xml.XmlDocument{XmlResolver=null};xml.Load(Path.Combine(payload,"Nexus.exe.config"));if(xml.DocumentElement==null||xml.DocumentElement.Name!="configuration")throw new InvalidDataException("Configuração do pacote inválida.");
      target=Path.GetFullPath(target);if(Path.GetFileName(target)!="Nexus.exe"||!File.Exists(target))throw new InvalidDataException("Inicie o NEXUS a partir da pasta extraída para atualizar.");NoLinks(target);string directory=Path.GetDirectoryName(target);foreach(string name in PackageFiles)NoLinks(Path.Combine(directory,name));
      string probe=Path.Combine(directory,".nexus-write-"+Guid.NewGuid().ToString("N"));using(File.Create(probe)){}File.Delete(probe);
      var process=Process.GetCurrentProcess();var plan=new LauncherUpdatePlan{Target=target,DataRoot=Path.GetFullPath(dataRoot),Version=release.Version,Token=Guid.NewGuid().ToString("N"),OldHash=UpdateManager.Hash(target),State="prepared",ParentId=process.Id,ParentTicks=process.StartTime.ToUniversalTime().Ticks,Files=PackageFiles.ToDictionary(n=>n,n=>UpdateManager.Hash(Path.Combine(payload,n))),Originals=PackageFiles.ToDictionary(n=>n,n=>File.Exists(Path.Combine(directory,n))?UpdateManager.Hash(Path.Combine(directory,n)):null)};
      File.Copy(CurrentExe,Path.Combine(job,"NexusUpdate.exe"));File.Copy(CurrentExe+".config",Path.Combine(job,"NexusUpdate.exe.config"));Save(job,plan);return Path.Combine(job,"plan.json");
    }
    public static void Save(string job,LauncherUpdatePlan plan){string file=Path.Combine(job,"plan.json"),temp=file+".tmp";File.WriteAllText(temp,Json().Serialize(plan));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
    public static LauncherUpdatePlan Read(string planFile){
      string job=Path.GetDirectoryName(Path.GetFullPath(planFile));ValidateJob(job);if(Path.GetFileName(planFile)!="plan.json"||new FileInfo(planFile).Length>65536)throw new InvalidDataException("Plano de atualização inválido.");var p=Json().Deserialize<LauncherUpdatePlan>(File.ReadAllText(planFile));Guid id;
      if(p==null||!Guid.TryParseExact(p.Token,"N",out id)||Path.GetFileName(p.Target)!="Nexus.exe"||!Path.IsPathRooted(p.Target)||!Path.IsPathRooted(p.DataRoot)||p.Files==null||p.Originals==null||p.Files.Count!=5||p.Originals.Count!=5||PackageFiles.Any(n=>!p.Files.ContainsKey(n)||!p.Originals.ContainsKey(n)||!Regex.IsMatch(p.Files[n]??"",@"^[0-9a-f]{64}$")||p.Originals[n]!=null&&!Regex.IsMatch(p.Originals[n],@"^[0-9a-f]{64}$"))||p.OldHash!=p.Originals["Nexus.exe"])throw new InvalidDataException("Plano de atualização inconsistente.");StableVersion(p.Version);NoLinks(p.Target);return p;
    }
    public static Process StartHelper(string planFile,bool recovery){
      var p=Read(planFile);if(!String.Equals(p.Target,CurrentExe,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O plano pertence a outra instalação.");string ready=Path.Combine(Path.GetDirectoryName(planFile),"ready.txt");if(File.Exists(ready))File.Delete(ready);
      var helper=Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(planFile),"NexusUpdate.exe"),"--apply-nexus-update "+Uncharted.Quote(planFile)+(recovery?" --recover-nexus-update":"")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(planFile)});if(helper==null)throw new IOException("Não foi possível abrir o auxiliar.");
      try{var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(8)){if(File.Exists(ready)&&File.ReadAllText(ready)==p.Token)return helper;if(helper.HasExited)throw new IOException("O auxiliar não conseguiu iniciar. O NEXUS continuará aberto.");Thread.Sleep(50);}throw new IOException("O auxiliar não confirmou o início. Tente novamente.");}catch{if(!helper.HasExited){helper.Kill();helper.WaitForExit(5000);}helper.Dispose();throw;}
    }
    public static void DiscardPrepared(string planFile){if(String.IsNullOrEmpty(planFile))return;var p=Read(planFile);string job=Path.GetDirectoryName(planFile);if(p.State=="prepared"&&!Directory.Exists(Path.Combine(job,"backup")))Directory.Delete(job,true);}
    static void WaitForParent(LauncherUpdatePlan p){try{using(var parent=Process.GetProcessById(p.ParentId)){if(parent.StartTime.ToUniversalTime().Ticks!=p.ParentTicks)return;if(!parent.WaitForExit(60000))throw new IOException("O NEXUS anterior continua aberto; atualização adiada.");}}catch(ArgumentException){}}
    static string Destination(LauncherUpdatePlan p,string name){string file=Path.Combine(Path.GetDirectoryName(p.Target),name);NoLinks(file);return file;}
    static void Replace(string source,string destination){string temp=destination+".nexus-new-"+Guid.NewGuid().ToString("N");try{File.Copy(source,temp);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}}
    public static void Apply(string job,LauncherUpdatePlan p,Action<int> afterFile){
      ValidateJob(job);foreach(string n in PackageFiles){string d=Destination(p,n);if((File.Exists(d)?UpdateManager.Hash(d):null)!=p.Originals[n]||UpdateManager.Hash(Path.Combine(job,"payload",n))!=p.Files[n])throw new IOException("Os arquivos mudaram desde a preparação; atualização cancelada.");}
      string backup=Path.Combine(job,"backup");Directory.CreateDirectory(backup);foreach(string n in PackageFiles)if(p.Originals[n]!=null)File.Copy(Destination(p,n),Path.Combine(backup,n),false);
      p.State="applying";Save(job,p);int count=0;foreach(string n in PackageFiles.OrderBy(n=>n=="Nexus.exe"?1:0)){Replace(Path.Combine(job,"payload",n),Destination(p,n));if(afterFile!=null)afterFile(++count);}p.State="launching";Save(job,p);
    }
    public static void Rollback(string job,LauncherUpdatePlan p){
      ValidateJob(job);foreach(string n in PackageFiles){string dest=Destination(p,n),hash=File.Exists(dest)?UpdateManager.Hash(dest):null;if(hash==p.Originals[n])continue;if(hash!=p.Files[n])throw new IOException("Arquivo alterado fora da atualização; backup preservado: "+n);string backup=Path.Combine(job,"backup",n);if(p.Originals[n]!=null){if(!File.Exists(backup)||UpdateManager.Hash(backup)!=p.Originals[n])throw new IOException("Backup inválido: "+n);Replace(backup,dest);}else File.Delete(dest);}p.State="rolledback";Save(job,p);
    }
    static Process Restart(LauncherUpdatePlan p,string planFile,bool failed){return Process.Start(new ProcessStartInfo(p.Target,"--data-dir "+Uncharted.Quote(p.DataRoot)+" --skip-nexus-update "+(failed?"--nexus-update-failed":"--update-complete "+Uncharted.Quote(planFile))){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(p.Target)});}
    public static bool Acknowledge(string planFile){var p=Read(planFile);if(!String.Equals(CurrentExe,p.Target,StringComparison.OrdinalIgnoreCase)||UpdateManager.Hash(CurrentExe)!=p.Files["Nexus.exe"]||p.State!="launching")throw new InvalidDataException("Confirmação de atualização inválida.");File.WriteAllText(Path.Combine(Path.GetDirectoryName(planFile),"started.txt"),p.Token);return true;}
    public static int RunHelper(string planFile,bool recovery){
      LauncherUpdatePlan p=null;string job=Path.GetDirectoryName(Path.GetFullPath(planFile));bool touched=false,owns=false;Process child=null;Mutex guard=null;
      try{
        p=Read(planFile);if(!String.Equals(CurrentExe,Path.Combine(job,"NexusUpdate.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Auxiliar fora da pasta preparada.");
        guard=new Mutex(false,"Local\\NexusLauncherUpdate-"+Ports.Normalize(p.Target));try{owns=guard.WaitOne(0);}catch(AbandonedMutexException){owns=true;}if(!owns)return 0;
        File.WriteAllText(Path.Combine(job,"ready.txt"),p.Token);WaitForParent(p);
        if(recovery){if(p.State=="applying"||p.State=="launching")Rollback(job,p);else return 0;Restart(p,planFile,true);return 0;}
        if(p.State!="prepared")throw new InvalidDataException("Atualização já processada.");touched=true;Apply(job,p,null);child=Restart(p,planFile,false);if(child==null)throw new IOException("Não foi possível reiniciar o NEXUS.");
        var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(45)){string ack=Path.Combine(job,"started.txt");if(File.Exists(ack)&&File.ReadAllText(ack)==p.Token){p.State="complete";Save(job,p);return 0;}if(child.HasExited)throw new IOException("A nova versão fechou antes de confirmar o início.");Thread.Sleep(100);}
        throw new IOException("A nova versão não confirmou o início.");
      }catch(Exception ex){
        try{File.AppendAllText(Path.Combine(job,"update-error.txt"),ex.ToString()+Environment.NewLine);
          if(p!=null&&touched&&(p.State=="applying"||p.State=="launching")){if(child!=null&&!child.HasExited){child.CloseMainWindow();if(!child.WaitForExit(5000)){child.Kill();child.WaitForExit(5000);}}Rollback(job,p);Restart(p,planFile,true);}
          else if(p!=null&&p.State=="prepared"){p.State="failed";Save(job,p);Restart(p,planFile,true);}
        }catch(Exception rollback){try{File.AppendAllText(Path.Combine(job,"update-error.txt"),rollback.ToString());}catch{}}return 1;
      }finally{if(child!=null)child.Dispose();if(guard!=null){if(owns)guard.ReleaseMutex();guard.Dispose();}}
    }
    public static bool RecoverInterrupted(){
      if(!Directory.Exists(JobsRoot))return false;
      foreach(string job in Directory.GetDirectories(JobsRoot))try{
        string file=Path.Combine(job,"plan.json");if(!File.Exists(file))continue;var p=Read(file);if(!String.Equals(p.Target,CurrentExe,StringComparison.OrdinalIgnoreCase)||p.State!="applying"&&p.State!="launching")continue;
        using(var guard=new Mutex(false,"Local\\NexusLauncherUpdate-"+Ports.Normalize(p.Target))){bool owns=false;try{
          try{owns=guard.WaitOne(0);}catch(AbandonedMutexException){owns=true;}if(!owns)return true;
          if(File.Exists(Path.Combine(job,"started.txt"))&&File.ReadAllText(Path.Combine(job,"started.txt"))==p.Token&&UpdateManager.Hash(p.Target)==p.Files["Nexus.exe"]){p.State="complete";Save(job,p);continue;}
          var current=Process.GetCurrentProcess();p.ParentId=current.Id;p.ParentTicks=current.StartTime.ToUniversalTime().Ticks;Save(job,p);
        }finally{if(owns)guard.ReleaseMutex();}}
        using(var helper=StartHelper(file,true)){}return true;
      }catch{}return false;
    }
  }
}
