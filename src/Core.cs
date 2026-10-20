using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace Nexus {
  public class Game {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Executable { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public string ConfigurationExecutable { get; set; }
    public string ConfigurationArguments { get; set; }
    public string ArtworkSearchTitle { get; set; }
    public string Artwork { get; set; }
    public string ArtworkSource { get; set; }
    public bool Favorite { get; set; }
    public string LastPlayed { get; set; }
    public string Platform {get;set;}
    public string Repository {get;set;}
    public string InstalledVersion {get;set;}
    public string ManagedInstallationRoot {get;set;}
    public Game() { Id=Guid.NewGuid().ToString("N"); Arguments=""; }
    public Game Copy() { return (Game)MemberwiseClone(); }
  }
  public class Library {
    public int Version {get;set;}
    public List<Game> Games {get;set;}
    public bool OnlineArtwork {get;set;}
    public bool MinimizeOnLaunch {get;set;}
    public string ProtectedApiKey {get;set;}
    public string ViewMode {get;set;}
    public int GridSize {get;set;}
    public int ListSize {get;set;}
    public Library() {Version=1;Games=new List<Game>();OnlineArtwork=true;ViewMode="grid";GridSize=220;ListSize=76;}
    public void NormalizeView(){ViewMode=ViewMode=="list"?"list":"grid";GridSize=Math.Max(140,Math.Min(340,GridSize<=0?220:GridSize));ListSize=Math.Max(48,Math.Min(132,ListSize<=0?76:ListSize));}
    public string ApiKey() {
      if(String.IsNullOrEmpty(ProtectedApiKey))return "";
      try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedApiKey),null,DataProtectionScope.CurrentUser));}catch{return "";}
    }
    public void SetApiKey(string key) { ProtectedApiKey=String.IsNullOrWhiteSpace(key)?null:Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()),null,DataProtectionScope.CurrentUser)); }
  }
  public class Store {
    public readonly string Root;
    public string FilePath {get{return Path.Combine(Root,"library.json");}}
    public string Cache {get{return Path.Combine(Root,"artwork");}}
    public Store(string root) { Root=root;Directory.CreateDirectory(root);Directory.CreateDirectory(Cache); }
    public Library Load() {
      if(!File.Exists(FilePath))return new Library();
      try{return Parse(File.ReadAllText(FilePath,Encoding.UTF8));}
      catch(Exception ex){
        Log("Biblioteca inválida: "+ex.Message);
        if(File.Exists(FilePath+".bak")){try{Library backup=Parse(File.ReadAllText(FilePath+".bak",Encoding.UTF8));File.Copy(FilePath,FilePath+".corrupt-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true);return backup;}catch{}}
        throw new InvalidDataException("Não foi possível ler sua biblioteca. Seus arquivos foram preservados em "+Root,ex);
      }
    }
    public static Library Parse(string json) {
      Library lib=new JavaScriptSerializer().Deserialize<Library>(json);
      if(lib==null||lib.Version!=1||lib.Games==null)throw new InvalidDataException("Formato de biblioteca não reconhecido.");
      lib.NormalizeView();
      foreach(var game in lib.Games)if(game!=null)game.Platform=Platforms.Resolve(game);
      HashSet<string> ids=new HashSet<string>();
      foreach(Game g in lib.Games){if(g==null||String.IsNullOrWhiteSpace(g.Id)||!ids.Add(g.Id)||String.IsNullOrWhiteSpace(g.Name)||String.IsNullOrWhiteSpace(g.Executable))throw new InvalidDataException("Cadastro de jogo inválido.");}
      return lib;
    }
    public void Save(Library lib) {
      string json=new JavaScriptSerializer().Serialize(lib);Parse(json);
      string temp=FilePath+".tmp";File.WriteAllText(temp,json,Encoding.UTF8);
      if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak");else File.Move(temp,FilePath);
    }
    public void Log(string message){try{string p=Path.Combine(Root,"nexus.log");if(File.Exists(p)&&new FileInfo(p).Length>1000000)File.Delete(p);File.AppendAllText(p,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);}catch{}}
    public string ImportArtwork(string file,string id) {
      if(!File.Exists(file))throw new FileNotFoundException("Imagem não encontrada.",file);
      using(var image=System.Drawing.Image.FromFile(file)){
        if(image.Width>16000||image.Height>16000)throw new InvalidDataException("Imagem muito grande.");
        string target=Path.Combine(Cache,id+"-"+Guid.NewGuid().ToString("N")+".jpg");
        int width=Math.Min(1920,image.Width),height=Math.Max(1,(int)(image.Height*(double)width/image.Width));
        using(var scaled=new System.Drawing.Bitmap(width,height))using(var gr=System.Drawing.Graphics.FromImage(scaled)){
          gr.Clear(System.Drawing.Color.FromArgb(16,17,21));gr.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;gr.DrawImage(image,0,0,width,height);scaled.Save(target,System.Drawing.Imaging.ImageFormat.Jpeg);
        }return target;
      }
    }
  }
  public static class Ports {
    public static readonly string[] Extensions={".exe",".lnk"};
    public static bool Allowed(string file){return !String.IsNullOrWhiteSpace(file)&&Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());}
    public static Game FromPath(string executable) {
      string full=Path.GetFullPath(executable);string dir=Path.GetDirectoryName(full);string name=Path.GetFileNameWithoutExtension(full);
      string marker=(name+" "+dir).ToLowerInvariant();Game g=new Game{Executable=full,WorkingDirectory=dir,Name=name};
      if(marker.Contains("uncharted")){
        g.Name="Uncharted: The Nathan Drake Collection";
      }else if(marker.Contains("gow3")||marker.Contains("god of war iii")){
        g.Name="God of War III";string direct=Path.Combine(dir,"Play God of War III.exe");if(File.Exists(direct))g.Executable=direct;
      }else if(marker.Contains("sotc")||marker.Contains("shadow of the colossus")){
        g.Name="Shadow of the Colossus";if(Path.GetExtension(full).Equals(".exe",StringComparison.OrdinalIgnoreCase))g.Arguments="--play";
      }else if(marker.Contains("bloodborne")||marker.Contains("bbport")){
        g.Name="Bloodborne";string direct=Path.Combine(dir,"Bloodborne.exe");if(File.Exists(direct))g.Executable=direct;
      }
      g.ConfigurationExecutable=FindConfigurationExecutable(g);
      foreach(var entry in PortCatalog.LoadEntries().Where(e=>!e.ProjectOnly))if(entry.PlayNames.Any(n=>Path.GetFileName(full).Equals(n,StringComparison.OrdinalIgnoreCase))){if(g.Name==name)g.Name=entry.Name;g.Platform=entry.Platform;g.ArtworkSearchTitle=entry.ArtworkTitle;string cfg=entry.ConfigNames.Select(n=>Path.Combine(dir,n)).FirstOrDefault(File.Exists);g.ConfigurationExecutable=cfg;g.Repository=entry.Identity;g.Arguments=entry.Arguments??"";break;}g.Platform=Platforms.Resolve(g);return g;
    }
    public static List<Game> Detect(string downloads) {
      List<Game> result=new List<Game>();if(!Directory.Exists(downloads))return result;
      string[] relative={@"gow3-windows\gow3-windows\Play God of War III.exe",@"gow3-windows\Play God of War III.exe",@"SotC-PC\SotC.exe",@"BBPORT\Bloodborne.exe",@"Uncharted PC port Bila\Uncharted Launcher.exe"};
      foreach(string rel in relative){string p=Path.Combine(downloads,rel);if(File.Exists(p))result.Add(FromPath(p));}return result;
    }
    public static ProcessStartInfo StartInfo(Game game,bool direct=true) {
      if(!Allowed(game.Executable))throw new InvalidDataException("Selecione um executável .exe ou atalho .lnk.");
      if(!File.Exists(game.Executable))throw new FileNotFoundException("O executável foi movido ou removido. Atualize o caminho em Editar cadastro.",game.Executable);
      string dir=String.IsNullOrWhiteSpace(game.WorkingDirectory)?Path.GetDirectoryName(game.Executable):game.WorkingDirectory;
      if(!Directory.Exists(dir))throw new DirectoryNotFoundException("A pasta de trabalho não existe: "+dir);
      if(direct&&Path.GetFileName(game.Executable).Equals("Uncharted Launcher.exe",StringComparison.OrdinalIgnoreCase)&&String.IsNullOrWhiteSpace(game.Arguments))return Uncharted.StartInfo(game,ReadPortConfig("Uncharted: The Nathan Drake Collection",Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
      return new ProcessStartInfo{FileName=Path.GetFullPath(game.Executable),Arguments=game.Arguments??"",WorkingDirectory=Path.GetFullPath(dir),UseShellExecute=true};
    }
    public static string CanonicalTitle(Game game){
      if(!String.IsNullOrWhiteSpace(game.ArtworkSearchTitle))return game.ArtworkSearchTitle.Trim();
      return PortTitle(game);
    }
    public static string PortTitle(Game game){
      string marker=(game.Name+" "+Path.GetFileName(game.Executable)+" "+Path.GetDirectoryName(game.Executable)).ToLowerInvariant();
      if(marker.Contains("uncharted"))return "Uncharted: The Nathan Drake Collection";
      if(marker.Contains("bloodborne")||marker.Contains("bbport"))return "Bloodborne";
      if(marker.Contains("sotc")||marker.Contains("shadow of the colossus"))return "Shadow of the Colossus";
      if(marker.Contains("gow3")||marker.Contains("god of war iii"))return "God of War III";
      return game.Name;
    }
    public static string FindConfigurationExecutable(Game game){
      if(!String.IsNullOrWhiteSpace(game.ConfigurationExecutable))return game.ConfigurationExecutable;
      string root=Path.GetDirectoryName(game.Executable);string title=PortTitle(game);
      string name=title=="God of War III"?"God of War III.exe":title=="Bloodborne"?"Bloodborne Launcher.exe":title=="Shadow of the Colossus"?"SotC.exe":title=="Uncharted: The Nathan Drake Collection"?"Uncharted Launcher.exe":null;
      string candidate=name==null?null:Path.Combine(root,name);return candidate!=null&&File.Exists(candidate)?candidate:null;
    }
    public static ProcessStartInfo ConfigurationStartInfo(Game game){
      string file=FindConfigurationExecutable(game);
      if(String.IsNullOrWhiteSpace(file))throw new InvalidDataException("Escolha o launcher de configuração em Editar cadastro.");
      Game config=game.Copy();config.Executable=file;config.Arguments=game.ConfigurationArguments??"";config.WorkingDirectory=Path.GetDirectoryName(file);
      if(!Allowed(file)||!File.Exists(file))throw new FileNotFoundException("O launcher de configuração não foi encontrado. Atualize-o em Editar cadastro.",file);
      return new ProcessStartInfo{FileName=Path.GetFullPath(file),Arguments=config.Arguments,WorkingDirectory=Path.GetFullPath(config.WorkingDirectory),UseShellExecute=true};
    }
    public static string FindLocalArtwork(Game game) {
      string root=Path.GetDirectoryName(game.Executable);
      string[] candidates={@"artwork.jpg",@"artwork.png",@"background.jpg",@"background.png",@"hero.jpg",@"hero.png",@"sce_sys\pic1.png",@"sce_sys\pic0.png",@"assets\background.jpg",@"launcher\background.jpg"};
      foreach(string rel in candidates){string p=Path.Combine(root,rel);if(File.Exists(p))return p;}
      string configured=ConfiguredGameDirectory(PortTitle(game),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
      if(!String.IsNullOrEmpty(configured))foreach(string rel in new[]{@"sce_sys\pic1.png",@"sce_sys\pic0.png",@"sce_sys\icon0.png"}){string p=Path.Combine(configured,rel);if(File.Exists(p))return p;}
      return null;
    }
    public static string ConfiguredGameDirectory(string name,string appData) {
      try{var config=ReadPortConfig(name,appData);object value;
        return config.TryGetValue("game_dir",out value)&&value is string&&Directory.Exists((string)value)?(string)value:null;
      }catch{return null;}
    }
    public static Dictionary<string,object> ReadPortConfig(string name,string appData){
      string folder=name=="God of War III"?"gow3-launcher":name=="Shadow of the Colossus"?"sotc-launcher":name=="Bloodborne"?"bbport-launcher":name=="Uncharted: The Nathan Drake Collection"?"Uncharted Launcher":null;
      if(folder==null)return new Dictionary<string,object>();
      string file=Path.Combine(appData,folder,folder=="Uncharted Launcher"?"launcher.json":"settings.json");
      if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)return new Dictionary<string,object>();
      try{return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file))??new Dictionary<string,object>();}catch{return new Dictionary<string,object>();}
    }
    public static string Normalize(string value){return new String(value.ToLowerInvariant().Where(Char.IsLetterOrDigit).ToArray());}
  }
}
