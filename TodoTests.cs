using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Standalone localhost integration tests; never use a real account or backend.
static class TodoTests {
    sealed class Server : IDisposable {
        readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
        readonly Thread worker;
        public Func<string,string,string> Reply;
        public int Port;
        public Exception Failure;
        public Server(){listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;worker=new Thread(Run);worker.IsBackground=true;worker.Start();}
        void Run(){try{while(true)using(var connection=listener.AcceptTcpClient())using(var stream=connection.GetStream()){
            var reader=new StreamReader(stream,Encoding.UTF8);string line=reader.ReadLine(),header;int length=0;
            while(!String.IsNullOrEmpty(header=reader.ReadLine()))if(header.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=Int32.Parse(header.Substring(15).Trim());
            char[] body=new char[length];int read=0;while(read<length){int n=reader.Read(body,read,length-read);if(n==0)break;read+=n;}
            string reply=Reply(line,new string(body));byte[] bytes=Encoding.UTF8.GetBytes(reply);
            byte[] headers=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: "+bytes.Length+"\r\n\r\n");
            stream.Write(headers,0,headers.Length);stream.Write(bytes,0,bytes.Length);
        }}catch(SocketException){}catch(Exception e){Failure=e;}}
        public void Dispose(){listener.Stop();worker.Join(2000);if(Failure!=null)throw Failure;}
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
    static void Pump(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(10);do{Application.DoEvents();Thread.Sleep(10);if(DateTime.UtcNow>until)throw new Exception("UI timeout");}while(!done());}
    const string Login="{\"code\":0,\"data\":{\"access_token\":\"test-access\",\"refresh_token\":\"test-refresh\"}}";
    const string Tasks="{\"code\":0,\"data\":[{\"id\":100,\"content\":\"First task\",\"is_completed\":false},{\"id\":101,\"content\":\"Second task\",\"is_completed\":false},{\"id\":102,\"content\":\"Done\",\"is_completed\":true}]}";
    [STAThread] static int Main(){try{
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa"));ServicePointManager.Expect100Continue=false;
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var old=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<WalkerSettings>("{\"Size\":192}");
        Check(!old.Todo.Enabled&&old.Todo.Count==5,"Legacy defaults");
        var settings=new TodoSettings{Username="test",ProtectedPassword=TodoSettings.Protect("fake-password")};
        Check(settings.Password()=="fake-password"&&!settings.ProtectedPassword.Contains("fake-password"),"Encrypted password roundtrip");
        for(int i=0;i<50;i++){
            var selected=TodoClient.Select(new List<TodoTask>{new TodoTask{id=1},new TodoTask{id=2},new TodoTask{id=3,is_completed=true}},5,new Random(i));
            Check(selected.Count==2&&selected[0].id!=selected[1].id&&!selected.Exists(t=>t.is_completed),"Pending sampling");
        }
        using(var server=new Server()) {
            settings.Port=server.Port;int reads=0,refreshes=0,logins=0,writes=0;
            server.Reply=delegate(string request,string body){
                if(request.Contains("auth/login")){logins++;return Login;}
                if(request.Contains("auth/refresh")){refreshes++;return "{\"code\":0,\"data\":{\"access_token\":\"renewed\"}}";}
                if(request.StartsWith("GET")){reads++;return reads==1?"{\"code\":401}":Tasks;}
                writes++;Check(body.Contains("\"task_id\":100")&&body.Contains("\"is_completed\":true"),"Completion payload");return "{\"code\":0}";
            };
            var client=new TodoClient(settings);Check(client.GetToday(CancellationToken.None).Count==3,"Task parse");
            client.Complete(100,CancellationToken.None);Check(reads==2&&refreshes==1&&logins==1&&writes==1,"Bounded auth refresh");
            server.Reply=(request,body)=>"{\"code\":401}";
            bool rejected=false;try{client.GetToday(CancellationToken.None);}catch(TodoApiException){rejected=true;}Check(rejected,"Invalid auth rejected");
            server.Reply=(request,body)=>request.Contains("auth/login")?Login:"not-json";
            rejected=false;try{new TodoClient(settings).GetToday(CancellationToken.None);}catch(Exception){rejected=true;}Check(rejected,"Invalid JSON rejected");

            // Verify partial completion preserves only failed checks and retry succeeds.
            writes=0;bool failSecond=true;var doneIds=new List<long>();
            server.Reply=delegate(string request,string body){
                if(request.Contains("auth/login"))return Login;
                if(request.StartsWith("GET"))return Tasks;
                writes++;if(writes==2&&failSecond)return "{\"code\":500}";
                doneIds.Add(body.Contains("100")?100:101);return "{\"code\":0}";
            };
            using(var window=new TodoTasksWindow(new TodoClient(settings))){
                window.Show();Pump(()=>!window.Busy);
                var boxes=Field<List<CheckBox>>(window,"boxes");Check(boxes.Count==2,"UI pending list");
                foreach(var box in boxes)box.Checked=true;
                Field<Button>(window,"confirm").PerformClick();Pump(()=>!window.Busy);
                Check(writes==2&&boxes.FindAll(b=>b.Enabled&&b.Checked).Count==1,"Partial failure preserves selection");
                failSecond=false;Field<Button>(window,"confirm").PerformClick();Pump(()=>!window.Busy);
                Check(writes==3&&doneIds.Count==2&&doneIds[0]!=doneIds[1],"Retry only failed item");
                window.Close();
            }
            server.Reply=(request,body)=>request.Contains("auth/login")?Login:"{\"code\":0,\"data\":[{\"id\":1,\"content\":\"喝水并活动身体，休息一下眼睛\",\"is_completed\":false},{\"id\":2,\"content\":\"阅读二十分钟，整理今天学习到的新知识\",\"is_completed\":false},{\"id\":3,\"content\":\"检查今天的计划并安排下一步工作\",\"is_completed\":false},{\"id\":4,\"content\":\"完成每日练习\",\"is_completed\":false},{\"id\":5,\"content\":\"记录今天的收获\",\"is_completed\":false}]}";
            using(var window=new TodoTasksWindow(new TodoClient(settings))){
                window.Show();Pump(()=>!window.Busy);using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","todo-preview.png"));}window.Close();
            }
            // The real pet still uses ReminderBubble: clicks only dismiss, never submit tasks.
            server.Reply=(request,body)=>request.Contains("auth/login")?Login:Tasks;
            using(var quit=new EventWaitHandle(false,EventResetMode.AutoReset))
            using(var pause=new EventWaitHandle(false,EventResetMode.AutoReset))
            using(var pet=new PetWindow(true,false,quit,pause)) {
                var configuration=Field<WalkerSettings>(pet,"settings");configuration.Todo=settings;configuration.Todo.Enabled=true;
                pet.GetType().GetField("todoClient",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,new TodoClient(settings));
                pet.Show();Application.DoEvents();
                pet.GetType().GetMethod("ReadTodoReminder",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                Pump(()=>Field<ReminderBubble>(pet,"bubble")!=null);
                var bubble=Field<ReminderBubble>(pet,"bubble");
                Check(bubble.Controls.Count==0,"Todo bubble must have no controls");
                string message=Field<string>(bubble,"message");
                Check(message.Contains("First task")&&message.Contains("Second task")&&!message.Contains("Done"),"Bubble contains only pending task contents");
                using(var screenshot=new Bitmap(bubble.Width,bubble.Height)){bubble.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));screenshot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","todo-bubble-preview.png"));}
                bubble.TestClick(new Point(30,26));Check(Field<ReminderBubble>(pet,"bubble")==null,"Task text click dismisses");
                var schedule=Field<ReminderSchedule>(pet,"reminders");Check(Math.Abs((schedule.NextUtc-DateTime.UtcNow).TotalMinutes-configuration.ReminderMinutes)<0.05,"Dismiss reschedules normally");
                pet.GetType().GetMethod("ReadTodoReminder",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                Pump(()=>Field<ReminderBubble>(pet,"bubble")!=null);
                bubble=Field<ReminderBubble>(pet,"bubble");bubble.TestClick(new Point(bubble.Width-25,bubble.Height-40));Check(Field<ReminderBubble>(pet,"bubble")==null,"Task blank click dismisses");
                pet.Close();
            }
            Check(TodoClient.ReminderText(new List<TodoTask>(),5,new Random())=="今天还没有任务。","Empty day");
            Check(TodoClient.ReminderText(new List<TodoTask>{new TodoTask{id=1,is_completed=true}},5,new Random())=="今日任务已全部完成！","Completed day");
            var allPending=new List<TodoTask>();for(int i=1;i<=8;i++)allPending.Add(new TodoTask{id=i,content="Task "+i});
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            server.Reply=(request,body)=>request.Contains("auth/login")?Login:serializer.Serialize(new{code=0,data=allPending});
            using(var manager=new TodoTasksWindow(new TodoClient(settings))){manager.Show();Pump(()=>!manager.Busy);Check(Field<List<CheckBox>>(manager,"boxes").Count==8,"Tray manager lists all tasks, independent of reminder count");manager.Close();}
            using(var options=new TodoOptions(settings)){
                options.Show();Application.DoEvents();using(var image=new Bitmap(options.Width,options.Height)){options.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","todo-settings-preview.png"));}options.Close();
            }
        }
        Console.WriteLine("PASS: legacy defaults, DPAPI, pending sampling, login/refresh, auth rejection, invalid JSON, completion payload, UI partial failure and retry, original bubble content/clicks/reschedule, empty and completed day, full tray checklist, previews.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
