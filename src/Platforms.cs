using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
namespace Nexus {
  public static class Platforms {
    public static readonly string[] All={"Todas as plataformas","PlayStation 4","PlayStation 3","Xbox","Xbox 360","Xbox One","Nintendo Switch","Outras"};
    public static string Resolve(Game game){
      if(All.Skip(1).Contains(game.Platform))return game.Platform;
      var entry=PortCatalog.LoadEntries().FirstOrDefault(e=>!String.IsNullOrEmpty(game.Repository)&&e.Identity==game.Repository);
      if(entry!=null)return entry.Platform;
      entry=PortCatalog.LoadEntries().FirstOrDefault(e=>e.PlayNames.Any(n=>n.Equals(Path.GetFileName(game.Executable),StringComparison.OrdinalIgnoreCase)));if(entry!=null)return entry.Platform;
      string title=Ports.PortTitle(game);return new[]{"God of War III","Bloodborne","Shadow of the Colossus","Uncharted: The Nathan Drake Collection"}.Contains(title)?"PlayStation 4":"Outras";
    }
    public static bool Matches(Game game,string filter){return String.IsNullOrEmpty(filter)||filter==All[0]||Resolve(game)==filter;}
    public static List<Game> Filter(IEnumerable<Game> games,string platform,string search,bool favorites){return games.Where(g=>Matches(g,platform)&&(!favorites||g.Favorite)&&g.Name.IndexOf(search??"",StringComparison.CurrentCultureIgnoreCase)>=0).OrderByDescending(g=>g.Favorite).ThenBy(g=>g.Name).ToList();}
  }
}
