using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public sealed class TodoSettings {
    public bool Enabled;
    public string Host="127.0.0.1", Username="", ProtectedPassword="";
    public int Port=8080, Count=5;
    public bool Https;
    public string BaseUrl() {
        string host=(Host??"").Trim();
        if(Uri.CheckHostName(host)==UriHostNameType.Unknown || Port<1 || Port>65535)
            throw new Exception("请填写有效的 IP 或域名，以及 1–65535 的端口。");
        return new UriBuilder(Https?"https":"http",host,Port,"api/").Uri.AbsoluteUri;
    }
    public static string Protect(string value) {
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));
    }
    public string Password() {
        try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword),null,DataProtectionScope.CurrentUser));}
        catch{throw new Exception("无法读取保存的密码，请重新打开 Todo 设置并输入密码。");}
    }
}

public sealed class TodoTask {
    public long id;
    public string content;
    public bool is_completed;
}

sealed class TodoApiException : Exception {
    public readonly int Code;
    public TodoApiException(int code,string message):base(message){Code=code;}
}

sealed class TodoClient {
    readonly TodoSettings settings;
    readonly object gate=new object();
    string access="",refresh="";
    public TodoClient(TodoSettings value){settings=value;}
    Dictionary<string,object> Request(string method,string path,object body,bool authorized,CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        var request=(HttpWebRequest)WebRequest.Create(settings.BaseUrl()+path);
        request.Method=method;request.Timeout=15000;request.ReadWriteTimeout=15000;request.AllowAutoRedirect=false;
        request.ContentType="application/json; charset=utf-8";
        if(authorized)request.Headers[HttpRequestHeader.Authorization]="Bearer "+access;
        using(cancel.Register(request.Abort)) {
            try {
                if(body!=null){byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));request.ContentLength=bytes.Length;using(var output=request.GetRequestStream())output.Write(bytes,0,bytes.Length);}
                HttpWebResponse response;
                try{response=(HttpWebResponse)request.GetResponse();}
                catch(WebException e){response=e.Response as HttpWebResponse;if(response==null)throw new Exception("无法连接 Todo 后端，请检查地址、端口和网络。");}
                using(response) {
                    int status=(int)response.StatusCode;
                    if(status==401)throw new TodoApiException(401,"登录已失效或账号密码不正确。");
                    if(status<200||status>=300)throw new TodoApiException(status,"后端返回 HTTP "+status+"。");
                    string json;using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))json=reader.ReadToEnd();
                    Dictionary<string,object> result;
                    try{result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);}
                    catch{throw new Exception("后端返回的内容不是有效的 JSON。");}
                    if(result==null||!result.ContainsKey("code"))throw new Exception("后端响应缺少业务状态码。");
                    int code=Convert.ToInt32(result["code"]);
                    if(code!=0)throw new TodoApiException(code,code==401?"登录已失效或账号密码不正确。":"Todo 操作失败（代码 "+code+"），请重试。");
                    return result;
                }
            }finally{cancel.ThrowIfCancellationRequested();}
        }
    }
    string Token(Dictionary<string,object> result,string name) {
        object data,value;var tokens=result.TryGetValue("data",out data)?data as Dictionary<string,object>:null;
        if(tokens==null||!tokens.TryGetValue(name,out value)||!(value is string)||String.IsNullOrEmpty((string)value))throw new Exception("登录响应缺少凭证。");
        return (string)value;
    }
    void Login(CancellationToken cancel) {
        if(String.IsNullOrWhiteSpace(settings.Username))throw new Exception("请先在托盘 Todo 设置中填写账号密码。");
        var result=Request("POST","auth/login",new{username=settings.Username,password=settings.Password()},false,cancel);
        access=Token(result,"access_token");refresh=Token(result,"refresh_token");
    }
    Dictionary<string,object> Authorized(string method,string path,object body,CancellationToken cancel) {
        if(access.Length==0)Login(cancel);
        try{return Request(method,path,body,true,cancel);}
        catch(TodoApiException e){if(e.Code!=401)throw;}
        try{access=Token(Request("POST","auth/refresh",new{refresh_token=refresh},false,cancel),"access_token");}
        catch(TodoApiException e){if(e.Code!=401)throw;access="";refresh="";Login(cancel);}
        try{return Request(method,path,body,true,cancel);}
        catch(TodoApiException e){if(e.Code==401){access="";refresh="";}throw;}
    }
    public List<TodoTask> GetToday(CancellationToken cancel) {
        lock(gate){return ReadToday(cancel);}
    }
    List<TodoTask> ReadToday(CancellationToken cancel) {
        var result=Authorized("GET","myday",null,cancel);
        if(!result.ContainsKey("data"))throw new Exception("任务响应缺少列表。");
        var serializer=new JavaScriptSerializer();
        var tasks=serializer.Deserialize<List<TodoTask>>(serializer.Serialize(result["data"]));
        if(tasks==null)throw new Exception("任务列表格式错误。");
        foreach(var task in tasks)if(task==null||task.id<=0||task.content==null)throw new Exception("任务数据不完整。");
        return tasks;
    }
    public void Complete(long id,CancellationToken cancel){lock(gate){Authorized("PUT","myday/status",new{task_id=id,is_completed=true},cancel);}}
    public static string ReminderText(List<TodoTask> tasks,int count,Random random) {
        var selected=Select(tasks,count,random);
        if(selected.Count==0)return tasks.Count==0?"今天还没有任务。":"今日任务已全部完成！";
        return String.Join(Environment.NewLine,selected.ConvertAll(t=>t.content).ToArray());
    }
    public static List<TodoTask> Select(List<TodoTask> tasks,int count,Random random) {
        var pending=tasks.FindAll(delegate(TodoTask t){return !t.is_completed;});
        for(int i=pending.Count-1;i>0;i--){int j=random.Next(i+1);var temp=pending[i];pending[i]=pending[j];pending[j]=temp;}
        return pending.GetRange(0,Math.Min(Math.Max(1,count),pending.Count));
    }
}

sealed class TodoOptions : Form {
    readonly TextBox host=new TextBox(),username=new TextBox(),password=new TextBox();
    readonly NumericUpDown port=new NumericUpDown(),count=new NumericUpDown();
    readonly CheckBox enabled=new CheckBox(),https=new CheckBox();
    readonly string savedPassword;
    public TodoSettings Value;
    public TodoOptions(TodoSettings current,bool enable=false) {
        Text="Todo · 账号与提醒设置";Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(460,360);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
        savedPassword=current.ProtectedPassword;
        enabled.Text="启用 Todo 任务提醒";enabled.Checked=enable||current.Enabled;enabled.SetBounds(20,12,300,28);
        AddField("后端 IP / 域名",host,52);host.Text=current.Host;
        AddField("端口",port,94);port.Minimum=1;port.Maximum=65535;port.Value=Math.Max(1,Math.Min(65535,current.Port));
        https.Text="HTTPS";https.Checked=current.Https;https.SetBounds(340,94,100,28);port.Width=180;
        AddField("账号",username,136);username.Text=current.Username;
        AddField("密码",password,178);password.UseSystemPasswordChar=true;
        AddField("每次显示任务数",count,220);count.Minimum=1;count.Maximum=50;count.Value=Math.Max(1,Math.Min(50,current.Count));
        var hint=new Label{Text="密码留空保留已存密码；提醒沿用休息提醒间隔。",Bounds=new Rectangle(20,262,425,25)};
        var save=new Button{Text="保存并应用",Bounds=new Rectangle(195,310,120,32)};
        var cancel=new Button{Text="取消",Bounds=new Rectangle(325,310,110,32),DialogResult=DialogResult.Cancel};
        save.Click+=delegate {
            try {
                var next=new TodoSettings{Enabled=enabled.Checked,Host=host.Text.Trim(),Port=(int)port.Value,Https=https.Checked,Username=username.Text.Trim(),Count=(int)count.Value,ProtectedPassword=password.Text.Length==0?savedPassword:TodoSettings.Protect(password.Text)};
                next.BaseUrl();
                if(next.Username!=current.Username&&password.Text.Length==0&&!String.IsNullOrEmpty(savedPassword))throw new Exception("更换账号时请重新输入密码。");
                if(next.Enabled&&(next.Username.Length==0||String.IsNullOrEmpty(next.ProtectedPassword)))throw new Exception("启用 Todo 前请填写账号和密码。");
                Value=next;DialogResult=DialogResult.OK;
            }catch(Exception e){MessageBox.Show(this,e.Message,"Todo 设置");}
        };
        Controls.AddRange(new Control[]{enabled,https,hint,save,cancel});AcceptButton=save;CancelButton=cancel;
    }
    void AddField(string title,Control input,int y){Controls.Add(new Label{Text=title,Bounds=new Rectangle(20,y+3,135,28)});input.SetBounds(160,y,275,29);Controls.Add(input);}
}

sealed class TodoTasksWindow : Form {
    readonly TodoClient client;

    readonly CancellationTokenSource cancellation=new CancellationTokenSource();
    readonly Label status=new Label();
    readonly FlowLayoutPanel rows=new FlowLayoutPanel();
    readonly Button confirm=new Button(),retry=new Button();
    readonly List<CheckBox> boxes=new List<CheckBox>();
    public bool Busy {get;private set;}

    public TodoTasksWindow(TodoClient api) {
        client=api;
        Text="Todo · 今日任务／勾选完成";Font=new Font("Microsoft YaHei UI",10);ClientSize=new Size(390,340);
        BackColor=Color.FromArgb(249,253,255);TopMost=true;ShowInTaskbar=false;
        FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.CenterScreen;
        MaximizeBox=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;
        status.SetBounds(14,12,360,55);status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        rows.SetBounds(14,72,360,210);rows.FlowDirection=FlowDirection.TopDown;rows.WrapContents=false;rows.AutoScroll=true;
        rows.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        confirm.Text="确认完成所选";confirm.SetBounds(14,295,145,32);confirm.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
        retry.Text="刷新 / 重试";retry.SetBounds(170,295,100,32);retry.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
        var close=new Button{Text="关闭",Bounds=new Rectangle(280,295,95,32),Anchor=AnchorStyles.Bottom|AnchorStyles.Right};
        close.Click+=delegate{Close();};retry.Click+=async delegate{await LoadTasks();};confirm.Click+=async delegate{await CompleteSelected();};
        Controls.AddRange(new Control[]{status,rows,confirm,retry,close});
        Shown+=async delegate{await LoadTasks();};
        FormClosed+=delegate{cancellation.Cancel();};
    }


    void SetBusy(bool value){confirm.Enabled=!value;retry.Enabled=!value;rows.Enabled=!value;Busy=value;}
    void ClearRows(){boxes.Clear();while(rows.Controls.Count>0)rows.Controls[0].Dispose();}
    async Task LoadTasks() {
        if(Busy)return;SetBusy(true);status.Text="正在读取今日未完成任务…";ClearRows();
        try {
            var tasks=await Task.Factory.StartNew(()=>client.GetToday(cancellation.Token),cancellation.Token);
            if(IsDisposed)return;
            var pending=tasks.FindAll(t=>!t.is_completed);
            foreach(var task in pending) {
                var box=new CheckBox{Text=task.content,Tag=task,AutoSize=false,Width=rows.ClientSize.Width-28};
                box.Height=Math.Max(32,TextRenderer.MeasureText(task.content,Font,new Size(box.Width-26,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+12);
                box.UseMnemonic=false;boxes.Add(box);rows.Controls.Add(box);
            }
            status.Text=pending.Count==0?(tasks.Count==0?"今天还没有任务。":"今日任务已全部完成！"):"今日未完成 "+pending.Count+" 条。\n勾选后点击“确认完成所选”同步。";
        }catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)status.Text=e.Message+" 点击刷新重试。";}
        finally{if(!IsDisposed){SetBusy(false);confirm.Enabled=boxes.Count>0;}}
    }
    async Task CompleteSelected() {
        if(Busy)return;
        var selected=boxes.FindAll(b=>b.Enabled&&b.Checked);
        if(selected.Count==0){status.Text="请先勾选已完成的任务。";return;}
        SetBusy(true);int completed=0;
        try {
            // Re-read before writing: another client may already have completed a task.
            var today=await Task.Factory.StartNew(()=>client.GetToday(cancellation.Token),cancellation.Token);
            foreach(var box in selected) {
                if(IsDisposed)return;
                var task=(TodoTask)box.Tag;var latest=today.Find(t=>t.id==task.id);
                if(latest==null)throw new Exception("任务已不在今天的列表中，请刷新后重新选择。");
                if(!latest.is_completed)await Task.Factory.StartNew(()=>client.Complete(task.id,cancellation.Token),cancellation.Token);
                if(IsDisposed)return;
                box.Checked=false;box.Enabled=false;box.Text="已完成 · "+task.content;completed++;
            }
            status.Text="已同步完成 "+completed+" 条任务。可刷新查看剩余任务。";
        }catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)status.Text="已成功 "+completed+" 条。"+e.Message+" 未成功项可再次确认。";}
        finally{if(!IsDisposed){SetBusy(false);confirm.Enabled=boxes.Exists(b=>b.Enabled);}}
    }
}
