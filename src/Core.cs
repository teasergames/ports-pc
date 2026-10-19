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
    public string Artwork { get; set; }
    public string ArtworkSource { get; set; }
    public bool Favorite { get; set; }
    public string LastPlayed { get; set; }
    public Game() { Id=Guid.NewGuid().ToString("N"); Arguments=""; }
    public Game Copy() { return (Game)MemberwiseClone(); }
  }
  public class Library {
    public int Version {get;set;}
    public List<Game> Games {get;set;}
    public bool OnlineArtwork {get;set;}
    public bool MinimizeOnLaunch {get;set;}
    public string ProtectedApiKey {get;set;}
    public Library() {Version=1;Games=new List<Game>();OnlineArtwork=true;}
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
      if(marker.Contains("gow3")||marker.Contains("god of war iii")){
        g.Name="God of War III";string direct=Path.Combine(dir,"Play God of War III.exe");if(File.Exists(direct))g.Executable=direct;
      }else if(marker.Contains("sotc")||marker.Contains("shadow of the colossus")){
        g.Name="Shadow of the Colossus";if(Path.GetExtension(full).Equals(".exe",StringComparison.OrdinalIgnoreCase))g.Arguments="--play";
      }else if(marker.Contains("bloodborne")||marker.Contains("bbport")){
        g.Name="Bloodborne";string direct=Path.Combine(dir,"Bloodborne.exe");if(File.Exists(direct))g.Executable=direct;
      }
      return g;
    }
    public static List<Game> Detect(string downloads) {
      List<Game> result=new List<Game>();if(!Directory.Exists(downloads))return result;
      string[] relative={@"gow3-windows\gow3-windows\Play God of War III.exe",@"gow3-windows\Play God of War III.exe",@"SotC-PC\SotC.exe",@"BBPORT\Bloodborne.exe"};
      foreach(string rel in relative){string p=Path.Combine(downloads,rel);if(File.Exists(p))result.Add(FromPath(p));}return result;
    }
    public static ProcessStartInfo StartInfo(Game game) {
      if(!Allowed(game.Executable))throw new InvalidDataException("Selecione um executável .exe ou atalho .lnk.");
      if(!File.Exists(game.Executable))throw new FileNotFoundException("O executável foi movido ou removido. Atualize o caminho em Configurar jogo.",game.Executable);
      string dir=String.IsNullOrWhiteSpace(game.WorkingDirectory)?Path.GetDirectoryName(game.Executable):game.WorkingDirectory;
      if(!Directory.Exists(dir))throw new DirectoryNotFoundException("A pasta de trabalho não existe: "+dir);
      return new ProcessStartInfo{FileName=Path.GetFullPath(game.Executable),Arguments=game.Arguments??"",WorkingDirectory=Path.GetFullPath(dir),UseShellExecute=true};
    }
    public static string FindLocalArtwork(Game game) {
      string root=Path.GetDirectoryName(game.Executable);
      string[] candidates={@"artwork.jpg",@"artwork.png",@"background.jpg",@"background.png",@"hero.jpg",@"hero.png",@"sce_sys\pic1.png",@"sce_sys\pic0.png",@"assets\background.jpg",@"launcher\background.jpg"};
      foreach(string rel in candidates){string p=Path.Combine(root,rel);if(File.Exists(p))return p;}
      string configured=ConfiguredGameDirectory(game.Name,Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
      if(!String.IsNullOrEmpty(configured))foreach(string rel in new[]{@"sce_sys\pic1.png",@"sce_sys\pic0.png",@"sce_sys\icon0.png"}){string p=Path.Combine(configured,rel);if(File.Exists(p))return p;}
      return null;
    }
    public static string ConfiguredGameDirectory(string name,string appData) {
      string folder=name=="God of War III"?"gow3-launcher":name=="Shadow of the Colossus"?"sotc-launcher":name=="Bloodborne"?"bbport-launcher":null;
      if(folder==null)return null;
      try{string file=Path.Combine(appData,folder,"settings.json");if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)return null;
        var config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));object value;
        return config.TryGetValue("game_dir",out value)&&value is string&&Directory.Exists((string)value)?(string)value:null;
      }catch{return null;}
    }
    public static string Normalize(string value){return new String(value.ToLowerInvariant().Where(Char.IsLetterOrDigit).ToArray());}
  }
}
