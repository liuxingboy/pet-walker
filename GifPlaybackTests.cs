using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
class GifPlaybackTest {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string name){return o.GetType().GetField(name,F).GetValue(o);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 [STAThread] static int Main(){
  string root=AppDomain.CurrentDomain.BaseDirectory,qa=Path.Combine(root,"qa","gif-playback");Form pet=null;
  Directory.CreateDirectory(qa);
  using(var quit=new EventWaitHandle(false,EventResetMode.AutoReset))using(var pause=new EventWaitHandle(false,EventResetMode.AutoReset))try{
   var assembly=Assembly.LoadFrom(Path.Combine(root,"HydrangeaWalker.exe"));var type=assembly.GetType("PetWindow");
   pet=(Form)Activator.CreateInstance(type,new object[]{true,false,quit,pause});
   var art=(Dictionary<string,Bitmap[]>)Field(pet,"art");var times=(Dictionary<string,int[]>)Field(pet,"gifDurations");var index=type.GetMethod("GifFrameIndex",F);
   string decoded=Path.Combine(qa,"decoded");Directory.CreateDirectory(decoded);var report=new List<object>();
   foreach(string name in new[]{"idle","walk-left","walk-right","wave","jump","failed","waiting","task","review"}){
    int sum=0;for(int i=0;i<art[name].Length;i++){
     art[name][i].Save(Path.Combine(decoded,name+"-"+i+".png"),ImageFormat.Png);
     Check((int)index.Invoke(pet,new object[]{name,(sum+1)/1000.0})==i,"Wrong timing: "+name+" "+i);sum+=times[name][i];
    }
    Check((int)index.Invoke(pet,new object[]{name,(sum+1)/1000.0})==0,"Wrong loop: "+name);
    report.Add(new{state=name,frames=art[name].Length,delays_ms=times[name],cycle_ms=sum});
   }
   var menu=(ContextMenuStrip)Field(pet,"menu");ToolStripItem action=null;
   foreach(ToolStripItem item in menu.Items){Check(item.Text!="挥挥手","Old menu remains");if(item.Text=="测试任务动作")action=item;}
   Check(action!=null&&action.Enabled,"Task submenu unavailable");var submenu=(ToolStripMenuItem)action;
   Check(submenu.DropDownItems.Count==11,"Expected nine actions, separator, and stop");
   pet.Show();Application.DoEvents();object motion=Field(pet,"motion");var xpos=motion.GetType().GetField("X");
   var states=new[]{"idle","walk-left","walk-right","wave","jump","failed","waiting","task","review"};var tested=new List<object>();
   var petClock=(Stopwatch)Field(pet,"clock");
   for(int a=0;a<states.Length;a++){
    string state=states[a];Check(submenu.DropDownItems[a].Enabled,"Disabled action "+state);
    petClock.Restart();type.GetField("lastTick",F).SetValue(pet,0.0);submenu.DropDownItems[a].PerformClick();
    double startX=(double)xpos.GetValue(motion);int cycle=0;foreach(int ms in times[state])cycle+=ms;
    var observed=new HashSet<string>();var watchAction=Stopwatch.StartNew();
    while(watchAction.Elapsed.TotalMilliseconds<cycle+160){
     Application.DoEvents();if(watchAction.Elapsed.TotalMilliseconds>40){
      Check((string)Field(pet,"animationState")==state,"Wrong submenu action "+state);
      Check((double)xpos.GetValue(motion)==startX,"Moved during "+state);observed.Add((string)Field(pet,"lastFrame"));
     }Thread.Sleep(15);
    }
    Check(observed.Count==art[state].Length,"Incomplete frames: "+state);tested.Add(new{state=state,framesSeen=observed.Count});
   }
   submenu.DropDownItems[10].PerformClick();var stopWatch=Stopwatch.StartNew();
   while(stopWatch.ElapsedMilliseconds<150){Application.DoEvents();Thread.Sleep(15);}
   Check((string)Field(pet,"animationState")=="idle","Stop did not restore idle");
   petClock.Restart();type.GetField("lastTick",F).SetValue(pet,0.0);submenu.DropDownItems[7].PerformClick();
   var watch=Stopwatch.StartNew();bool restored=false;
   while(watch.Elapsed.TotalSeconds<6.5){Application.DoEvents();if(watch.Elapsed.TotalSeconds>6.3&&(string)Field(pet,"animationState")!="task")restored=true;Thread.Sleep(15);}
   Check(restored,"Timed recovery failed");
   File.WriteAllText(Path.Combine(qa,"gif-test-report.json"),new JavaScriptSerializer().Serialize(new{ok=true,gifs=report,actions=tested,stopRestoredIdle=true,movementPaused=true,returnedAfterSixSeconds=restored}));
   Console.WriteLine("PASS: GIF decoding/timings, menu click, all 9 actions and 57 frames, paused movement, automatic return.");return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(qa,"gif-test-error.txt"),e.ToString());Console.WriteLine(e);return 1;}
  finally{if(pet!=null){pet.Close();pet.Dispose();}}
 }
}
