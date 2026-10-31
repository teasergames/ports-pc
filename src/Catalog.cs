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
    public string ConfigurationArguments {get;set;}public string[] PlayNames {get;set;}public string[] ConfigNames {get;set;}public string Arguments {get;set;}public string ArtworkTitle {get;set;}public string Notes {get;set;}
    public string Platform {get;set;}public string Kind {get;set;}public bool ProjectOnly {get;set;}
    public bool AllowPrerelease {get;set;}public bool ManualPackage {get;set;}public string SourceUrl {get;set;}
    public bool DiscoverExecutable {get;set;}public string PackageKind {get;set;}
    public bool PreferRootExecutable {get;set;}public bool UsesInstaller {get{return PackageKind=="Installer"||PackageKind=="InstallerZip";}}
    public string Identity {get{return String.IsNullOrEmpty(Repository)?SourceUrl:Repository;}}
    public string ProjectUrl {get{return String.IsNullOrEmpty(SourceUrl)?"https://github.com/"+Repository:SourceUrl;}}

    // What the project is (Port, Recompilação...). Stored in ports.json and validated against ProjectTypes.
    public string ProjectType {get;set;}
    // File name pattern shown in the import dialog of profiles distributed outside GitHub (ManualPackage).
    public string ManualPackageFile {get;set;}
    // Oldest NEXUS version able to install this profile. Newer profiles are hidden from older launchers.
    public string MinNexusVersion {get;set;}

    // How the project is installed, derived from the profile flags so it never disagrees with the installer.
    public string InstallMode {
      get {
        if (ProjectOnly) return String.Equals(Kind, "Em desenvolvimento", StringComparison.OrdinalIgnoreCase) ? InstallModes.InDevelopment : InstallModes.Manual;
        if (ManualPackage) return InstallModes.OfficialSite;
        if (UsesInstaller) return InstallModes.Wizard;
        return InstallModes.Integrated;
      }
    }

    public override string ToString(){return Name;}
  }
  public static class ProjectTypes {
    public const string Port = "Port", Recompilation = "Recompilação", Decompilation = "Decompilação", Reimplementation = "Reimplementação", Compatibility = "Compatibilidade";
    public static readonly string[] All = { Port, Recompilation, Decompilation, Reimplementation, Compatibility };
  }
  public static class InstallModes {
    public const string Integrated = "Integrada", Wizard = "Assistente", OfficialSite = "Site oficial", Manual = "Manual", InDevelopment = "Em desenvolvimento";
    public static readonly string[] All = { Integrated, Wizard, OfficialSite, Manual, InDevelopment };
    public static bool Automatic(string mode) { return mode == Integrated || mode == Wizard || mode == OfficialSite; }
  }
  public class CatalogQuery {
    public string Platform;
    public string Search;
    public readonly HashSet<string> ProjectTypes = new HashSet<string>();
    public readonly HashSet<string> InstallModes = new HashSet<string>();
  }
  public class PortRelease {
    public string Version;public string ReleaseUrl;public string AssetName;public string DownloadUrl;public long Size;public string Digest;public bool FromCache;public bool IsPrerelease;public string CheckedUtc;
  }
  public class InstallProgress {public string Message;public double Percent;}
  public class PortCatalog:IDisposable {
    readonly HttpClient client;public List<PortEntry> Entries {get;private set;}
    readonly Dictionary<string,PortRelease> memo=new Dictionary<string,PortRelease>();
    readonly Dictionary<string,DateTime> memoTime=new Dictionary<string,DateTime>();
    List<Dictionary<string,object>> remoteReleases;
    public PortCatalog():this(new HttpClientHandler()){}
    public PortCatalog(HttpMessageHandler handler){
      Entries=LoadEntries().Where(Supported).ToList();ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;client=new HttpClient(handler);client.Timeout=Timeout.InfiniteTimeSpan;client.DefaultRequestHeaders.UserAgent.ParseAdd("NexusPortsPC/1.6 (https://github.com/teasergames/ports-pc)");
    }
    // Sites trusted for profiles distributed outside GitHub Releases. A remote catalog cannot add new ones.
    public static readonly string[] TrustedManualSources = { "https://almost-there-games.itch.io/azimuth" };
    static readonly string[] PackageKinds = { null, "Zip", "Installer", "Executable", "InstallerZip" };
    public const string RemoteCatalogUrl = "https://raw.githubusercontent.com/teasergames/ports-pc/main/assets/ports.json";
    public const string RemoteReleasesCacheUrl = "https://raw.githubusercontent.com/teasergames/ports-pc/catalog-data/releases-cache.json";
    const int RemoteLimit = 4 * 1024 * 1024;

    static List<PortEntry> embedded;
    public static List<PortEntry> LoadEntries(){
      if (embedded == null) {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.ports.json"))
        using (var reader = new StreamReader(stream)) embedded = Parse(reader.ReadToEnd());
      }
      // Callers may keep or mutate the list, so every call gets its own copy of the validated profiles.
      return embedded.ToList();
    }

    // Validates a complete catalog. Any invalid profile rejects the whole document, so a broken remote
    // catalog can never partially replace the profiles bundled with this NEXUS version.
    public static List<PortEntry> Parse(string json){
      var entries = new JavaScriptSerializer { MaxJsonLength = RemoteLimit }.Deserialize<List<PortEntry>>(json);
      if (entries == null || entries.Count == 0) throw new InvalidDataException("Catálogo vazio.");
      var ids = new HashSet<string>();
      foreach (var e in entries) {
        if (e == null || !Regex.IsMatch(e.Id ?? "", @"^[a-z0-9-]+$") || !ids.Add(e.Id) || String.IsNullOrWhiteSpace(e.Name)
            || (!e.ManualPackage && !Regex.IsMatch(e.Repository ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) || e.PlayNames == null || e.ConfigNames == null)
          throw new InvalidDataException("Entrada do catálogo inválida.");
        if (e.ManualPackage && (e.ProjectOnly || !TrustedManualSources.Contains(e.SourceUrl) || String.IsNullOrEmpty(e.ManualPackageFile)))
          throw new InvalidDataException("Fonte externa inválida.");
        if (!Platforms.All.Skip(1).Contains(e.Platform)) throw new InvalidDataException("Plataforma inválida.");
        if (!ProjectTypes.All.Contains(e.ProjectType)) throw new InvalidDataException("Tipo de projeto inválido: " + e.Id);
        if (!PackageKinds.Contains(e.PackageKind)) throw new InvalidDataException("Formato de pacote desconhecido: " + e.Id);
        if (e.MinNexusVersion != null) SelfUpdater.StableVersion(e.MinNexusVersion);
        if (!e.ProjectOnly) {
          if ((e.PlayNames.Length == 0 && !e.DiscoverExecutable && !e.UsesInstaller) || String.IsNullOrEmpty(e.AssetPattern)
              || !(e.AssetPattern.EndsWith("zip$") || ((e.PackageKind == "Executable" || e.PackageKind == "Installer") && e.AssetPattern.EndsWith("exe$"))))
            throw new InvalidDataException("Perfil Windows inválido.");
          new Regex(e.AssetPattern);
        }
      }
      return entries;
    }

    // Profiles that need a newer launcher stay hidden until NEXUS itself is updated.
    public static bool Supported(PortEntry entry){
      return entry.MinNexusVersion == null || SelfUpdater.StableVersion(entry.MinNexusVersion) <= SelfUpdater.StableVersion(SelfUpdater.CurrentVersion);
    }

    // Downloads the catalog published on the main branch (raw.githubusercontent.com does not count
    // against the GitHub API limit). Falls back to the last valid copy saved in the data folder.
    public async Task<bool> RefreshFromRemote(string dataRoot, CancellationToken token){
      string cacheFile = Path.Combine(dataRoot, "catalog-cache.json");
      List<PortEntry> remote = null;
      try {
        string json = await FetchText(RemoteCatalogUrl, token);
        remote = Parse(json);
        File.WriteAllText(cacheFile, json, System.Text.Encoding.UTF8);
      } catch (OperationCanceledException) { if (token.IsCancellationRequested) throw; }
      catch (Exception) { }
      if (remote == null && File.Exists(cacheFile)) {
        try { remote = Parse(File.ReadAllText(cacheFile, System.Text.Encoding.UTF8)); } catch (Exception) { }
      }
      try {
        string releases = await FetchText(RemoteReleasesCacheUrl, token);
        remoteReleases = ReleaseRecords(releases);
      } catch (OperationCanceledException) { if (token.IsCancellationRequested) throw; }
      catch (Exception) { }
      if (remote == null) return false;
      var visible = remote.Where(Supported).ToList();
      if (visible.Count < Entries.Count / 2) return false;
      Entries = visible;
      return true;
    }

    async Task<string> FetchText(string url, CancellationToken token){
      using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)) {
          response.EnsureSuccessStatusCode();
          using (var stream = await response.Content.ReadAsStreamAsync())
          using (var bytes = new MemoryStream()) {
            byte[] buffer = new byte[16384]; int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0) {
              if (bytes.Length + read > RemoteLimit) throw new InvalidDataException("Resposta muito grande.");
              bytes.Write(buffer, 0, read);
            }
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
          }
        }
      }
    }

    // Kept for existing callers: mode 1 = automatic installation, mode 2 = manual or in development.
    public static List<PortEntry> Filter(IEnumerable<PortEntry> entries,string platform,string search,int installationMode){
      var query = new CatalogQuery { Platform = platform, Search = search };
      if (installationMode == 1) query.InstallModes.UnionWith(InstallModes.All.Where(InstallModes.Automatic));
      if (installationMode == 2) query.InstallModes.UnionWith(InstallModes.All.Where(m => !InstallModes.Automatic(m)));
      return Filter(entries, query);
    }
    public static List<PortEntry> Filter(IEnumerable<PortEntry> entries, CatalogQuery query){
      string search = (query.Search ?? "").Trim();
      return entries.Where(e =>
          (String.IsNullOrEmpty(query.Platform) || query.Platform == Platforms.All[0] || e.Platform == query.Platform)
          && (query.ProjectTypes.Count == 0 || query.ProjectTypes.Contains(e.ProjectType))
          && (query.InstallModes.Count == 0 || query.InstallModes.Contains(e.InstallMode))
          && (search.Length == 0 || (e.Name + " " + e.ArtworkTitle + " " + e.Identity + " " + e.Platform + " " + e.ProjectType).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0))
        .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    public async Task<PortRelease> Latest(PortEntry entry,CancellationToken token,bool force=false){
      token.ThrowIfCancellationRequested();if(entry.ManualPackage)throw new InvalidDataException("Baixe o ZIP na página oficial e use Importar ZIP no catálogo.");if(entry.ProjectOnly)throw new InvalidDataException("Este projeto não tem instalação automática validada. Abra o projeto para instruções.");PortRelease previous;DateTime checkedAt;
      if(!force&&memo.TryGetValue(entry.Repository,out previous)&&memoTime.TryGetValue(entry.Repository,out checkedAt)&&(DateTime.UtcNow-checkedAt).TotalMinutes<10)return previous;
      try{var current=await FetchLatest(entry,token);current.CheckedUtc=DateTime.UtcNow.ToString("o");memo[entry.Repository]=current;memoTime[entry.Repository]=DateTime.UtcNow;return current;}
      catch(HttpRequestException){var cached=CachedRelease(entry);if(cached==null)throw;memo[entry.Repository]=cached;memoTime[entry.Repository]=DateTime.UtcNow;return cached;}
      catch(OperationCanceledException){if(token.IsCancellationRequested)throw;var cached=CachedRelease(entry);if(cached==null)throw;memo[entry.Repository]=cached;memoTime[entry.Repository]=DateTime.UtcNow;return cached;}
    }
    // Prefers the cache refreshed daily on GitHub; the copy bundled in the executable is the last resort.
    PortRelease CachedRelease(PortEntry entry){
      if (remoteReleases != null) {
        try { var fresh = FromRecords(entry, remoteReleases); if (fresh != null) return fresh; } catch (InvalidDataException) { }
      }
      return BundledRelease(entry);
    }
    // Last release known without calling the GitHub API (daily cache, then the bundled copy). Marked FromCache.
    public PortRelease KnownRelease(PortEntry entry){try{return CachedRelease(entry);}catch(InvalidDataException){return null;}}
    static List<Dictionary<string,object>> bundledReleases;
    public static PortRelease BundledRelease(PortEntry entry){
      if (bundledReleases == null) {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nexus.releases-cache.json"))
        using (var reader = new StreamReader(stream)) bundledReleases = ReleaseRecords(reader.ReadToEnd());
      }
      return FromRecords(entry, bundledReleases);
    }
    static List<Dictionary<string,object>> ReleaseRecords(string json){
      return new JavaScriptSerializer{MaxJsonLength=RemoteLimit}.Deserialize<List<Dictionary<string,object>>>(json) ?? new List<Dictionary<string,object>>();
    }
    static PortRelease FromRecords(PortEntry entry, List<Dictionary<string,object>> records){
      foreach(var record in records)if(record!=null&&record.ContainsKey("Repository")&&Convert.ToString(record["Repository"])==entry.Repository){var release=ParseRelease(entry,new JavaScriptSerializer().Serialize(record["Release"]));release.FromCache=true;release.CheckedUtc=Convert.ToString(record["CheckedUtc"]);return release;}return null;
    }
    async Task<PortRelease> FetchLatest(PortEntry entry,CancellationToken token){
      using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using(var response=await client.GetAsync("https://api.github.com/repos/"+entry.Repository+(entry.AllowPrerelease||entry.DiscoverExecutable?"/releases?per_page=30":"/releases/latest"),HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
          if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidDataException("Este repositório não publicou uma release estável acessível.");
          if(response.StatusCode==HttpStatusCode.Forbidden||(int)response.StatusCode==429)throw new HttpRequestException("O GitHub limitou as consultas. Aguarde e tente novamente.");response.EnsureSuccessStatusCode();
          using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream()){
            byte[] buffer=new byte[16384];int read;while((read=await stream.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0){if(bytes.Length+read>4*1024*1024)throw new InvalidDataException("Resposta do GitHub muito grande.");bytes.Write(buffer,0,read);}
            string json=System.Text.Encoding.UTF8.GetString(bytes.ToArray());return entry.AllowPrerelease||entry.DiscoverExecutable?ParseReleaseList(entry,json):ParseRelease(entry,json);
          }
        }
      }
    }
    public static PortRelease ParseReleaseList(PortEntry entry,string json){
      var records=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<List<Dictionary<string,object>>>(json);
      foreach(var record in records){if(record.ContainsKey("draft")&&Convert.ToBoolean(record["draft"]))continue;if(record.ContainsKey("prerelease")&&Convert.ToBoolean(record["prerelease"])&&!entry.AllowPrerelease)continue;if(entry.DiscoverExecutable){object value;var assets=record.TryGetValue("assets",out value)?value as System.Collections.IEnumerable:null;bool match=false;if(assets!=null)foreach(var asset in assets){var item=asset as Dictionary<string,object>;if(item!=null&&Regex.IsMatch(Convert.ToString(item["name"]),entry.AssetPattern,RegexOptions.IgnoreCase)){match=true;break;}}if(!match)continue;}return ParseRelease(entry,new JavaScriptSerializer().Serialize(record));}
      throw new InvalidDataException("Nenhuma release compatível publicada.");
    }
    public static PortRelease ParseRelease(PortEntry entry,string json){
      var data=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,object>>(json);
      if(data.ContainsKey("draft")&&Convert.ToBoolean(data["draft"])||data.ContainsKey("prerelease")&&Convert.ToBoolean(data["prerelease"])&&!entry.AllowPrerelease)throw new InvalidDataException("A release não é estável.");
      object value;var assets=data.TryGetValue("assets",out value)?value as System.Collections.IEnumerable:null;var found=new List<Dictionary<string,object>>();
      if(assets!=null)foreach(object asset in assets){var a=asset as Dictionary<string,object>;if(a!=null&&Regex.IsMatch(Convert.ToString(a["name"]),entry.AssetPattern,RegexOptions.IgnoreCase))found.Add(a);}
      if(found.Count!=1)throw new InvalidDataException(found.Count==0?"Nenhuma release consultada contém o pacote Windows esperado. O autor pode ter alterado o formato.":"Mais de um pacote corresponde ao perfil. A seleção precisa ser revisada.");
      var chosen=found[0];string url=Convert.ToString(chosen["browser_download_url"]);ValidateDownloadUrl(entry,url);
      long size=Convert.ToInt64(chosen["size"]);if(size<=0||size>2L*1024*1024*1024)throw new InvalidDataException("Tamanho de pacote inválido ou acima de 2 GB.");
      return new PortRelease{IsPrerelease=data.ContainsKey("prerelease")&&Convert.ToBoolean(data["prerelease"]),Version=Convert.ToString(data["tag_name"]),ReleaseUrl=Convert.ToString(data["html_url"]),AssetName=Convert.ToString(chosen["name"]),DownloadUrl=url,Size=size,Digest=chosen.TryGetValue("digest",out value)?Convert.ToString(value):null};
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
    public async Task<Game> Install(PortEntry entry,PortRelease release,string parent,IProgress<InstallProgress> progress,CancellationToken token,bool acceptPrerelease=false,InstallInteraction interaction=null){
      if(interaction!=null&&interaction.UpdateInPlace){var previous=interaction.Previous;if(previous==null||previous.Repository!=entry.Identity||!UpdateManager.Managed(previous))throw new InvalidDataException("Selecione uma instalação gerenciada deste port para atualizar.");if(release.FromCache||!UpdateManager.Available(previous,release))throw new InvalidDataException("Confirme online uma versão mais recente antes de atualizar.");if((previous.InstallationKind=="Installer")!=(entry.UsesInstaller))throw new InvalidDataException("O formato do pacote mudou. Instale a nova versão separadamente para preservar a cópia anterior.");}
      if(release.IsPrerelease&&(!entry.AllowPrerelease||!acceptPrerelease))throw new InvalidDataException("Confirme que deseja instalar a versão beta.");if(entry.ProjectOnly||entry.ManualPackage)throw new InvalidDataException("Instalação automática indisponível para este projeto.");ValidateDownloadUrl(entry,release.DownloadUrl);string root=Path.GetFullPath(parent);Directory.CreateDirectory(FsPath(root));
      string final=ChildPath(root,entry.Id+"-"+VersionFolder(release.Version));if(!entry.UsesInstaller&&(interaction==null||!interaction.UpdateInPlace)&&(Directory.Exists(FsPath(final))||File.Exists(FsPath(final))))throw new IOException("Esta pasta de versão já existe. Escolha outro destino; nenhum arquivo foi substituído.");
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
        return await FinishInstallation(entry,release,root,staging,download,progress,token,interaction);
      }finally{
        if(File.Exists(FsPath(download)))File.Delete(FsPath(download));
        if(Directory.Exists(FsPath(staging)))RemoveStaging(root,staging);
      }
    }
    async Task<Game> FinishInstallation(PortEntry entry,PortRelease release,string root,string staging,string download,IProgress<InstallProgress> progress,CancellationToken token,InstallInteraction interaction=null){
      bool updating=interaction!=null&&interaction.UpdateInPlace;string final=ChildPath(root,entry.Id+"-"+VersionFolder(release.Version)+(entry.UsesInstaller?"-"+Guid.NewGuid().ToString("N"):""));if(!updating&&(Directory.Exists(FsPath(final))||File.Exists(FsPath(final))))throw new IOException("Esta pasta de versão já existe. Nenhum arquivo foi substituído.");
      if(entry.PackageKind=="Executable"||entry.PackageKind=="Installer"){
        string executable=ChildPath(staging,Path.GetFileName(release.AssetName));File.Copy(FsPath(download),FsPath(executable));if(!PackageSupport.WindowsExecutable(executable))throw new InvalidDataException("O download não é um executável Windows x64/x86 compatível.");
      }else await Task.Run(()=>Extract(download,staging,progress,token),token);
      if(entry.UsesInstaller){
        if(interaction==null||interaction.RunInstaller==null)throw new InvalidDataException("Este pacote precisa do assistente de instalação do autor.");
        string installerFile=entry.PackageKind=="InstallerZip"?FindExecutable(staging,entry.PlayNames,entry.PreferRootExecutable):ChildPath(staging,Path.GetFileName(release.AssetName));if(installerFile==null||!PackageSupport.WindowsExecutable(installerFile))throw new InvalidDataException("O instalador Windows esperado não foi encontrado no pacote.");string installerRelative=PackageSupport.Relative(staging,installerFile);
        UpdateManager.WriteReceipt(entry,release,staging,token);token.ThrowIfCancellationRequested();Directory.Move(FsPath(staging),FsPath(final));string installer=ChildPath(final,installerRelative);Report(progress,"Conclua o assistente do autor e escolha o aplicativo instalado no NEXUS.",95);
        var choice=await interaction.RunInstaller(installer,interaction.Previous);if(choice==null)throw new OperationCanceledException();if(!Path.IsPathRooted(choice.Play)||!File.Exists(choice.Play)||!PackageSupport.WindowsExecutable(choice.Play)||(!String.IsNullOrEmpty(choice.Configuration)&&(!Path.IsPathRooted(choice.Configuration)||!File.Exists(choice.Configuration)||!PackageSupport.WindowsExecutable(choice.Configuration))))throw new InvalidDataException("Escolha o aplicativo Windows instalado pelo assistente.");
        var installed=new Game{Name=entry.Name,Platform=entry.Platform,Repository=entry.Identity,InstalledVersion=release.Version,Executable=choice.Play,WorkingDirectory=Path.GetDirectoryName(choice.Play),Arguments=choice.Arguments??"",ConfigurationExecutable=choice.Configuration,ConfigurationArguments=choice.ConfigurationArguments,ArtworkSearchTitle=entry.ArtworkTitle,InstallationKind="Installer",InstallerCacheRoot=final};if(interaction.Previous!=null)PackageSupport.PreserveMetadata(installed,interaction.Previous);return installed;
      }
      Game game=entry.DiscoverExecutable?PackageSupport.Register(entry,staging,release.Version,interaction):Register(entry,staging,release.Version);token.ThrowIfCancellationRequested();
      game.InstallationKind=entry.PackageKind??"Zip";UpdateManager.WriteReceipt(entry,release,staging,token);
      if(updating)return await Task.Run(()=>UpdateManager.Commit(entry,staging,game,interaction.Previous,progress,token),token);
      // Paths are rewritten only after the complete package has been validated.
      string exe=game.Executable.Substring(staging.Length),config=String.IsNullOrEmpty(game.ConfigurationExecutable)?null:game.ConfigurationExecutable.Substring(staging.Length);
      Directory.Move(FsPath(staging),FsPath(final));game.Executable=final+exe;game.ConfigurationExecutable=config==null?null:final+config;game.WorkingDirectory=Path.GetDirectoryName(game.Executable);game.ManagedInstallationRoot=final;
      Report(progress,"Instalado e pronto para adicionar à biblioteca.",100);return game;
    }
    public async Task<Game> ImportZip(PortEntry entry,string source,string parent,IProgress<InstallProgress> progress,CancellationToken token){
      if(!entry.ManualPackage||entry.ProjectOnly)throw new InvalidDataException("Este perfil não aceita importação de ZIP.");
      if(!Regex.IsMatch(Path.GetFileName(source),entry.AssetPattern,RegexOptions.IgnoreCase))throw new InvalidDataException("Selecione o ZIP oficial "+entry.ManualPackageFile+".");
      string root=Path.GetFullPath(parent);Directory.CreateDirectory(FsPath(root));string staging=ChildPath(root,".nexus-install-"+Guid.NewGuid().ToString("N")),snapshot=staging+".zip";Directory.CreateDirectory(FsPath(staging));
      try{
        using(var input=new FileStream(FsPath(source),FileMode.Open,FileAccess.Read,FileShare.Read)){
          if(input.Length<=0||input.Length>2L*1024*1024*1024)throw new InvalidDataException("Tamanho de ZIP inválido.");
          using(var output=new FileStream(FsPath(snapshot),FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){byte[] buffer=new byte[65536];int read;while((read=await input.ReadAsync(buffer,0,buffer.Length,token))>0)await output.WriteAsync(buffer,0,read,token);}
        }
        string digest;using(var input=File.OpenRead(FsPath(snapshot)))using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        var release=new PortRelease{Version="zip-"+digest.Substring(0,12),AssetName=Path.GetFileName(source),Digest="sha256:"+digest,Size=new FileInfo(FsPath(snapshot)).Length,ReleaseUrl=entry.ProjectUrl};
        string final=ChildPath(root,entry.Id+"-"+VersionFolder(release.Version));if(Directory.Exists(FsPath(final))||File.Exists(FsPath(final)))throw new IOException("Este ZIP já foi importado neste destino. Nenhum arquivo foi substituído.");
        return await FinishInstallation(entry,release,root,staging,snapshot,progress,token);
      }finally{if(File.Exists(FsPath(snapshot)))File.Delete(FsPath(snapshot));if(Directory.Exists(FsPath(staging)))RemoveStaging(root,staging);}
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
      if(entry.ProjectOnly||entry.UsesInstaller)throw new InvalidDataException("Este pacote precisa de preparação específica.");
      if(entry.DiscoverExecutable)return PackageSupport.Register(entry,folder,version,null);
      string exe=FindExecutable(folder,entry.PlayNames,entry.PreferRootExecutable),config=FindExecutable(folder,entry.ConfigNames,entry.PreferRootExecutable);
      if(entry.ProjectOnly||exe==null||(entry.ConfigNames.Length>0&&config==null))throw new InvalidDataException("A estrutura deste pacote mudou: executáveis esperados não encontrados. Nada foi cadastrado; confira o projeto.");
      return new Game{Name=entry.Name,Platform=entry.Platform,Executable=exe,Arguments=entry.Arguments??"",WorkingDirectory=Path.GetDirectoryName(exe),ConfigurationExecutable=config,ConfigurationArguments=entry.ConfigurationArguments,ArtworkSearchTitle=entry.ArtworkTitle,Repository=entry.Identity,InstalledVersion=version,ManagedInstallationRoot=folder};
    }
    static string FindExecutable(string root,string[] names,bool preferRoot=false){
      // Packages may have one or several enclosing folders. Never pick a runtime by extension alone.
      var files=Directory.GetFiles(FsPath(root),"*.exe",SearchOption.AllDirectories).Select(DisplayPath).ToArray();foreach(string name in names){var matches=files.Where(f=>Path.GetFileName(f).Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length==1)return matches[0];if(matches.Length>1&&preferRoot){int depth=matches.Min(f=>PackageSupport.Relative(root,f).Count(c=>c==Path.DirectorySeparatorChar));matches=matches.Where(f=>PackageSupport.Relative(root,f).Count(c=>c==Path.DirectorySeparatorChar)==depth).ToArray();if(matches.Length==1)return matches[0];}if(matches.Length>1)throw new InvalidDataException("Executável duplicado no pacote: "+name);}return null;
    }
    static void RemoveStaging(string root,string staging){string safe=ChildPath(root,Path.GetFileName(staging));if(safe.Equals(Path.GetFullPath(staging),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(safe).StartsWith(".nexus-install-",StringComparison.Ordinal))Directory.Delete(FsPath(safe),true);}
    static void Report(IProgress<InstallProgress> progress,string message,double percent){if(progress!=null)progress.Report(new InstallProgress{Message=message,Percent=percent});}
    public void Dispose(){client.Dispose();}
  }
}
