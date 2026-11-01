using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Nexus {
  public class ArtworkResult {public string File;public string Source;public string Message;}
  public class ArtworkService : IDisposable {
    readonly HttpClient client;
    readonly Store store;
    public ArtworkService(Store data) {
      store=data;ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
      client=new HttpClient();client.Timeout=TimeSpan.FromSeconds(18);client.DefaultRequestHeaders.UserAgent.ParseAdd("NexusPortsPC/1.0 (https://github.com/teasergames/ports-pc)");
    }
    public async Task<ArtworkResult> Resolve(Game game,bool online,string key,CancellationToken token) {
      string local=Ports.FindLocalArtwork(game);
      if(local!=null){try{return new ArtworkResult{File=store.ImportArtwork(local,game.Id),Source="Imagem local: "+Path.GetFileName(local)};}catch(Exception ex){store.Log("Imagem local: "+ex.Message);}}
      if(!online)return new ArtworkResult{Message="Busca online desativada. Escolha uma artwork em Editar cadastro."};
      if(!String.IsNullOrWhiteSpace(key)){
        try {ArtworkResult found=await SteamGrid(game,key,token);if(found!=null)return found;}
        catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
        catch(Exception ex){store.Log("SteamGridDB: "+ex.Message);}
      }
      try {
        var found=await Wikipedia(game,token);if(found!=null)return found;
      }catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
      catch(Exception ex){store.Log("Wikipedia: "+ex.Message);}
      return new ArtworkResult{Message="Nenhuma imagem encontrada. Escolha uma artwork ou configure o SteamGridDB para fundos panorâmicos."};
    }
    // Portrait cover for the library cards: SteamGridDB 600×900 grids when a key is set, otherwise the
    // infobox image of the game's Wikipedia article, which is normally the official box art.
    public async Task<ArtworkResult> ResolveCover(Game game,bool online,string key,CancellationToken token) {
      if(!online)return new ArtworkResult{Message="Busca online desativada. Escolha uma capa em Editar cadastro."};
      if(!String.IsNullOrWhiteSpace(key)){
        try{
          string id=await SteamGridId(game,key,token);
          if(id!=null){var response=await Json("https://www.steamgriddb.com/api/v2/grids/game/"+Uri.EscapeDataString(id)+"?dimensions=600x900&types=static&mimes=image/png,image/jpeg&nsfw=false&humor=false",key,token);
            foreach(object item in Array(Get(response,"data"))){string url=Convert.ToString(Get(Map(item),"url"));if(!String.IsNullOrEmpty(url))return new ArtworkResult{File=await Download(url,game.Id,token),Source="SteamGridDB · https://www.steamgriddb.com/game/"+id};}}
        }catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
        catch(Exception ex){store.Log("SteamGridDB capa: "+ex.Message);}
      }
      try{var found=await Wikipedia(game,token);if(found!=null)return found;}
      catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
      catch(Exception ex){store.Log("Wikipedia capa: "+ex.Message);}
      return new ArtworkResult{Message="Nenhuma capa encontrada. Escolha uma imagem em Editar cadastro."};
    }
    async Task<ArtworkResult> Wikipedia(Game game,CancellationToken token){
      string title=Ports.CanonicalTitle(game);if(title=="Shadow of the Colossus")title="Shadow of the Colossus (2018 video game)";
      string endpoint="https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageimages&piprop=original&pilicense=any&titles="+Uri.EscapeDataString(title);
      var found=await WikipediaPages(endpoint,game,token);if(found!=null)return found;
      // No exact article (or one without an image, like a disambiguation page): search for the game instead.
      string search="https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&generator=search&gsrlimit=3&gsrnamespace=0&prop=pageimages&piprop=original&pilicense=any&gsrsearch="+Uri.EscapeDataString(title+" video game");
      return await WikipediaPages(search,game,token);
    }
    async Task<ArtworkResult> WikipediaPages(string endpoint,Game game,CancellationToken token){
      var doc=await Json(endpoint,null,token);var query=Map(Get(doc,"query"));
      var pages=Array(Get(query,"pages")).Select(Map).OrderBy(p=>{object index=Get(p,"index");return index==null?0:Convert.ToInt32(index);});
      foreach(var page in pages){string url=Convert.ToString(Get(Map(Get(page,"original")),"source"));if(!String.IsNullOrWhiteSpace(url))return new ArtworkResult{File=await Download(url,game.Id,token),Source="Wikipedia · https://en.wikipedia.org/wiki/"+Uri.EscapeDataString(Convert.ToString(Get(page,"title")))};}
      return null;
    }
    async Task<string> SteamGridId(Game game,string key,CancellationToken token){
      string title=Ports.CanonicalTitle(game);
      var search=await Json("https://www.steamgriddb.com/api/v2/search/autocomplete/"+Uri.EscapeDataString(title),key,token);
      foreach(object item in Array(Get(search,"data"))){var entry=Map(item);if(Ports.Normalize(Convert.ToString(Get(entry,"name")))==Ports.Normalize(title))return Convert.ToString(Get(entry,"id"));}
      return null;
    }
    async Task<ArtworkResult> SteamGrid(Game game,string key,CancellationToken token) {
      string id=await SteamGridId(game,key,token);
      if(id==null)return null;
      var response=await Json("https://www.steamgriddb.com/api/v2/heroes/game/"+Uri.EscapeDataString(id)+"?mimes=image/png,image/jpeg&nsfw=false&humor=false",key,token);
      foreach(object item in Array(Get(response,"data"))){string url=Convert.ToString(Get(Map(item),"url"));if(!String.IsNullOrEmpty(url))return new ArtworkResult{File=await Download(url,game.Id,token),Source="SteamGridDB · https://www.steamgriddb.com/game/"+id};}return null;
    }
    async Task<Dictionary<string,object>> Json(string url,string key,CancellationToken token) {
      using(var req=new HttpRequestMessage(HttpMethod.Get,url)){
        if(!String.IsNullOrEmpty(key))req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        using(var res=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,token)){
          res.EnsureSuccessStatusCode();byte[] bytes=await Limited(res,2*1024*1024,token);
          return Map(new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.DeserializeObject(System.Text.Encoding.UTF8.GetString(bytes)));
        }
      }
    }
    async Task<string> Download(string url,string id,CancellationToken token) {
      Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!(uri.Host=="upload.wikimedia.org"||uri.Host=="cdn2.steamgriddb.com"||uri.Host=="cdn.steamgriddb.com"))throw new InvalidDataException("Servidor de imagens não reconhecido.");
      using(var req=new HttpRequestMessage(HttpMethod.Get,uri))using(var res=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,token)){
        res.EnsureSuccessStatusCode();byte[] bytes=await Limited(res,15*1024*1024,token);
        string temp=Path.Combine(store.Cache,Guid.NewGuid().ToString("N")+".download");
        try{File.WriteAllBytes(temp,bytes);return store.ImportArtwork(temp,id);}finally{if(File.Exists(temp))File.Delete(temp);}
      }
    }
    static async Task<byte[]> Limited(HttpResponseMessage response,int limit,CancellationToken token){
      if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Resposta muito grande.");
      using(var stream=await response.Content.ReadAsStreamAsync())using(var buffer=new MemoryStream()){
        byte[] chunk=new byte[16384];int count;while((count=await stream.ReadAsync(chunk,0,chunk.Length,token))>0){if(buffer.Length+count>limit)throw new InvalidDataException("Resposta muito grande.");buffer.Write(chunk,0,count);}return buffer.ToArray();
      }
    }
    static object Get(Dictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
    static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
    static object[] Array(object value){return value as object[]??new object[0];}
    public void Dispose(){client.Dispose();}
  }
}
