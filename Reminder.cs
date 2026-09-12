using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class PetIcons {
    public static Icon Load(Size size){
        using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("HydrangeaWalker.Icon")){
            if(stream==null)throw new System.IO.InvalidDataException("Missing embedded pet icon");
            using(var icon=new Icon(stream,size))return (Icon)icon.Clone();
        }
    }
}

// One due time, never a queue: resuming after sleep produces at most one reminder.
public sealed class ReminderSchedule {
    public bool Enabled;
    public int Minutes;
    public DateTime NextUtc, LastUtc;
    public ReminderSchedule(bool enabled,int minutes,DateTime now) { Configure(enabled,minutes,now); }
    public void Configure(bool enabled,int minutes,DateTime now) {
        Enabled=enabled; Minutes=Math.Max(1,Math.Min(1440,minutes)); NextUtc=now.AddMinutes(Minutes);
    }
    public bool TakeDue(DateTime now,bool canShow) {
        if(!Enabled||!canShow||now<NextUtc)return false;
        RecordShown(now);return true;
    }
    public void RecordShown(DateTime now) { LastUtc=now;NextUtc=now.AddMinutes(Minutes); }
    public void Snooze(DateTime now,int minutes) { NextUtc=now.AddMinutes(minutes); }
    public void Acknowledge(DateTime now) { NextUtc=now.AddMinutes(Minutes); }
    public string Status(DateTime now) {
        if(!Enabled)return "提醒已关闭";
        if(now>=NextUtc)return "提醒待显示";
        TimeSpan left=NextUtc-now;
        return "下次提醒："+(int)left.TotalMinutes+"分"+left.Seconds+"秒";
    }
}

sealed class ReminderBubble : Form {
    public event Action<bool> Responded;
    public const double DisplaySeconds=300;
    string message=""; bool tailUp; float tailX=162;
    GraphicsPath outline;
    public ReminderBubble() {
        Text="蓝蝶少女 · 休息提醒";FormBorderStyle=FormBorderStyle.None;
        ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(224,86);
        BackColor=Color.FromArgb(241,249,255);Font=new Font("Microsoft YaHei UI",10);
        DoubleBuffered=true;Cursor=Cursors.Hand;
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80|0x08000000;return p;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=(IntPtr)3;return;}base.WndProc(ref m);}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(Responded!=null)Responded(false);}
    internal void TestClick(Point at){OnMouseClick(new MouseEventArgs(MouseButtons.Left,1,at.X,at.Y,0));}
    void Shape() {
        if(outline!=null)outline.Dispose();outline=new GraphicsPath();
        float top=tailUp?22:2,bottom=tailUp?Height-2:Height-22,right=Width-2,r=18;
        outline.AddArc(2,top,r*2,r*2,180,90);
        if(tailUp){outline.AddLine(26,top,tailX-16,top);outline.AddBezier(tailX-16,top,tailX-10,top-5,tailX+5,2,tailX+10,2);outline.AddBezier(tailX+10,2,tailX+4,14,tailX+8,top,tailX+16,top);}
        outline.AddArc(right-r*2,top,r*2,r*2,270,90);outline.AddArc(right-r*2,bottom-r*2,r*2,r*2,0,90);
        if(!tailUp){outline.AddLine(right-r,bottom,tailX+16,bottom);outline.AddBezier(tailX+16,bottom,tailX+8,bottom,tailX+4,Height-14,tailX+10,Height-2);outline.AddBezier(tailX+10,Height-2,tailX+5,Height-2,tailX-10,bottom+5,tailX-16,bottom);}
        outline.AddArc(2,bottom-r*2,r*2,r*2,90,90);outline.CloseFigure();
        var old=Region;Region=new Region(outline);if(old!=null)old.Dispose();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(outline==null)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var fill=new SolidBrush(Color.FromArgb(249,253,255)))e.Graphics.FillPath(fill,outline);
        using(var pen=new Pen(Color.FromArgb(104,153,198),2))e.Graphics.DrawPath(pen,outline);
        int y=tailUp?35:15;
        TextRenderer.DrawText(e.Graphics,message,Font,new Rectangle(16,y,Width-32,Height-48),Color.FromArgb(45,57,78),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
    }
    protected override void Dispose(bool disposing){if(disposing&&outline!=null){outline.Dispose();outline=null;}base.Dispose(disposing);}
    public static Rectangle Place(Rectangle pet,Rectangle work,Size size) {
        int x=pet.Left+(pet.Width-size.Width)/2,y=pet.Top-size.Height-8;
        if(y<work.Top)y=pet.Bottom+8;
        x=(int)WalkMotion.Clamp(x,work.Left,Math.Max(work.Left,work.Right-size.Width));
        y=(int)WalkMotion.Clamp(y,work.Top,Math.Max(work.Top,work.Bottom-size.Height));
        return new Rectangle(x,y,size.Width,size.Height);
    }
    public void Present(string text,Rectangle pet,Rectangle work){
        message=text;int h=Math.Max(30,TextRenderer.MeasureText(text,Font,new Size(192,600),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4);
        ClientSize=new Size(224,48+h);Reposition(pet,work);Shape();Show();
    }
    public void Reposition(Rectangle pet,Rectangle work){
        Bounds=Place(pet,work,Size);bool up=Top>=pet.Bottom;
        float x=(float)WalkMotion.Clamp(pet.Left+pet.Width/2-Left,60,Width-60);
        if(outline==null||up!=tailUp||x!=tailX){tailUp=up;tailX=x;Shape();}
    }
}

sealed class ReminderOptions : Form {
    readonly Icon windowIcon=PetIcons.Load(new Size(32,32));
    readonly CheckBox enabled=new CheckBox();readonly NumericUpDown minutes=new NumericUpDown();
    readonly TextBox messages=new TextBox();
    public bool RemindersEnabled {get{return enabled.Checked;}}
    public int IntervalMinutes {get{return (int)minutes.Value;}}
    public string[] Messages {get{return messages.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);}}
    public ReminderOptions(bool active,int interval,string[] text) {
        Icon=windowIcon;
        Text="蓝蝶少女 · 提醒设置";FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(410,372);Font=new Font("Microsoft YaHei UI",10);
        enabled.Text="启用休息提醒";enabled.Checked=active;enabled.SetBounds(18,14,220,28);
        var caption=new Label{Text="提醒间隔（分钟）",Bounds=new Rectangle(18,53,200,25)};
        minutes.Minimum=1;minutes.Maximum=1440;minutes.Value=Math.Max(1,Math.Min(1440,interval));minutes.SetBounds(244,50,144,29);
        var hint=new Label{Text="提醒文案（每行一条，随机轮换）",Bounds=new Rectangle(18,94,365,27)};
        messages.Multiline=true;messages.ScrollBars=ScrollBars.Vertical;messages.Text=String.Join(Environment.NewLine,text);messages.SetBounds(18,125,370,173);messages.MaxLength=8000;
        var save=new Button{Text="保存并应用",Bounds=new Rectangle(148,321,125,32)};
        save.Click+=delegate{if(Messages.Length==0){MessageBox.Show(this,"请至少填写一条提醒文案。");return;}foreach(var line in Messages)if(line.Trim().Length==0||line.Length>100){MessageBox.Show(this,"每条文案请填写 1–100 个字符。");return;}DialogResult=DialogResult.OK;};
        var cancel=new Button{Text="取消",Bounds=new Rectangle(283,321,105,32),DialogResult=DialogResult.Cancel};
        Controls.AddRange(new Control[]{enabled,caption,minutes,hint,messages,save,cancel});AcceptButton=save;CancelButton=cancel;
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)windowIcon.Dispose();}
}
