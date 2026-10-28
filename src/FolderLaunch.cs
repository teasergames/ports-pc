using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
namespace Nexus {
  public static class FolderLaunch {
    public static bool Supports(Game game){return game!=null&&((game.Repository=="beatrixzy/PGR4-Recomp"&&Path.GetFileName(game.Executable).Equals("pgr4_recompiled.exe",StringComparison.OrdinalIgnoreCase))||(game.Repository=="rashevskyv/conker-lr-recomp"&&Path.GetFileName(game.Executable).Equals("clx.exe",StringComparison.OrdinalIgnoreCase)));}
    public static string RequiredFile(Game game){return game.Repository=="beatrixzy/PGR4-Recomp"?"default.xex":"default.xbe";}
    public static bool NeedsSetup(Game game){return Supports(game)&&(String.IsNullOrEmpty(game.SourceGameDirectory)||!File.Exists(Path.Combine(game.SourceGameDirectory,RequiredFile(game))));}
    public static void Validate(Game game,string folder){if(!Supports(game)||String.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder)||!File.Exists(Path.Combine(folder,RequiredFile(game))))throw new InvalidDataException("Escolha a pasta extraída com "+RequiredFile(game)+" e os demais arquivos do jogo.");}
    public static ProcessStartInfo StartInfo(Game game){
      Validate(game,game.SourceGameDirectory);string root=Path.GetDirectoryName(Path.GetFullPath(game.Executable));var start=new ProcessStartInfo{FileName=Path.GetFullPath(game.Executable),WorkingDirectory=root,Arguments=game.Arguments??"",UseShellExecute=true};
      if(game.Repository=="beatrixzy/PGR4-Recomp"){start.Arguments+=" --game_data_root "+Uncharted.Quote(Path.GetFullPath(game.SourceGameDirectory));return start;}
      start.UseShellExecute=false;start.CreateNoWindow=true;start.EnvironmentVariables["NFSU2_GAME_DIR"]=Path.GetFullPath(game.SourceGameDirectory);
      Default(start,"CLX_START_SINGLE","1");Default(start,"RECOMP_PAD_MASK","0x20");Default(start,"RECOMP_QUIET","1");Default(start,"RECOMP_CACHE_DIR",Path.Combine(root,"cache")+Path.DirectorySeparatorChar);Default(start,"CLX_OPTIONS",Path.Combine(root,"clx_options.txt"));Default(start,"CLX_MOUSE_OPTIONS",Path.Combine(root,"clx_mouse_options.txt"));
      string options=start.EnvironmentVariables["CLX_OPTIONS"];if(File.Exists(options))foreach(string line in File.ReadAllLines(options)){string text=line.Split('#')[0].Trim();int at=text.IndexOf('=');if(at<0)continue;string key=text.Substring(0,at).Trim(),value=text.Substring(at+1).Trim();if(key=="skip_intro"&&value=="1")Default(start,"CLX_SKIP_INTRO","1");if(key=="start_single")Default(start,"CLX_START_SINGLE",value);if(key=="fullscreen")Default(start,"CLX_BORDERLESS",value=="borderless"||value=="fullscreen"||value=="1"?"1":"0");if(key=="mouse")Default(start,"CLX_NATIVE_MOUSE",value=="0"||value=="off"?"0":"1");}
      Default(start,"CLX_NATIVE_MOUSE","1");Default(start,"CLX_BORDERLESS","1");return start;
    }
    public static void PrepareLaunch(Game game,ProcessStartInfo start){if(Supports(game)&&game.Repository=="rashevskyv/conker-lr-recomp")Directory.CreateDirectory(start.EnvironmentVariables["RECOMP_CACHE_DIR"]);}
    static void Default(ProcessStartInfo start,string name,string value){if(String.IsNullOrEmpty(start.EnvironmentVariables[name]))start.EnvironmentVariables[name]=value;}
  }
}
