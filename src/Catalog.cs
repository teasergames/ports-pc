using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Web.Script.Serialization;

namespace Nexus {
  public class PortEntry {
    public string Id {get;set;}public string Name {get;set;}public string Repository {get;set;}public string AssetPattern {get;set;}
    public string[] PlayNames {get;set;}public string[] ConfigNames {get;set;}public string Arguments {get;set;}public string ArtworkTitle {get;set;}public string Notes {get;set;}
    public string Platform {get;set;}public string Kind {get;set;}public bool ProjectOnly {get;set;}
    public override string ToString(){return Name;}
  }
  public class PortRelease {
    public string Version;public string ReleaseUrl;public string AssetName;public string DownloadUrl;public long Size;public string Digest;public bool FromCache;public string CheckedUtc;
  }
  public class InstallProgress {public string Message;public double Percent;}
  public class PortCatalog:IDisposable {
    readonly HttpClient client;public readonly List<PortEntry> Entries;
    readonly Dictionary<string,PortRelease> memo=new Dictionary<string,PortRelease>();
    readonly Dictionary<string,DateTime> memoTime=new Dictionary<string,DateTime>();
    public PortCatalog():this(new HttpClientHandler()){}
    public PortCatalog(HttpMessageHandler handler){
      Entries=LoadEntries();ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;client=new HttpClient(handler);client.Timeout=Timeout.InfiniteTimeSpan;client.DefaultRequestHeaders.UserAgent.ParseAdd("NexusPortsPC/1.2 (https://github.com/teasergames/ports-pc)");
    }
    public static List<PortEntry> LoadEntries(){
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.ports.json"))using(var reader=new StreamReader(stream)){
        var entries=new JavaScriptSerializer().Deserialize<List<PortEntry>>(reader.ReadToEnd());var ids=new HashSet<string>();
        foreach(var e in entries){if(!Regex.IsMatch(e.Id??"",@"^[a-z0-9-]+$")||!ids.Add(e.Id)||!Regex.IsMatch(e.Repository??"",@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")||e.PlayNames==null||e.ConfigNames==null)throw new InvalidDataException("Entrada do catálogo inválida.");if(!Platforms.All.Skip(1).Contains(e.Platform))throw new InvalidDataException("Plataforma inválida.");if(!e.ProjectOnly){if(e.PlayNames.Length==0||!e.AssetPattern.EndsWith("zip$"))throw new InvalidDataException("Perfil Windows inválido.");new Regex(e.AssetPattern);}}
        return entries;
      }
    }
    public async Task<PortRelease> Latest(PortEntry entry,CancellationToken token,bool force=false){
      token.ThrowIfCancellationRequested();if(entry.ProjectOnly)throw new InvalidDataException("Este projeto não tem instalação automática validada. Abra o projeto para instruções.");PortRelease previous;DateTime checkedAt;
      if(!force&&memo.TryGetValue(entry.Repository,out previous)&&memoTime.TryGetValue(entry.Repository,out checkedAt)&&(DateTime.UtcNow-checkedAt).TotalMinutes<10)return previous;
      try{var current=await FetchLatest(entry,token);current.CheckedUtc=DateTime.UtcNow.ToString("o");memo[entry.Repository]=current;memoTime[entry.Repository]=DateTime.UtcNow;return current;}
      catch(HttpRequestException){var cached=BundledRelease(entry);if(cached==null)throw;memo[entry.Repository]=cached;memoTime[entry.Repository]=DateTime.UtcNow;return cached;}
      catch(OperationCanceledException){if(token.IsCancellationRequested)throw;var cached=BundledRelease(entry);if(cached==null)throw;memo[entry.Repository]=cached;memoTime[entry.Repository]=DateTime.UtcNow;return cached;}
    }
    public static PortRelease BundledRelease(PortEntry entry){
      using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.releases-cache.json"))using(var reader=new StreamReader(stream)){
        var records=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<List<Dictionary<string,object>>>(reader.ReadToEnd());
        foreach(var record in records)if(Convert.ToString(record["Repository"])==entry.Repository){var release=ParseRelease(entry,new JavaScriptSerializer().Serialize(record["Release"]));release.FromCache=true;release.CheckedUtc=Convert.ToString(record["CheckedUtc"]);return release;}return null;
      }
    }
    async Task<PortRelease> FetchLatest(PortEntry entry,CancellationToken token){
      using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using(var response=await client.GetAsync("https://api.github.com/repos/"+entry.Repository+"/releases/latest",HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
          if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidDataException("Este repositório não publicou uma release estável acessível.");
          if(response.StatusCode==HttpStatusCode.Forbidden||(int)response.StatusCode==429)throw new HttpRequestException("O GitHub limitou as consultas. Aguarde e tente novamente.");response.EnsureSuccessStatusCode();
          using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream()){
            byte[] buffer=new byte[16384];int read;while((read=await stream.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0){if(bytes.Length+read>4*1024*1024)throw new InvalidDataException("Resposta do GitHub muito grande.");bytes.Write(buffer,0,read);}
            return ParseRelease(entry,System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
          }
        }
      }
    }
    public static PortRelease ParseRelease(PortEntry entry,string json){
      var data=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,object>>(json);
      if(data.ContainsKey("draft")&&Convert.ToBoolean(data["draft"])||data.ContainsKey("prerelease")&&Convert.ToBoolean(data["prerelease"]))throw new InvalidDataException("A release não é estável.");
      object value;var assets=data.TryGetValue("assets",out value)?value as System.Collections.IEnumerable:null;var found=new List<Dictionary<string,object>>();
      if(assets!=null)foreach(object asset in assets){var a=asset as Dictionary<string,object>;if(a!=null&&Regex.IsMatch(Convert.ToString(a["name"]),entry.AssetPattern,RegexOptions.IgnoreCase))found.Add(a);}
      if(found.Count!=1)throw new InvalidDataException(found.Count==0?"A última release não tem o ZIP Windows esperado. Abra o projeto para conferir.":"Mais de um pacote corresponde ao perfil. A seleção precisa ser revisada.");
      var chosen=found[0];string url=Convert.ToString(chosen["browser_download_url"]);ValidateDownloadUrl(entry,url);
      long size=Convert.ToInt64(chosen["size"]);if(size<=0||size>2L*1024*1024*1024)throw new InvalidDataException("Tamanho de pacote inválido ou acima de 2 GB.");
      return new PortRelease{Version=Convert.ToString(data["tag_name"]),ReleaseUrl=Convert.ToString(data["html_url"]),AssetName=Convert.ToString(chosen["name"]),DownloadUrl=url,Size=size,Digest=chosen.TryGetValue("digest",out value)?Convert.ToString(value):null};
    }
    public static void ValidateDownloadUrl(PortEntry entry,string url){
      Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/"+entry.Repository+"/releases/download/",StringComparison.OrdinalIgnoreCase)||!String.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("O pacote não pertence às Releases do projeto selecionado.");
    }
    public static string VersionFolder(string version){string slug=Regex.Replace(version??"",@"[^A-Za-z0-9._-]","_").Trim('.');if(String.IsNullOrEmpty(slug)||slug.Length>100)throw new InvalidDataException("Identificador de versão inválido.");return slug;}
    public static string ChildPath(string parent,string child){
      string root=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
      string target=Path.GetFullPath(Path.Combine(root,child));if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho fora da pasta de instalação.");return target;
    }
    public static string FsPath(string path){
      string full=Path.GetFullPath(path);if(Environment.OSVersion.Platform!=PlatformID.Win32NT||full.StartsWith(@"\\?\"))return full;
      return full.StartsWith(@"\\")?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;
    }
    static string DisplayPath(string path){return path.StartsWith(@"\\?\UNC\")?@"\\"+path.Substring(8):path.StartsWith(@"\\?\")?path.Substring(4):path;}
    public async Task<Game> Install(PortEntry entry,PortRelease release,string parent,IProgress<InstallProgress> progress,CancellationToken token){
      if(entry.ProjectOnly)throw new InvalidDataException("Instalação automática indisponível para este projeto.");ValidateDownloadUrl(entry,release.DownloadUrl);string root=Path.GetFullPath(parent);Directory.CreateDirectory(FsPath(root));
      string final=ChildPath(root,entry.Id+"-"+VersionFolder(release.Version));if(Directory.Exists(FsPath(final))||File.Exists(FsPath(final)))throw new IOException("Esta pasta de versão já existe. Escolha outro destino; nenhum arquivo foi substituído.");
      string staging=ChildPath(root,".nexus-install-"+Guid.NewGuid().ToString("N")),download=staging+".zip";Directory.CreateDirectory(FsPath(staging));
      try{
        using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
          timeout.CancelAfter(TimeSpan.FromHours(1));
          using(var response=await client.GetAsync(release.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
            response.EnsureSuccessStatusCode();string host=response.RequestMessage.RequestUri.Host;
            if(host!="github.com"&&host!="release-assets.githubusercontent.com"&&host!="objects.githubusercontent.com")throw new InvalidDataException("O GitHub redirecionou o download para um servidor não reconhecido.");
            if(response.Content.Headers.ContentLength.HasValue&&response.Content.Headers.ContentLength.Value!=release.Size)throw new InvalidDataException("O tamanho retornado difere do publicado pelo GitHub.");
            using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(FsPath(download),FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
              byte[] buffer=new byte[65536];long total=0;int read;DateTime last=DateTime.MinValue;
              while((read=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0){total+=read;if(total>release.Size)throw new InvalidDataException("O download excedeu o tamanho publicado.");await output.WriteAsync(buffer,0,read,timeout.Token);if((DateTime.UtcNow-last).TotalMilliseconds>120){Report(progress,"Baixando "+(total/1048576.0).ToString("0.0")+" / "+(release.Size/1048576.0).ToString("0.0")+" MB",total*75.0/release.Size);last=DateTime.UtcNow;}}
              if(total!=release.Size)throw new InvalidDataException("Download incompleto.");
            }
          }
        }
        Report(progress,"Verificando pacote…",77);VerifyDigest(download,release.Digest);token.ThrowIfCancellationRequested();
        await Task.Run(()=>Extract(download,staging,progress,token),token);
        Game game=Register(entry,staging,release.Version);token.ThrowIfCancellationRequested();
        // Paths are rewritten only after the complete package has been validated.
        string exe=game.Executable.Substring(staging.Length),config=String.IsNullOrEmpty(game.ConfigurationExecutable)?null:game.ConfigurationExecutable.Substring(staging.Length);
        File.WriteAllText(FsPath(ChildPath(staging,".nexus-install.json")),new JavaScriptSerializer().Serialize(new {repository=entry.Repository,version=release.Version,asset=release.AssetName,digest=release.Digest,downloadedAt=DateTime.UtcNow.ToString("o")}));
        Directory.Move(FsPath(staging),FsPath(final));game.Executable=final+exe;game.ConfigurationExecutable=config==null?null:final+config;game.WorkingDirectory=Path.GetDirectoryName(game.Executable);game.ManagedInstallationRoot=final;
        Report(progress,"Instalado e pronto para adicionar à biblioteca.",100);return game;
      }finally{
        if(File.Exists(FsPath(download)))File.Delete(FsPath(download));
        if(Directory.Exists(FsPath(staging)))RemoveStaging(root,staging);
      }
    }
    public static void VerifyDigest(string file,string digest){
      if(String.IsNullOrEmpty(digest))return;
      if(!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidDataException("O checksum publicado não é SHA-256 válido.");
      using(var stream=File.OpenRead(FsPath(file)))using(var hash=SHA256.Create()){string actual=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");if(!actual.Equals(digest.Substring(7),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 não confere. O pacote não será instalado.");}
    }
    public static void Extract(string zip,string folder,IProgress<InstallProgress> progress,CancellationToken token){
      using(var archive=ZipFile.OpenRead(FsPath(zip))){
        if(archive.Entries.Count>50000)throw new InvalidDataException("O pacote contém arquivos demais.");long total=0;
        foreach(var entry in archive.Entries){if(entry.Length>2L*1024*1024*1024)throw new InvalidDataException("Arquivo individual acima de 2 GB.");total=checked(total+entry.Length);}
        if(total>8L*1024*1024*1024)throw new InvalidDataException("O pacote extraído excede 8 GB.");
        long done=0;int count=0;foreach(var entry in archive.Entries){
          token.ThrowIfCancellationRequested();string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);
          if(Path.IsPathRooted(name)||name.Contains(":"))throw new InvalidDataException("Caminho inválido no ZIP.");
          if(((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Links não são aceitos no pacote.");
          string dest=ChildPath(folder,name);if(String.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(FsPath(dest));continue;}Directory.CreateDirectory(FsPath(Path.GetDirectoryName(dest)));
          using(var input=entry.Open())using(var output=new FileStream(FsPath(dest),FileMode.CreateNew,FileAccess.Write)){
            byte[] buffer=new byte[65536];long written=0;int read;while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();written+=read;if(written>entry.Length)throw new InvalidDataException("Conteúdo maior que o tamanho informado no ZIP.");output.Write(buffer,0,read);}if(written!=entry.Length)throw new InvalidDataException("Arquivo incompleto no ZIP.");
          }
          done+=entry.Length;count++;if(count%25==0)Report(progress,"Extraindo "+count+" / "+archive.Entries.Count+" arquivos…",80+done*18.0/Math.Max(1,total));
        }
      }
    }
    public static Game Register(PortEntry entry,string folder,string version){
      string exe=FindExecutable(folder,entry.PlayNames),config=FindExecutable(folder,entry.ConfigNames);
      if(entry.ProjectOnly||exe==null||(entry.ConfigNames.Length>0&&config==null))throw new InvalidDataException("A estrutura deste pacote mudou: executáveis esperados não encontrados. Nada foi cadastrado; confira o projeto.");
      return new Game{Name=entry.Name,Platform=entry.Platform,Executable=exe,Arguments=entry.Arguments??"",WorkingDirectory=Path.GetDirectoryName(exe),ConfigurationExecutable=config,ArtworkSearchTitle=entry.ArtworkTitle,Repository=entry.Repository,InstalledVersion=version,ManagedInstallationRoot=folder};
    }
    static string FindExecutable(string root,string[] names){
      // Packages may have one or several enclosing folders. Never pick a runtime by extension alone.
      var files=Directory.GetFiles(FsPath(root),"*.exe",SearchOption.AllDirectories).Select(DisplayPath).ToArray();foreach(string name in names){var matches=files.Where(f=>Path.GetFileName(f).Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length==1)return matches[0];if(matches.Length>1)throw new InvalidDataException("Executável duplicado no pacote: "+name);}return null;
    }
    static void RemoveStaging(string root,string staging){string safe=ChildPath(root,Path.GetFileName(staging));if(safe.Equals(Path.GetFullPath(staging),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(safe).StartsWith(".nexus-install-",StringComparison.Ordinal))Directory.Delete(FsPath(safe),true);}
    static void Report(IProgress<InstallProgress> progress,string message,double percent){if(progress!=null)progress.Report(new InstallProgress{Message=message,Percent=percent});}
    public void Dispose(){client.Dispose();}
  }
}
