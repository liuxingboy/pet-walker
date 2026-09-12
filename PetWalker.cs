using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// All movement is confined to this program's own transparent window.
// No Codex process injection, window manipulation, task polling or network access.
public sealed class WalkMotion {
    public double X, Y, Target, MinX, MaxX, MinY, MaxY, Speed=42, Rest=2;
    public bool Walking; public int Direction=1; public int Trips;
    readonly Random random;
    public WalkMotion(int seed) { random=new Random(seed); }
    public static double Clamp(double x,double min,double max) { return Math.Max(min,Math.Min(max,x)); }
    public void SetArea(Rectangle area,int width,int height) {
        MinX=area.Left; MaxX=Math.Max(MinX,area.Right-width);
        MinY=area.Top; MaxY=Math.Max(MinY,area.Bottom-height);
        X=Clamp(X,MinX,MaxX); Y=Clamp(Y,MinY,MaxY); Target=Clamp(Target,MinX,MaxX);
    }
    public void StartTrip() {
        double span=MaxX-MinX; if(span<2) { Walking=false; Rest=3; return; }
        int dir=random.Next(2)==0?-1:1;
        if(X-MinX<50) dir=1; else if(MaxX-X<50) dir=-1;
        double distance=Math.Min(span,90+random.NextDouble()*330);
        Target=Clamp(X+dir*distance,MinX,MaxX);
        if(Math.Abs(Target-X)<1) Target=dir>0?MinX:MaxX;
        Direction=Target>=X?1:-1; Walking=true; Trips++;
    }
    public void StopFor(double seconds) { Walking=false; Target=X; Rest=seconds; }
    public void Tick(double dt,bool paused) {
        if(paused||dt<=0) return;
        dt=Math.Min(dt,0.1); // resume from sleep without teleporting
        if(!Walking) { Rest-=dt; if(Rest<=0) StartTrip(); return; }
        double step=Speed*dt,delta=Target-X;
        if(Math.Abs(delta)<=step) { X=Target; StopFor(4+random.NextDouble()*9); }
        else { Direction=delta>0?1:-1; X=Clamp(X+Direction*step,MinX,MaxX); }
    }
}

public sealed class WalkerSettings {
    public int Size=192; public double Speed=42; public int X=int.MinValue,Y=int.MinValue;
    public bool Paused=false,PauseOnHover=true; public string Monitor="";
    public bool RemindersEnabled=true; public int ReminderMinutes=55;
    public string[] ReminderMessages={"亲爱的，该喝口水休息一下啦～","眼睛累了吗？望望窗外远方吧。","站起来活动活动筋骨吧！","休息一小会，为接下来的工作充充电～","你的蓝蝶少女提醒你：该放松一下啦。","深呼吸，闭上眼，享受这一分钟的宁静吧。","工作可以稍等一下，起来走走吧。","吃点水果或者伸个懒腰吧～"};
}

static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x,y; public POINT(int a,int b){x=a;y=b;} }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx,cy; public SIZE(int a,int b){cx=a;cy=b;} }
    [StructLayout(LayoutKind.Sequential,Pack=1)] public struct BLEND { public byte Op,Flags,Alpha,Format; }
    [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref POINT at,ref SIZE size,IntPtr src,ref POINT origin,int key,ref BLEND blend,int flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}

sealed class PetWindow : Form {
    readonly string root=AppDomain.CurrentDomain.BaseDirectory;
    readonly WalkMotion motion=new WalkMotion(Environment.TickCount);
    readonly Dictionary<string,Bitmap[]> art=new Dictionary<string,Bitmap[]>();
    readonly Dictionary<string,Bitmap[]> scaled=new Dictionary<string,Bitmap[]>();
    readonly NotifyIcon tray=new NotifyIcon(); readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly Icon applicationIcon,notificationIcon;
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly EventWaitHandle quitEvent,pauseEvent;
    readonly bool smoke; WalkerSettings settings; Screen monitor;
    readonly bool reminderSmoke;
    ReminderSchedule reminders; ReminderBubble bubble; bool optionsOpen;
    readonly Random reminderRandom=new Random();int previousMessage=-1,reminderStage,remindersShown;
    double bubbleUntil,lastTip,testReminderX; string animationState="";
    ToolStripMenuItem pauseItem,hoverItem,hideItem; Bitmap current;
    bool dragging,menuOpen,hidden; Point mouseStart,windowStart;
    double lastTick,animationTime,waveUntil,saveAt,lastMonitorCheck; string lastFrame="";
    int renders,moves,rightRenders; Point initialPosition;
    public PetWindow(bool test,bool testReminder,EventWaitHandle quit,EventWaitHandle pause) {
        smoke=test||testReminder;reminderSmoke=testReminder; quitEvent=quit; pauseEvent=pause;
        Text="Hydrangea Butterfly Girl — 散步"; FormBorderStyle=FormBorderStyle.None;
        ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None;
        applicationIcon=PetIcons.Load(new Size(32,32));notificationIcon=PetIcons.Load(SystemInformation.SmallIconSize);Icon=applicationIcon;
        settings=ReadSettings();
        reminders=new ReminderSchedule(settings.RemindersEnabled,settings.ReminderMinutes,DateTime.UtcNow);
        LoadArt("idle",6);LoadArt("wave",4);LoadArt("walk-left",8);LoadArt("walk-right",8);
        string naturalRight=Path.Combine(root,"assets","natural-right"),rightRig=Path.Combine(root,"assets","rig");
        if(Directory.Exists(rightRig))ReplaceArt("walk-right",RightWalkRig.Load(rightRig));
        else if(Directory.Exists(naturalRight))ReplaceArt("walk-right",LoadFrames(naturalRight,"walk-right",8));
        monitor=FindScreen(settings.Monitor); ResizeArt(settings.Size);
        motion.Speed=settings.Speed;
        motion.X=settings.X==int.MinValue?monitor.WorkingArea.Left+monitor.WorkingArea.Width*0.35:settings.X;
        motion.Y=settings.Y==int.MinValue?monitor.WorkingArea.Bottom-Height:settings.Y;
        motion.SetArea(monitor.WorkingArea,Width,Height); Location=new Point((int)motion.X,(int)motion.Y);
        initialPosition=Location; BuildMenu();
        tray.Icon=notificationIcon;tray.Text="蓝蝶少女 · 右键暂停/退出";tray.ContextMenuStrip=menu;
        tray.DoubleClick+=delegate { hidden=false; Show();ResetPosition(false); };
        tray.Visible=!smoke;
        timer.Interval=33;timer.Tick+=Tick;
        Shown+=delegate { Render("idle",0);timer.Start();if(smoke){settings.Paused=false;settings.PauseOnHover=false;motion.Rest=0;if(!reminderSmoke){motion.X=motion.MinX;initialPosition=new Point((int)motion.X,(int)motion.Y);}} };
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p; } }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x21){m.Result=(IntPtr)3;return;} // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    WalkerSettings ReadSettings() {
        try { var s=new JavaScriptSerializer().Deserialize<WalkerSettings>(File.ReadAllText(Path.Combine(root,"settings.json")));
            if(s==null)throw new Exception();s.Size=(int)WalkMotion.Clamp(s.Size,128,320);
            s.Speed=double.IsNaN(s.Speed)||double.IsInfinity(s.Speed)?42:WalkMotion.Clamp(s.Speed,15,100);
            s.ReminderMinutes=(int)WalkMotion.Clamp(s.ReminderMinutes,1,1440);
            var messages=new List<string>();if(s.ReminderMessages!=null)foreach(var line in s.ReminderMessages)if(!String.IsNullOrWhiteSpace(line)&&line.Length<=100)messages.Add(line.Trim());
            s.ReminderMessages=messages.Count>0?messages.ToArray():new WalkerSettings().ReminderMessages;return s;
        }catch{return new WalkerSettings();}
    }
    void SaveSettings() {
        if(smoke)return;
        settings.X=(int)motion.X;settings.Y=(int)motion.Y;settings.Monitor=monitor.DeviceName;
        try { File.WriteAllText(Path.Combine(root,"settings.json"),new JavaScriptSerializer().Serialize(settings)); }
        catch(Exception e){Log("Settings: "+e.Message);}
    }
    void Log(string s){try{File.AppendAllText(Path.Combine(root,"walker.log"),DateTime.Now.ToString("s")+" "+s+Environment.NewLine);}catch{}}
    void LoadArt(string name,int count) {
        var frames=LoadFrames(Path.Combine(root,"assets"),name,count);
        art.Add(name,frames);
    }
    Bitmap[] LoadFrames(string directory,string name,int count) {
        var frames=new Bitmap[count];for(int i=0;i<count;i++)using(var b=new Bitmap(Path.Combine(directory,name+"-"+i+".png")))frames[i]=new Bitmap(b);
        return frames;
    }
    void ReplaceArt(string name,Bitmap[] frames){foreach(var old in art[name])old.Dispose();art[name]=frames;}
    void ResizeArt(int width) {
        foreach(var frames in scaled.Values)foreach(var b in frames)b.Dispose();scaled.Clear();current=null;
        settings.Size=width;Size=new Size(width,(int)Math.Round(width*208.0/192));
        foreach(var entry in art){var frames=new Bitmap[entry.Value.Length];for(int i=0;i<frames.Length;i++){
            var b=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(entry.Value[i],new Rectangle(0,0,Width,Height));}
            frames[i]=b;}scaled.Add(entry.Key,frames);}
        lastFrame="";
    }
    Screen FindScreen(string name){foreach(var s in Screen.AllScreens)if(s.DeviceName==name)return s;return Screen.PrimaryScreen;}
    void BuildMenu() {
        menu.Items.Add("Hydrangea Butterfly Girl").Enabled=false;
        pauseItem=new ToolStripMenuItem("暂停散步");pauseItem.Click+=delegate{settings.Paused=!settings.Paused;SaveSettings();};menu.Items.Add(pauseItem);
        menu.Items.Add("挥挥手",null,delegate{motion.StopFor(3);waveUntil=clock.Elapsed.TotalSeconds+2.24;animationTime=0;});
        hoverItem=new ToolStripMenuItem("鼠标悬停时暂停");hoverItem.Click+=delegate{settings.PauseOnHover=!settings.PauseOnHover;SaveSettings();};menu.Items.Add(hoverItem);
        var reminderMenu=new ToolStripMenuItem("休息提醒");
        var status=new ToolStripMenuItem();status.Enabled=false;reminderMenu.DropDownItems.Add(status);
        var enable=new ToolStripMenuItem("启用提醒");enable.Click+=delegate{settings.RemindersEnabled=!settings.RemindersEnabled;reminders.Configure(settings.RemindersEnabled,settings.ReminderMinutes,DateTime.UtcNow);DismissReminder(false,false);SaveSettings();};reminderMenu.DropDownItems.Add(enable);
        reminderMenu.DropDownItems.Add("间隔和文案…",null,delegate{ShowReminderOptions();});
        reminderMenu.DropDownItems.Add("测试提醒",null,delegate{ShowReminder(true);});
        var pauseReminder=new ToolStripMenuItem("暂停提醒 1 小时");pauseReminder.Click+=delegate{DismissReminder(false,false);reminders.Snooze(DateTime.UtcNow,60);};reminderMenu.DropDownItems.Add(pauseReminder);
        reminderMenu.DropDownOpening+=delegate{enable.Checked=settings.RemindersEnabled;status.Text=reminders.Status(DateTime.UtcNow);pauseReminder.Enabled=settings.RemindersEnabled;};menu.Items.Add(reminderMenu);
        var speed=new ToolStripMenuItem("散步速度");foreach(var item in new[]{new KeyValuePair<string,int>("慢速",24),new KeyValuePair<string,int>("舒缓（默认）",42),new KeyValuePair<string,int>("轻快",65)}){
            var value=item.Value;speed.DropDownItems.Add(item.Key,null,delegate{settings.Speed=value;motion.Speed=value;SaveSettings();});}menu.Items.Add(speed);
        var sizes=new ToolStripMenuItem("角色大小");foreach(int size in new[]{144,192,256}){int value=size;sizes.DropDownItems.Add(size+" 像素",null,delegate{double feet=motion.Y+Height;ResizeArt(value);motion.Y=feet-Height;motion.SetArea(monitor.WorkingArea,Width,Height);SaveSettings();});}menu.Items.Add(sizes);
        var screens=new ToolStripMenuItem("显示器");screens.DropDownOpening+=delegate{screens.DropDownItems.Clear();int i=0;foreach(var s in Screen.AllScreens){var target=s;var item=new ToolStripMenuItem("显示器 "+(++i)+(s.Primary?"（主屏）":""));item.Checked=s.DeviceName==monitor.DeviceName;item.Click+=delegate{monitor=target;ResetPosition(true);};screens.DropDownItems.Add(item);}};menu.Items.Add(screens);
        menu.Items.Add("回到屏幕底部",null,delegate{ResetPosition(true);});
        hideItem=new ToolStripMenuItem("隐藏角色");hideItem.Click+=delegate{hidden=!hidden;if(hidden){DismissReminder(false,false);Hide();}else Show();};menu.Items.Add(hideItem);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出散步程序",null,delegate{Close();});
        menu.Opening+=delegate{menuOpen=true;pauseItem.Checked=settings.Paused;pauseItem.Text=settings.Paused?"继续散步":"暂停散步";hoverItem.Checked=settings.PauseOnHover;hideItem.Text=hidden?"显示角色":"隐藏角色";};
        menu.Closed+=delegate{menuOpen=false;};
    }
    void ShowReminderOptions() {
        optionsOpen=true;
        try{using(var dialog=new ReminderOptions(settings.RemindersEnabled,settings.ReminderMinutes,settings.ReminderMessages)){
            if(dialog.ShowDialog()==DialogResult.OK){settings.RemindersEnabled=dialog.RemindersEnabled;settings.ReminderMinutes=dialog.IntervalMinutes;settings.ReminderMessages=dialog.Messages;
                reminders.Configure(settings.RemindersEnabled,settings.ReminderMinutes,DateTime.UtcNow);DismissReminder(false,false);SaveSettings();}
        }}finally{optionsOpen=false;}
    }
    void ShowReminder(bool manual) {
        if(bubble!=null){bubble.Present(settings.ReminderMessages[Math.Max(0,previousMessage)%settings.ReminderMessages.Length],Bounds,monitor.WorkingArea);return;}
        if(manual){reminders.RecordShown(DateTime.UtcNow);if(hidden){hidden=false;Show();}}
        int n=settings.ReminderMessages.Length,index=reminderRandom.Next(n);
        if(n>1&&index==previousMessage)index=(index+1+reminderRandom.Next(n-1))%n;
        previousMessage=index;
        bubble=new ReminderBubble();bubble.Responded+=delegate(bool later){DismissReminder(later,true);};
        bubble.Present(settings.ReminderMessages[index],new Rectangle((int)motion.X,(int)motion.Y,Width,Height),monitor.WorkingArea);
        bubbleUntil=clock.Elapsed.TotalSeconds+ReminderBubble.DisplaySeconds;waveUntil=clock.Elapsed.TotalSeconds+2.24;animationTime=0;remindersShown++;
    }
    void DismissReminder(bool later,bool reschedule) {
        if(bubble!=null){bubble.Close();bubble.Dispose();bubble=null;waveUntil=0;}
        if(reschedule){if(later)reminders.Snooze(DateTime.UtcNow,5);else reminders.Acknowledge(DateTime.UtcNow);}
    }
    void ResetPosition(bool bottom) {
        if(bottom){motion.X=monitor.WorkingArea.Left+(monitor.WorkingArea.Width-Width)/2;motion.Y=monitor.WorkingArea.Bottom-Height;}
        motion.SetArea(monitor.WorkingArea,Width,Height);motion.StopFor(2);SaveSettings();lastFrame="";
    }
    bool OpaquePoint(Point point){return current!=null&&point.X>=0&&point.Y>=0&&point.X<Width&&point.Y<Height&&current.GetPixel(point.X,point.Y).A>24;}
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);if(e.Button==MouseButtons.Right){menu.Show(this,e.Location);return;}
        if(e.Button==MouseButtons.Left){dragging=true;Capture=true;mouseStart=Cursor.Position;windowStart=new Point((int)motion.X,(int)motion.Y);}
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);if(!dragging)return;var cursor=Cursor.Position;
        motion.X=windowStart.X+cursor.X-mouseStart.X;motion.Y=windowStart.Y+cursor.Y-mouseStart.Y;
        Native.SetWindowPos(Handle,new IntPtr(-1),(int)motion.X,(int)motion.Y,0,0,0x11);
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;monitor=Screen.FromPoint(Cursor.Position);motion.SetArea(monitor.WorkingArea,Width,Height);motion.StopFor(2);SaveSettings();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(dragging&&!Capture){dragging=false;monitor=Screen.FromPoint(Cursor.Position);motion.SetArea(monitor.WorkingArea,Width,Height);motion.StopFor(2);}}
    int IdleIndex(double t){double[] d={1.68,.66,.66,.84,.84,1.92};t%=6.6;for(int i=0;i<d.Length;i++){if(t<d[i])return i;t-=d[i];}return 0;}
    void Tick(object sender,EventArgs args) {
        try {
            if(quitEvent.WaitOne(0)){Close();return;}if(pauseEvent.WaitOne(0)){settings.Paused=!settings.Paused;SaveSettings();}
            double now=clock.Elapsed.TotalSeconds,dt=lastTick==0?0.033:now-lastTick;lastTick=now;
            if(bubble!=null&&now>=bubbleUntil)DismissReminder(false,true);
            if(!smoke&&reminders.TakeDue(DateTime.UtcNow,!hidden&&!dragging&&!menuOpen&&!optionsOpen&&bubble==null))ShowReminder(false);
            if(now-lastTip>=1){tray.Text="蓝蝶少女 · "+reminders.Status(DateTime.UtcNow);lastTip=now;}
            if(now-lastMonitorCheck>2&&!dragging){monitor=FindScreen(monitor.DeviceName);motion.SetArea(monitor.WorkingArea,Width,Height);lastMonitorCheck=now;}
            Point local=new Point(Cursor.Position.X-(int)motion.X,Cursor.Position.Y-(int)motion.Y);
            bool frozen=settings.Paused||dragging||menuOpen||hidden||optionsOpen||bubble!=null||(settings.PauseOnHover&&OpaquePoint(local))||now<waveUntil;
            motion.Tick(dt,frozen);
            string key=motion.Walking&&!frozen?(motion.Direction>0?"walk-right":"walk-left"):"idle";
            if(now<waveUntil)key="wave";
            if(key!=animationState){animationTime=0;animationState=key;}else animationTime+=Math.Min(dt,0.1);
            int frame=key=="idle"?IdleIndex(animationTime):(int)(animationTime/(42.0/motion.Speed*.96/art[key].Length))%art[key].Length;
            if(key=="wave")frame=(int)(animationTime/.14)%4;
            if(!hidden){Native.SetWindowPos(Handle,new IntPtr(-1),(int)Math.Round(motion.X),(int)Math.Round(motion.Y),0,0,0x11);moves++;if(key+frame!=lastFrame)Render(key,frame);}
            if(bubble!=null)bubble.Reposition(new Rectangle((int)motion.X,(int)motion.Y,Width,Height),monitor.WorkingArea);
            if(now-saveAt>15){SaveSettings();saveAt=now;}
            if(reminderSmoke)ReminderSmokeTick(now);
            else if(smoke&&now>=8){var report=new {ok=rightRenders>5&&motion.X-initialPosition.X>5,renders=renders,rightRenders=rightRenders,rightFrameCount=art["walk-right"].Length,moves=moves,startX=initialPosition.X,endX=motion.X,windowWidth=Width,windowHeight=Height};File.WriteAllText(Path.Combine(root,"smoke-report.json"),new JavaScriptSerializer().Serialize(report));Close();}
        } catch(Exception e){Log(e.ToString());timer.Stop();MessageBox.Show("散步程序已暂停："+e.Message,"Hydrangea Butterfly Girl");Close();}
    }
    void ReminderSmokeTick(double now) {
        if(reminderStage==0&&now>=1){ShowReminder(true);if(Math.Abs(bubbleUntil-clock.Elapsed.TotalSeconds-300)>.2)throw new Exception("Wrong five minute duration");testReminderX=motion.X;reminderStage=1;}
        if(reminderStage==1&&now>=2){if(motion.X!=testReminderX)throw new Exception("Reminder failed to pause walking");using(var shot=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(root,"reminder-preview.png"),ImageFormat.Png);}if(bubble.Controls.Count!=0)throw new Exception("Unexpected child controls");bubble.TestClick(new Point(30,26));if(bubble!=null||Math.Abs((reminders.NextUtc-DateTime.UtcNow).TotalMinutes-settings.ReminderMinutes)>.05)throw new Exception("Text click dismissal failed");reminderStage=2;}
        if(reminderStage==2&&now>=3){ShowReminder(true);bubble.TestClick(new Point(bubble.Width-25,bubble.Height-40));if(bubble!=null)throw new Exception("Blank area click failed");reminderStage=3;}
        if(reminderStage==3&&now>=4){settings.Paused=true;ShowReminder(true);bubbleUntil=now+.5;reminderStage=4;}
        if(reminderStage==4&&now>=5){if(bubble!=null||!settings.Paused)throw new Exception("Auto-dismiss failed to preserve paused state");settings.Paused=false;reminderStage=5;}
        if(now>=7){bool ok=reminderStage==5&&bubble==null&&remindersShown==3&&renders>5;File.WriteAllText(Path.Combine(root,"reminder-smoke-report.json"),new JavaScriptSerializer().Serialize(new{ok=ok,remindersShown=remindersShown,textAndBlankClickDismiss=true,displaySeconds=ReminderBubble.DisplaySeconds,pausePreserved=reminderStage==5,autoDismissAccelerated=bubble==null,renders=renders}));Close();}
    }
    void Render(string name,int frame) {
        if(name=="walk-right")rightRenders++;
        current=scaled[name][frame];IntPtr screen=Native.GetDC(IntPtr.Zero),dc=Native.CreateCompatibleDC(screen),bitmap=IntPtr.Zero,old=IntPtr.Zero;
        try {bitmap=current.GetHbitmap(Color.FromArgb(0));old=Native.SelectObject(dc,bitmap);var at=new Native.POINT((int)Math.Round(motion.X),(int)Math.Round(motion.Y));var origin=new Native.POINT(0,0);var size=new Native.SIZE(Width,Height);var blend=new Native.BLEND{Op=0,Flags=0,Alpha=255,Format=1};
            if(!Native.UpdateLayeredWindow(Handle,screen,ref at,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());lastFrame=name+frame;renders++;
        } finally {if(old!=IntPtr.Zero)Native.SelectObject(dc,old);if(bitmap!=IntPtr.Zero)Native.DeleteObject(bitmap);Native.DeleteDC(dc);Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();DismissReminder(false,false);SaveSettings();tray.Visible=false;tray.Dispose();notificationIcon.Dispose();applicationIcon.Dispose();menu.Dispose();timer.Dispose();foreach(var frames in art.Values)foreach(var b in frames)b.Dispose();foreach(var frames in scaled.Values)foreach(var b in frames)b.Dispose();base.OnFormClosed(e);}
}

static class Program {
    const string Name="Local\\HydrangeaButterflyGirlWalker-v1";
    [STAThread] static int Main(string[] args) {
        string root=AppDomain.CurrentDomain.BaseDirectory;
        if(Array.IndexOf(args,"--export-right-rig")>=0){try{RightWalkRig.ExportPreview(root);return 0;}catch(Exception e){File.WriteAllText(Path.Combine(root,"rig-export-error.txt"),e.ToString());return 1;}}
        if(Array.IndexOf(args,"--self-test")>=0){try{SelfTest();File.WriteAllText(Path.Combine(root,"self-test-report.txt"),"PASS: boundaries, negative monitor coordinates, paused state, random stops, small work areas, asset decoding; reminder intervals, deferred hidden reminder, no sleep backlog, snooze, acknowledge, disabled reminders, interval limits, legacy settings, bubble bounds.");return 0;}catch(Exception e){File.WriteAllText(Path.Combine(root,"self-test-report.txt"),e.ToString());return 1;}}
        foreach(string command in new[]{"--quit","--pause"})if(Array.IndexOf(args,command)>=0){try{using(var ev=EventWaitHandle.OpenExisting(Name+command))ev.Set();return 0;}catch(WaitHandleCannotBeOpenedException){return 2;}}
        bool fresh;using(var mutex=new Mutex(true,Name,out fresh)){
            if(!fresh){MessageBox.Show("散步程序已经运行。请在系统托盘右键蓝蝶少女图标暂停或退出。","Hydrangea Butterfly Girl");return 0;}
            using(var quit=new EventWaitHandle(false,EventResetMode.AutoReset,Name+"--quit"))using(var pause=new EventWaitHandle(false,EventResetMode.AutoReset,Name+"--pause")){
                try{Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new PetWindow(Array.IndexOf(args,"--smoke-test")>=0,Array.IndexOf(args,"--reminder-smoke-test")>=0,quit,pause));return 0;}
                catch(Exception e){File.WriteAllText(Path.Combine(root,"startup-error.txt"),e.ToString());MessageBox.Show("无法启动散步程序："+e.Message,"Hydrangea Butterfly Girl");return 1;}
            }
        }
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void SelfTest() {
        DateTime t=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);var r=new ReminderSchedule(true,55,t);
        Require(!r.TakeDue(t.AddMinutes(54),true),"Early reminder");Require(!r.TakeDue(t.AddHours(3),false),"Hidden reminder");
        Require(r.TakeDue(t.AddHours(3),true)&&!r.TakeDue(t.AddHours(3),true),"Resume duplicates");
        r.Snooze(t,5);Require(!r.TakeDue(t.AddMinutes(4),true)&&r.TakeDue(t.AddMinutes(5),true),"Snooze interval");
        r.Acknowledge(t);Require(r.NextUtc==t.AddMinutes(55),"Acknowledge interval");
        r.Configure(false,55,t);Require(!r.TakeDue(t.AddDays(2),true),"Disabled reminder");
        r.Configure(true,0,t);Require(r.Minutes==1,"Interval lower bound");r.Configure(true,99999,t);Require(r.Minutes==1440,"Interval upper bound");
        var legacy=new JavaScriptSerializer().Deserialize<WalkerSettings>("{\"Size\":192,\"Paused\":true}");Require(legacy.RemindersEnabled&&legacy.ReminderMinutes==55&&legacy.Paused,"Legacy settings migration");
        foreach(var area in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1920,-180,1920,1080)})foreach(var pet in new[]{new Rectangle(area.Left,area.Top,192,208),new Rectangle(area.Right-192,area.Bottom-208,192,208)})Require(area.Contains(ReminderBubble.Place(pet,area,new Size(324,166))),"Bubble off monitor");
        foreach(var area in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1920,-180,1920,1080),new Rectangle(0,0,100,100)}){
            var m=new WalkMotion(173);m.SetArea(area,192,208);m.X=m.MinX;m.Y=m.MaxY;int moving=0,resting=0,left=0,right=0;
            for(int i=0;i<60000;i++){m.Tick(.033,false);Require(m.X>=m.MinX&&m.X<=m.MaxX,"Escaped horizontal edge");Require(m.Y>=m.MinY&&m.Y<=m.MaxY,"Escaped vertical edge");if(m.Walking){moving++;if(m.Direction>0)right++;else left++;}else resting++;}
            if(area.Width>192)Require(moving>0&&resting>0&&left>0&&right>0,"Missing random stop or direction");
            double x=m.X,rest=m.Rest;bool walking=m.Walking;for(int i=0;i<100;i++)m.Tick(.033,true);Require(x==m.X&&rest==m.Rest&&walking==m.Walking,"Pause changes movement");
            m.X=m.MaxX; m.StartTrip(); if(m.MaxX>m.MinX)Require(m.Direction==-1,"Right edge fails to turn");m.X=m.MinX;m.StartTrip();if(m.MaxX>m.MinX)Require(m.Direction==1,"Left edge fails to turn");
        }
        string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");int count=0;foreach(var file in Directory.GetFiles(assets,"*.png"))using(var b=new Bitmap(file)){Require(b.Width==192&&b.Height==208,"Invalid frame size");Require(b.GetPixel(0,0).A==0,"Opaque background");count++;}Require(count==26,"Missing animation frames");
    }
}
