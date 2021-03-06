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
using Newtonsoft.Json;
using Microsoft.Win32;

namespace YerraAgent
{
    class Program
    {

        protected Agent user;
        public System.Threading.Timer actionTimer;
        public System.Threading.Timer checkAppStateTimer;
        public List<IntPtr> preProcesses;
        public string password = "E546C8DF278CD5931069B522E695D222";
        public string domain = "https://localhost:44398";
        public string companyName = "APPLE";
        static string baseDir = @"C:/yerra";
        static int actionDuration = 15000;
        static int checkStateDuration = 6000;
        public HttpClient _client;
        private NotifyIcon trayIcon;

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
                generateAccount();
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "---------started");
            }
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            var agentWindow = new AgentWindow();
            agentWindow.setAgentID(this.user.Id);
            agentWindow.Show();
        }
        ~Program()
        {
            actionTimer.Dispose();
            checkAppStateTimer.Dispose();
            turnOff();
        }

        static void logger(string log)
        {
            try
            {
                string path = $"{baseDir}/log.txt";
                if (!File.Exists(path))
                {
                    if (!Directory.Exists(baseDir))
                    {
                        Directory.CreateDirectory(baseDir);
                    }
                    using (StreamWriter sw = File.CreateText(path))
                    {
                        sw.WriteLine(log);
                        sw.Close();
                    }

                    return;
                }
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(log);
                    sw.Close();
                }
            }
            catch(Exception evt)
            {

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

                RegistryKey key = Registry.CurrentUser.OpenSubKey("AgentAppInformation", true);
                if (key == null) return;
                var isInstalled = key.GetValue("IsInstalled").ToString();
                if (Int32.Parse(isInstalled) != 1) return;

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

                 
                foreach(ActionResult p in response)
                {
                    if (storedProcessStatus.Keys.Any(k => k.Equals(p.ProcessName)))
                    {

                        if (storedProcessStatus[p.ProcessName] != p.Action && p.Action == true)
                        {
                            action(p.Action ? "-h" : "-u", p.ProcessName);
                        }

                    }
                    processStatus[p.ProcessName] = p.Action;
                }

                var strProcessStatusResult = JsonConvert.SerializeObject(processStatus);

                File.WriteAllText(path, strProcessStatusResult);

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

                string[] splitedIds = { "", "", "" };
                for(int i = 0; i<this.user.MachineID.Length; i++)
                {
                    splitedIds[i/4] += this.user.MachineID[i];
                    
                }

                this.user.Id = $"{this.user.CompanyName}-{splitedIds[0]}-{splitedIds[1]}-{splitedIds[2]}";

                var res = await _client.PostAsJsonAsync("api/agent", this.user);
                res.EnsureSuccessStatusCode();

                var readTask = res.Content.ReadAsAsync<Agent>();
                this.user = readTask.Result;

                if (this.user == null) return;

                setAppState(0);

                preProcesses = new List<IntPtr>();

                actionTimer = new System.Threading.Timer((Object param) =>
                {
                    sendRequest();
                }, null, 5000, actionDuration);

                checkAppStateTimer = new System.Threading.Timer((Object param) =>
                {
                    checkAppState();

                }, null, 5000, checkStateDuration);

            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "--------generate account");
            }
        }

        async public void checkAppState()
        {
            try
            {
                var res = await _client.GetAsync($"api/agent/checkstate/{this.user.Id}");
                res.EnsureSuccessStatusCode();
                var readTask = res.Content.ReadAsAsync<int>();
                int state = readTask.Result;
                switch (state)
                {
                    case 0:
                        setAppState(0);
                        break;
                    case 1:
                        setAppState(1);
                        break;
                    case 2:
                        setAppState(0);
                        break;
                    case 3:
                        setAppState(2);
                        var process = new Process();
                        var startInfo = new ProcessStartInfo();
                        startInfo.WorkingDirectory = @"C:\Windows\System32";
                        startInfo.UseShellExecute = true;
                        startInfo.CreateNoWindow = true;
                        startInfo.FileName = "cmd.exe";
                        string killservice = "/c D: & installer uninstall";
                        startInfo.Arguments = killservice;
                        startInfo.Verb = "runas";
                        process.StartInfo = startInfo;
                        process.Start();
                        process.WaitForExit();

                        break;
                }
            }catch(Exception evt)
            {
                logger($"{evt.Message.ToString()}------check state issues");
            }
            
        }

        async public void turnOff()
        {
            await _client.GetAsync($"api/agent/turnoff/{this.user.Id}");
        }

        public void setAppState(int state)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("AgentAppInformation", true);
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey("AgentAppInformation");
                key.SetValue("IsInstalled", state);
                return;
            }
            key.SetValue("IsInstalled", state);
        }

        public string[] excludesProcesses = { "Idle.exe", "SystemSettings.exe", "TextInputHost.exe", "ApplicationFrameHost.exe", "smBootTime.exe", "Microsoft.Photos.exe", "Monitor.exe", "ScriptedSandbox64.exe" };

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
