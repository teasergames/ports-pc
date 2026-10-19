using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Nexus {
  public static class Uncharted {
    static string Text(Dictionary<string,object> data,string key,string fallback){object value;return data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):fallback;}
    static bool Flag(Dictionary<string,object> data,string key,bool fallback){object value;return data.TryGetValue(key,out value)&&value is bool?(bool)value:fallback;}
    public static string Quote(string arg){
      var result=new System.Text.StringBuilder("\"");int slashes=0;
      foreach(char c in arg){if(c=='\\'){slashes++;continue;}if(c=='"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
      result.Append('\\',slashes*2);result.Append('"');return result.ToString();
    }
    public static ProcessStartInfo StartInfo(Game game,Dictionary<string,object> config){
      string root=Path.GetDirectoryName(game.Executable),python=Path.Combine(root,@"python\python.exe"),script=Path.Combine(root,@"tools\probe_uncharted.py");
      if(!File.Exists(python)||!File.Exists(script))throw new FileNotFoundException("O port de Uncharted está incompleto. Confira python/python.exe e tools/probe_uncharted.py.");
      string content=Text(config,"game_dir","");if(!Directory.Exists(content)||!File.Exists(Path.Combine(content,"eboot.bin")))throw new DirectoryNotFoundException("Abra Configurar jogo e selecione a pasta do jogo no launcher de Uncharted antes de jogar.");
      string key=Text(config,"game","u1"),image=key=="u2"?"big2-ps4_Shipping.elf":key=="u3"?"big3-ps4_Shipping.elf":"eboot.bin";
      var info=new ProcessStartInfo{FileName=python,Arguments="-u "+Quote(script)+" --boot --seconds 0 --executable "+image+" --game "+Quote(content),WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true};
      info.EnvironmentVariables["UNCHARTED_DIAGNOSTICS"]=Flag(config,"diagnostics",true)?"1":"0";
      info.EnvironmentVariables["UNCHARTED_TORCH_FORWARD"]=Flag(config,"torch_forward",true)?"1":"0";
      info.EnvironmentVariables["UNCHARTED_JOB_SLEEP"]=Text(config,"job_sleep","auto")=="off"?"0":"1";
      info.EnvironmentVariables["UNCHARTED_LANGUAGE"]=Text(config,"language","1");info.EnvironmentVariables["UNCHARTED_UNLOCK"]=Flag(config,"unlock",false)?"1":"0";info.EnvironmentVariables.Remove("GOW3_VBLANK_HZ");
      object obj;var games=config.TryGetValue("games",out obj)?obj as Dictionary<string,object>:null;
      foreach(string gameKey in new[]{"u1","u2","u3"}){
        var values=games!=null&&games.TryGetValue(gameKey,out obj)?obj as Dictionary<string,object>:null;if(values==null)continue;
        info.EnvironmentVariables["UNCHARTED_RESOLUTION_"+gameKey.ToUpperInvariant()]=Resolution(Text(values,"resolution","1920x1080"),Text(values,"aspect","16:9"));
        info.EnvironmentVariables["UNCHARTED_MULTITHREADED_"+gameKey.ToUpperInvariant()]=Flag(values,"multithreaded",false)?"1":"0";
      }
      return info;
    }
    public static string Resolution(string resolution,string aspect){
      string[] parts=resolution.Split('x');int height;if(parts.Length!=2||!Int32.TryParse(parts[1],out height)||height<=0)return resolution;
      if(aspect=="16:9")return resolution;
      if(aspect=="21:9"&&(height==1080||height==1440||height==1600))return (height==1080?2560:height==1440?3440:3840)+"x"+height;
      string[] ratio=aspect.Split(':');double width,heightRatio;if(ratio.Length!=2||!Double.TryParse(ratio[0],out width)||!Double.TryParse(ratio[1],out heightRatio)||heightRatio<=0||width<=0)return resolution;
      return ((int)Math.Round(height*width/heightRatio/8)*8)+"x"+height;
    }
  }
}
