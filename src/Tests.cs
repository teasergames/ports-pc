using System;
using System.IO;
using System.Linq;
using System.Drawing;

namespace Nexus {
  public static class Tests {
    static int count;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);count++;}
    static void Reject(Action action,string name){try{action();}catch{count++;return;}throw new Exception("FAIL: "+name);}
    public static int Run(){
      string reportPath=Environment.GetEnvironmentVariable("NEXUS_TEST_REPORT");
      string testParent=String.IsNullOrEmpty(reportPath)?Path.GetTempPath():Path.GetDirectoryName(Path.GetFullPath(reportPath));
      string root=Path.Combine(testParent,"NexusTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
      try{
        var store=new Store(root);Library lib=store.Load();Check(lib.Games.Count==0,"empty library");
        string folder=Path.Combine(root,"Ports com espaço ç");Directory.CreateDirectory(folder);string exe=Path.Combine(folder,"Meu port.exe");File.WriteAllText(exe,"");
        Game g=Ports.FromPath(exe);g.Name="Aventura 日本語 ç";g.Arguments="--profile \"two words\"";lib.Games.Add(g);store.Save(lib);
        var read=store.Load();Check(read.Games[0].Name==g.Name,"unicode persistence");Check(read.Games[0].Arguments==g.Arguments,"argument persistence");
        var start=Ports.StartInfo(g);Check(start.FileName==exe&&start.WorkingDirectory==folder,"launch paths with spaces");Check(start.Arguments==g.Arguments&&start.UseShellExecute,"direct launch arguments preserved");
        Game invalid=g.Copy();invalid.Executable=Path.Combine(root,"bad.cmd");File.WriteAllText(invalid.Executable,"");Reject(()=>Ports.StartInfo(invalid),"reject command script");invalid.Executable=Path.Combine(root,"missing.exe");Reject(()=>Ports.StartInfo(invalid),"missing executable");invalid=g.Copy();invalid.WorkingDirectory=Path.Combine(root,"missing");Reject(()=>Ports.StartInfo(invalid),"missing working directory");
        g.Favorite=true;store.Save(lib);Check(File.Exists(store.FilePath+".bak"),"atomic save backup");File.WriteAllText(store.FilePath,"invalid json");Check(store.Load().Games.Count==1,"backup recovery");Check(Directory.GetFiles(root,"*.corrupt-*").Length==1,"preserve corrupt file");
        Reject(()=>Store.Parse("{\"Version\":2,\"Games\":[]}"),"reject unknown schema");
        var duplicate=new Library();duplicate.Games.Add(g);duplicate.Games.Add(g.Copy());Reject(()=>store.Save(duplicate),"reject duplicate identifiers");
        lib.SetApiKey("test-key");Check(lib.ApiKey()=="test-key"&&!lib.ProtectedApiKey.Contains("test-key"),"DPAPI key roundtrip");lib.SetApiKey("");Check(lib.ApiKey()=="","key clearing");
        string gow=Path.Combine(root,@"gow3-windows\gow3-windows");Directory.CreateDirectory(gow);File.WriteAllText(Path.Combine(gow,"Play God of War III.exe"),"");
        string sotc=Path.Combine(root,"SotC-PC");Directory.CreateDirectory(sotc);File.WriteAllText(Path.Combine(sotc,"SotC.exe"),"");
        var detected=Ports.Detect(root);Check(detected.Count==2,"detect known ports");Check(detected.First(x=>x.Name=="Shadow of the Colossus").Arguments=="--play","SotC direct play flag");Check(detected.First(x=>x.Name=="God of War III").Executable.EndsWith("Play God of War III.exe"),"GOW direct executable");
        string configRoot=Path.Combine(root,"appdata");Directory.CreateDirectory(Path.Combine(configRoot,"gow3-launcher"));
        File.WriteAllText(Path.Combine(configRoot,@"gow3-launcher\settings.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {game_dir=gow}));
        Check(Ports.ConfiguredGameDirectory("God of War III",configRoot)==gow,"read original launcher game folder");
        Check(Ports.ConfiguredGameDirectory("Other",configRoot)==null,"ignore unrelated launcher configs");
        string picture=Path.Combine(folder,"background.png");using(var image=new Bitmap(160,90)){image.Save(picture,System.Drawing.Imaging.ImageFormat.Png);}
        Check(Ports.FindLocalArtwork(g)==picture,"local artwork detection");string cached=store.ImportArtwork(picture,g.Id);Check(File.Exists(cached)&&cached.StartsWith(store.Cache),"artwork cached independently");File.Delete(picture);using(var image=Image.FromFile(cached)){Check(image.Width==160&&image.Height==90,"cached artwork survives source removal");}
        File.WriteAllText(Path.Combine(folder,"bad.png"),"bad");Reject(()=>store.ImportArtwork(Path.Combine(folder,"bad.png"),g.Id),"reject invalid image");
        File.WriteAllText(Path.Combine(gow,"God of War III.exe"),"");Game gowGame=Ports.FromPath(Path.Combine(gow,"Play God of War III.exe"));
        Check(Ports.ConfigurationStartInfo(gowGame).FileName.EndsWith("God of War III.exe"),"GOW original configuration launcher");Check(Ports.StartInfo(gowGame).FileName.EndsWith("Play God of War III.exe"),"GOW play stays direct");
        Game shadow=Ports.FromPath(Path.Combine(sotc,"SotC.exe"));Check(Ports.ConfigurationStartInfo(shadow).Arguments==""&&Ports.StartInfo(shadow).Arguments=="--play","Shadow configuration and play separated");
        string bb=Path.Combine(root,"BBPORT");Directory.CreateDirectory(bb);File.WriteAllText(Path.Combine(bb,"Bloodborne.exe"),"");File.WriteAllText(Path.Combine(bb,"Bloodborne Launcher.exe"),"");
        Game blood=Ports.FromPath(Path.Combine(bb,"Bloodborne.exe"));Check(Ports.ConfigurationStartInfo(blood).FileName.EndsWith("Bloodborne Launcher.exe")&&Ports.StartInfo(blood).FileName.EndsWith("Bloodborne.exe"),"Bloodborne configuration and play separated");
        Game custom=g.Copy();custom.ConfigurationExecutable=Path.Combine(bb,"Bloodborne Launcher.exe");custom.ConfigurationArguments="--custom";Check(Ports.ConfigurationStartInfo(custom).Arguments=="--custom","custom configuration arguments");
        var unchartedFolder=Path.Combine(root,"Uncharted PC port Bila");Directory.CreateDirectory(unchartedFolder);string launcher=Path.Combine(unchartedFolder,"Uncharted Launcher.exe");File.WriteAllText(launcher,"");Directory.CreateDirectory(Path.Combine(unchartedFolder,"python"));Directory.CreateDirectory(Path.Combine(unchartedFolder,"tools"));File.WriteAllText(Path.Combine(unchartedFolder,@"python\python.exe"),"");File.WriteAllText(Path.Combine(unchartedFolder,@"tools\probe_uncharted.py"),"");File.WriteAllText(Path.Combine(unchartedFolder,"eboot.bin"),"");
        Game uc=Ports.FromPath(launcher);uc.Name="Uncharted Collection";Check(Ports.CanonicalTitle(uc)=="Uncharted: The Nathan Drake Collection","Uncharted alias resolved");
        var ucConfig=new System.Collections.Generic.Dictionary<string,object>{{"game_dir",unchartedFolder},{"game","u2"},{"language","2"},{"games",new System.Collections.Generic.Dictionary<string,object>{{"u2",new System.Collections.Generic.Dictionary<string,object>{{"resolution","1920x1080"},{"aspect","21:9"},{"multithreaded",true}}}}}};
        var ucStart=Uncharted.StartInfo(uc,ucConfig);Check(ucStart.FileName.EndsWith(@"python\python.exe")&&ucStart.CreateNoWindow&&!ucStart.UseShellExecute,"Uncharted uses bundled runtime without launcher");Check(ucStart.Arguments.Contains("big2-ps4_Shipping.elf")&&ucStart.Arguments.Contains("--boot --seconds 0"),"Uncharted selected game direct boot");Check(ucStart.EnvironmentVariables["UNCHARTED_RESOLUTION_U2"]=="2560x1080"&&ucStart.EnvironmentVariables["UNCHARTED_MULTITHREADED_U2"]=="1"&&ucStart.EnvironmentVariables["UNCHARTED_LANGUAGE"]=="2","Uncharted saved options preserved");
        Check(Ports.ConfigurationStartInfo(uc).FileName==launcher&&Ports.ConfigurationStartInfo(uc).Arguments=="","Uncharted configure opens original launcher");uc.ArtworkSearchTitle="Custom artwork title";Check(Ports.CanonicalTitle(uc)=="Custom artwork title"&&Ports.PortTitle(uc)=="Uncharted: The Nathan Drake Collection","artwork override independent of port identity");
        Check(Uncharted.Quote(@"C:\space path\")=="\"C:\\space path\\\\\"","quote trailing slash safely");Check(Uncharted.Quote("a\"b")=="\"a\\\"b\"","quote embedded quote safely");
        Directory.CreateDirectory(Path.Combine(configRoot,"Uncharted Launcher"));File.WriteAllText(Path.Combine(configRoot,@"Uncharted Launcher\launcher.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(ucConfig));Check(Ports.ConfiguredGameDirectory("Uncharted: The Nathan Drake Collection",configRoot)==unchartedFolder,"Uncharted reads launcher.json");
        lib.Games[0].ConfigurationExecutable=custom.ConfigurationExecutable;lib.Games[0].ConfigurationArguments="--test";lib.Games[0].ArtworkSearchTitle="Test artwork";store.Save(lib);Check(store.Load().Games[0].ConfigurationArguments=="--test"&&store.Load().Games[0].ArtworkSearchTitle=="Test artwork","new fields persist in original library schema");
        var oldView=Store.Parse("{\"Version\":1,\"Games\":[]}");Check(oldView.ViewMode=="grid"&&oldView.GridSize==220&&oldView.ListSize==76,"legacy library view defaults");
        lib.ViewMode="list";lib.GridSize=320;lib.ListSize=60;store.Save(lib);var view=store.Load();Check(view.ViewMode=="list"&&view.GridSize==320&&view.ListSize==60,"view and separate sizes persist");
        lib.ViewMode="invalid";lib.GridSize=900;lib.ListSize=-4;store.Save(lib);view=store.Load();Check(view.ViewMode=="grid"&&view.GridSize==340&&view.ListSize==76,"invalid view preferences normalized");
        lib.ViewMode="list";lib.GridSize=10;lib.ListSize=900;store.Save(lib);view=store.Load();Check(view.GridSize==140&&view.ListSize==132,"view sizes clamped to bounds");
        File.WriteAllText(Path.Combine(root,"results.txt"),"PASS "+count);System.Console.WriteLine("PASS "+count+" checks");
        string report=Environment.GetEnvironmentVariable("NEXUS_TEST_REPORT");if(!String.IsNullOrEmpty(report))File.WriteAllText(report,"PASS "+count+" checks"+Environment.NewLine);
        return 0;
      }catch(Exception ex){System.Console.Error.WriteLine(ex.ToString());string report=Environment.GetEnvironmentVariable("NEXUS_TEST_REPORT");if(!String.IsNullOrEmpty(report))File.WriteAllText(report,ex.ToString());return 1;}
      finally{try{string resolved=Path.GetFullPath(root);string expected=Path.GetFullPath(testParent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(resolved.StartsWith(expected,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(resolved).StartsWith("NexusTests-"))Directory.Delete(resolved,true);}catch{}}
    }
  }
}
