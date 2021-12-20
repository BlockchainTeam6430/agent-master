using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace YerraAgent
{
    class Program
    {

        protected Agent user;
        public System.Threading.Timer aTimer;
        public List<IntPtr> preProcesses;
        public string password = "E546C8DF278CD5931069B522E695D222";
        public string domain = "https://localhost:44398";
        public string companyName = "APPLE";
        static string baseDir = @"C:/yerra";
        public string[] excludesProcesses = { "Idle.exe", "SystemSettings.exe", "TextInputHost.exe", "ApplicationFrameHost.exe", "smBootTime.exe", "Microsoft.Photos.exe", "Monitor.exe", "ScriptedSandbox64.exe" };
        public HttpClient _client;
        private NotifyIcon trayIcon;

        //HubConnection connection;

        static void Main(string[] args)
        {
            try
            {
                new Program();
                Application.Run();
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString());
            }
        }
        
        public Program()
        {
            try
            {
                logger("agent is started!!");
                Stream st;
                System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                st = a.GetManifestResourceStream("YerraAgent.logo.ico");

                this.trayIcon = new NotifyIcon();
                this.trayIcon.Icon = new System.Drawing.Icon(st);
                this.trayIcon.Text = "Yerra Agent";

                this.trayIcon.Click += new EventHandler(m_notifyIcon_Click);
                this.trayIcon.Visible = true;

                _client = new HttpClient();
                _client.BaseAddress = new Uri(domain);
                _client.DefaultRequestHeaders.Accept.Clear();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // connection = new HubConnectionBuilder()
                //.WithUrl(new Uri("https://localhost:443/signals"))
                //.Build();

                // connection.On<Agent>("ResRegisterAgent", (user) =>
                // {
                //     this.user = user;
                //     if (this.user == null) return;

                //     preProcesses = new List<IntPtr>();
                //     aTimer = new System.Threading.Timer((Object param) =>
                //     {
                //         sendRequest();
                //     }, null, 5000, 10000);
                // });

                // connection.On<List<ActionResult>>("ResActions", processes =>
                // {
                //     processes.ForEach(p =>
                //     {
                //         if (processStatus.Keys.Any(k => k.Equals(p.ProcessName)))
                //         {
                //             if (processStatus[p.ProcessName] != p.Action && p.Action == true)
                //             {
                //                 action(p.Action ? "-h" : "-u", p.ProcessName);
                //                 processStatus[p.ProcessName] = p.Action;
                //             }
                //         }
                //     });
                // });

                //string encryptedString = File.ReadAllText(baseDir + "liecense.lie");
                //string orignString = StringCipher.DecryptStringAES(encryptedString, password);

                //string[] words = orignString.Split('*');
                ////this.domain = words[1];
                //this.user.Id = words[0];

                generateAccount();
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "---------started");
            }
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
        }
        ~Program()
        {
            aTimer.Dispose();
        }
        static void logger(string log)
        {
            string path = $"{baseDir}/log.txt";
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(log);
                }
                return;
            }
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(log);
            }
        }

       
        public void action(string command, string processName)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.UseShellExecute = false;
                info.FileName = $"{baseDir}/action.exe";
                info.Arguments = command + " " + processName;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.CreateNoWindow = true;

                Process process = Process.Start(info);
                process.WaitForExit();
                logger(info.FileName + " " + info.Arguments);
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString());
            }
        }

        public async void sendRequest()
        {
            try
            {
                var newProcesses = Process.GetProcesses().Select(p => p.MainWindowHandle).Distinct();
                var strDiffProcessNames = newProcesses.Except(preProcesses);
                var sendProcessList = new List<ProcessInfo>();
                Dictionary<string, bool> processStatus = new Dictionary<string, bool>();

                string path = $"{baseDir}/process-infos.json";
                if (!File.Exists(path))
                {
                    var strProcessStatus = JsonConvert.SerializeObject(processStatus);

                    File.WriteAllText(path, strProcessStatus);
                }

                Dictionary<string, bool> storedProcessStatus = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(path));

                if (strDiffProcessNames.Count() > 0)
                {
                    preProcesses = newProcesses.ToList();
                    sendProcessList = newProcesses.Select(p =>
                    {
                        uint procId = 0;
                        NativeImports.GetWindowThreadProcessId(p, out procId);
                        var proc = Process.GetProcessById((int)procId);
                        string strProName = $"{proc.ProcessName}.exe";
                        
                        return new ProcessInfo(this.user, strProName);
                    }).Where(p => !excludesProcesses.Any(ep => ep == p.Name)).ToList();
                }

                foreach(ProcessInfo pro in sendProcessList)
                {
                    if (!storedProcessStatus.Keys.Any(k => k.Equals(pro.Name)))
                    {
                        storedProcessStatus.Add(pro.Name, false);
                    }
                }

                var res = await _client.PostAsJsonAsync($"api/agent/processes/{this.user.Id}", sendProcessList);
                res.EnsureSuccessStatusCode();
                var readTask = res.Content.ReadAsAsync<List<ActionResult>>();
                List<ActionResult> response = readTask.Result;

                //var client = new RestClient(domain + "/api/agent/processes/" + this.user.Id);
                //var request = new RestRequest(Method.POST).AddJsonBody(sendProcessList);
                //request.AddHeader("Content-Type", "application/json");
                //List<ActionResult> response = await _client.PostAsync<List<ActionResult>>(request);
                
                response.ForEach(p =>
                {
                    if (storedProcessStatus.Keys.Any(k => k.Equals(p.ProcessName)))
                    {
                        storedProcessStatus[p.ProcessName] = p.Action;

                        if (storedProcessStatus[p.ProcessName] != p.Action && p.Action == true)
                        {
                            action(p.Action ? "-h" : "-u", p.ProcessName);
                        }
                    }
                    processStatus[p.ProcessName] = p.Action;
                });

                var strProcessStatusResult = JsonConvert.SerializeObject(processStatus);

                File.WriteAllText(path, strProcessStatusResult);

                //await connection.InvokeAsync("ReqSendProcesses", this.user.Id, sendProcessList);
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "--------send request");
            }
        }

        public async void generateAccount()
        {
            try
            {
                this.user = new Agent();
                this.user.Domain = this.domain;
                this.user.CompanyName = this.companyName;

                var host = Dns.GetHostEntry(Dns.GetHostName());

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        this.user.IpAddress = ip.ToString();
                    }
                }

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    this.user.WinVersion = os["Caption"].ToString();
                    break;
                }

                this.user.SystemName = Environment.UserName.ToString();

                this.user.MachineID = (
                            from nic in NetworkInterface.GetAllNetworkInterfaces()
                            where nic.OperationalStatus == OperationalStatus.Up
                            select nic.GetPhysicalAddress().ToString()
                        ).FirstOrDefault();
                Guid guid = Guid.NewGuid();

                this.user.Id = $"{this.user.CompanyName}-{guid.ToString()}";

                //await connection.StartAsync();

                //await connection.InvokeAsync("ReqRegisterAgent", this.user);

                //var client = new RestClient(domain + "/api/agent");
                //var request = new RestRequest(Method.PUT).AddJsonBody(this.user);
                //request.AddHeader("Content-Type", "application/json");
                //this.user = await client.PutAsync<Agent>(request);

                
                var res = await _client.PostAsJsonAsync("api/agent", this.user);
                res.EnsureSuccessStatusCode();

                var readTask = res.Content.ReadAsAsync<Agent>();
                this.user = readTask.Result;

                if (this.user == null) return;


                preProcesses = new List<IntPtr>();

                aTimer = new System.Threading.Timer((Object param) =>
                {
                    sendRequest();
                }, null, 5000, 10000);

                logger($"generated {domain}");
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "--------generate account");
            }
        }
    }

    public class Agent
    {
        public string Id { get; set; }
        public string SystemName { get; set; }
        public string WinVersion { get; set; }
        public string IpAddress { get; set; }
        public string MachineID { get; set; }
        public long CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string Domain { get; set; }
        public string UniqueId { get; set; }
        public int Status { get; set; }

        public ICollection<ProcessInfo> ProcesseInfos { get; set; }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Target { get; set; }
        public bool IsRead { get; set; }
        public bool Action { get; set; }
        public Agent Agent { get; set; }
        public string AgentId { get; set; }

        public ProcessInfo(Agent agent, string name)
        {
            this.Agent = agent;
            this.AgentId = agent.Id;
            this.Name = name;
            this.IsRead = true;
            this.Target = 0;
            this.Action = false;
        }
    }

    public class ActionResult
    {
        public string ProcessName { get; set; }
        public bool Action { get; set; }
    }

    public class BaseModel
    {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class NativeImports
    {

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }

}
