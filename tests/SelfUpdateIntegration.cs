using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using Nexus;
public static class SelfUpdateIntegration {
  public static int Main(string[] args){
    try{
      string root=Environment.GetEnvironmentVariable("NEXUS_SELF_UPDATE_FIXTURE_ROOT");if(String.IsNullOrEmpty(root))throw new Exception("Fixture root missing");typeof(SelfUpdater).GetField("TestJobsRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.Combine(root,"jobs"));
      if(args.Length>1&&args[0]=="--apply-nexus-update")return SelfUpdater.RunHelper(args[1],args.Contains("--recover-nexus-update"));
      foreach(int mode in new[]{0,1,2}){bool failure=mode==1,recovery=mode==2;
        string targetDir=Path.Combine(root,recovery?"recovery & João's copy":failure?"rollback & João's copy":"success & João's copy");Directory.CreateDirectory(targetDir);string target=Path.Combine(targetDir,"Nexus.exe");File.Copy(Path.Combine(root,"Old.exe"),target);File.WriteAllText(Path.Combine(targetDir,"Nexus.exe.config"),"<configuration/>");File.WriteAllText(Path.Combine(targetDir,"README.md"),"old readme");File.WriteAllText(Path.Combine(targetDir,"library.json"),"my library");Directory.CreateDirectory(Path.Combine(targetDir,"ports"));File.WriteAllText(Path.Combine(targetDir,"ports","own.iso"),"owned disc sentinel");
        string zip=Path.Combine(root,recovery?"recovery.zip":failure?"bad-start.zip":"success.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){foreach(string n in SelfUpdater.PackageFiles){var entry=archive.CreateEntry("Nexus/"+n);using(var output=entry.Open()){byte[] bytes=n=="Nexus.exe"?File.ReadAllBytes(Path.Combine(root,failure?"Fail.exe":"New.exe")):System.Text.Encoding.UTF8.GetBytes(n=="Nexus.exe.config"?"<configuration/>":"updated "+n);output.Write(bytes,0,bytes.Length);}}}
        string job=SelfUpdater.NewJob();var release=new PortRelease{Version="v1.6.4",Digest="sha256:"+UpdateManager.Hash(zip),Size=new FileInfo(zip).Length};string file=SelfUpdater.Prepare(zip,release,job,target,Path.Combine(root,"data & user's library"),CancellationToken.None);var plan=SelfUpdater.Read(file);
        if(recovery){try{SelfUpdater.Apply(job,plan,delegate(int at){if(at==2)throw new IOException("simulate interrupted helper");});}catch(IOException){}if(plan.State!="applying")throw new Exception("Interrupted journal missing");}
        using(var old=Process.Start(new ProcessStartInfo(target,"--fixture-wait"){UseShellExecute=false,CreateNoWindow=true})){plan.ParentId=old.Id;plan.ParentTicks=old.StartTime.ToUniversalTime().Ticks;SelfUpdater.Save(job,plan);File.Copy(typeof(SelfUpdater).Assembly.Location,Path.Combine(job,"Nexus.exe"));using(var helper=Process.Start(new ProcessStartInfo(Path.Combine(job,"NexusUpdate.exe"),"--apply-nexus-update "+Uncharted.Quote(file)+(recovery?" --recover-nexus-update":"")){UseShellExecute=false,CreateNoWindow=true})){if(!helper.WaitForExit(60000))throw new Exception("Helper timeout");if(helper.ExitCode!=(failure?1:0))throw new Exception("Helper exit mismatch: "+File.ReadAllText(Path.Combine(job,"update-error.txt")));}if(!old.HasExited)throw new Exception("Old process was not awaited");}
        plan=SelfUpdater.Read(file);if(plan.State!=(failure||recovery?"rolledback":"complete")||UpdateManager.Hash(target)!=UpdateManager.Hash(Path.Combine(root,failure||recovery?"Old.exe":"New.exe"))||File.ReadAllText(Path.Combine(targetDir,"library.json"))!="my library"||File.ReadAllText(Path.Combine(targetDir,"ports","own.iso"))!="owned disc sentinel")throw new Exception("Transaction state/data mismatch");if((failure||recovery)&&File.ReadAllText(Path.Combine(targetDir,"README.md"))!="old readme")throw new Exception("Rollback docs mismatch");if(!failure&&!recovery&&!File.Exists(Path.Combine(job,"started.txt")))throw new Exception("Restart not acknowledged");Console.WriteLine("PASS separate helper, parent exit, "+(recovery?"interrupted transaction recovery":failure?"failed startup rollback":"automatic restart acknowledgement")+", quoted Unicode paths and user data preservation");
      }return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  }
}
