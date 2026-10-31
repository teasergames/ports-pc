using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
namespace Nexus {
  public static class Platforms {
    public static readonly string[] All={"Todas as plataformas","PlayStation 4","PlayStation 3","PlayStation 2","PlayStation","PSP","Xbox","Xbox 360","Xbox One","Nintendo Switch","Nintendo 64","GameCube","Wii","Wii U","Super Nintendo","Game Boy","Game Boy Advance","Nintendo DS","Dreamcast","Outras"};
    static readonly Dictionary<string,string> shortNames=new Dictionary<string,string>{
      {"PlayStation 4","PS4"},{"PlayStation 3","PS3"},{"PlayStation 2","PS2"},{"PlayStation","PS1"},{"PSP","PSP"},
      {"Xbox","Xbox"},{"Xbox 360","X360"},{"Xbox One","XB1"},{"Nintendo Switch","Switch"},{"Nintendo 64","N64"},
      {"GameCube","GC"},{"Wii","Wii"},{"Wii U","Wii U"},{"Super Nintendo","SNES"},{"Game Boy","GB"},
      {"Game Boy Advance","GBA"},{"Nintendo DS","NDS"},{"Dreamcast","DC"},{"Outras","Outras"}};
    // Compact label for badges on cards and filter chips.
    public static string ShortName(string platform){string value;return platform!=null&&shortNames.TryGetValue(platform,out value)?value:platform;}
    public static string Resolve(Game game){
      if(All.Skip(1).Contains(game.Platform))return game.Platform;
      var entry=PortCatalog.LoadEntries().FirstOrDefault(e=>!String.IsNullOrEmpty(game.Repository)&&e.Identity==game.Repository);
      if(entry!=null)return entry.Platform;
      entry=PortCatalog.LoadEntries().FirstOrDefault(e=>e.PlayNames.Any(n=>n.Equals(Path.GetFileName(game.Executable),StringComparison.OrdinalIgnoreCase)));if(entry!=null)return entry.Platform;
      string title=Ports.PortTitle(game);return new[]{"God of War III","Bloodborne","Shadow of the Colossus","Uncharted: The Nathan Drake Collection"}.Contains(title)?"PlayStation 4":"Outras";
    }
    // Catalog profile a library entry came from, when it was installed or detected from the catalog.
    public static PortEntry Profile(Game game){
      if(String.IsNullOrEmpty(game.Repository))return null;
      return PortCatalog.LoadEntries().FirstOrDefault(e=>e.Identity==game.Repository);
    }
    public static bool Matches(Game game,string filter){return String.IsNullOrEmpty(filter)||filter==All[0]||Resolve(game)==filter;}
    public static List<Game> Filter(IEnumerable<Game> games,string platform,string search,bool favorites){return Filter(games,platform,search,favorites,"favorites");}
    public static List<Game> Filter(IEnumerable<Game> games,string platform,string search,bool favorites,string sort){
      var matches=games.Where(g=>Matches(g,platform)&&(!favorites||g.Favorite)&&g.Name.IndexOf(search??"",StringComparison.CurrentCultureIgnoreCase)>=0);
      switch(sort){
        case "name":return matches.OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        case "recent":return matches.OrderByDescending(g=>LastPlayed(g)).ThenBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        case "platform":return matches.OrderBy(g=>Array.IndexOf(All,Resolve(g))).ThenBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        default:return matches.OrderByDescending(g=>g.Favorite).ThenBy(g=>g.Name).ToList();
      }
    }
    public static DateTime LastPlayed(Game game){DateTime when;return !String.IsNullOrEmpty(game.LastPlayed)&&DateTime.TryParse(game.LastPlayed,out when)?when.ToUniversalTime():DateTime.MinValue;}
  }
}
