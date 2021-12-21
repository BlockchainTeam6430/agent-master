using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

namespace YerraPro.ViewModel
{
    public class RegisterVM : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public Agent User { get; set; }
        public HttpClient _client;
        public ObservableCollection<ProcessInfo> ProcessInfos { get; set; }
        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

  
        public RegisterVM()
        {
            //_client = new HttpClient();
            //_client.BaseAddress = new Uri("https://localhost:44349");
            //_client.DefaultRequestHeaders.Accept.Clear();
            //_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            AddCommand = new RelayCommand(o => AddClick(o));
            RemoveCommand = new RelayCommand(o => RemoveClick(o));
            UploadCommand = new RelayCommand(o => UploadClick(o));
            InstallCommand = new RelayCommand(o => InstallClick(o));

            Process[] allProcesses = Process.GetProcesses();
            Datasource = new ObservableCollection<Process>();

            List<ProcessInfo> runProcesses = new List<ProcessInfo>();

            foreach (Process p in allProcesses)
            {
                if (!Datasource.Any(ap => ap.ProcessName == p.ProcessName))
                {
                    Datasource.Add(p);
                }
            }

            //filteredProcesses.ForEach(p =>
            //{
            //    uint procId = 0;
            //    NativeImports.GetWindowThreadProcessId(p.MainWindowHandle, out procId);
            //    var proc = Process.GetProcessById((int)procId);
            //    string strProName = proc.ProcessName + ".exe";
            //    runProcesses.Add(new ProcessInfo() { Name = strProName });
            //});

            //this.ProcessInfos = new ObservableCollection<ProcessInfo>();
            //datasource.ToList().ForEach(d =>
            //{
            //    uint procId = 0;
            //    NativeImports.GetWindowThreadProcessId(d.MainWindowHandle, out procId);
            //    var proc = Process.GetProcessById((int)procId);
            //    string strProName = proc.ProcessName + ".exe";

            //    ProcessInfos.Add(new ProcessInfo() { Name = strProName, Id = d.Id });
            //});

            //generateAccount();

        }

        public async void generateAccount()
        {
            this.User = new Agent();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
             
                string tempMac = nic.GetPhysicalAddress().ToString();
                if (nic.Speed > -1 &&
                    !string.IsNullOrEmpty(tempMac) &&
                    tempMac.Length >= 12)
                {
                    this.User.MachineID = tempMac;
                }
            }

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    this.User.IpAddress = ip.ToString();
                }
            }

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                User.WinVersion = os["Caption"].ToString();
                break;
            }

            this.User.MachineID = System.Environment.UserName;

            var res = await _client.PostAsJsonAsync("api/agent", this.User);
            res.EnsureSuccessStatusCode();
            var readTask = res.Content.ReadAsAsync<Agent>();
            var resultAgent = readTask.Result;
            this.User = resultAgent;
        }

        public string Name { get; set; }

        public ICommand AddCommand { get; set; }
        private void AddClick(object sender)
        {
            if (selectedProcess == null) return;
            if (selectedData.FirstOrDefault(d => d.Id == selectedProcess.Id) == null)
                selectedData.Add(selectedProcess);
        }
        public ICommand RemoveCommand { get; set; }
        private void RemoveClick(object sender)
        {
            if (selectedRProcess == null) return;
            if(selectedData.FirstOrDefault(d => d.Id == selectedRProcess.Id) != null)
                SelectedData = new ObservableCollection<Process>(SelectedData.Where(s => s.Id != selectedRProcess.Id)) ;
        }

        public ICommand UploadCommand { get; set; }
        private void UploadClick(object sender)
        {
            if(SelectedData.Count > 0)
            {
                var result = selectedData.Select(s => new ProcessInfo() { Name = s.ProcessName, Agent = User });
                _client.PostAsJsonAsync("/api/agent/RegisterProcess", result);
            }
        }

        public ICommand InstallCommand { get; set; }
        private void InstallClick(object sender)
        {
            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = @"C:\Windows\System32";
            startInfo.UseShellExecute = true;
            startInfo.CreateNoWindow = true;
            startInfo.FileName = "cmd.exe";
            string killservice = "/c E: & installer uninstall";
            startInfo.Arguments = killservice;
            startInfo.Verb = "runas";
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            //RegistryKey rk = Registry.CurrentUser.CreateSubKey("YerraServiceApp");
            //int check = Int32.Parse(rk.GetValue("userId").ToString());
            //if (check <= 0) return;

            //if (ServiceInstaller.ServiceIsInstalled("UnvisibleService"))
            //{
            //    ServiceInstaller.Uninstall("UnvisibleService");
            //    DirectoryDelete(@"C:/yerra");
            //    return;
            //}else
            //{
            //    DirectoryCopy(@"D:\Work\C#\ServiceYerra\ServiceYerra\bin\Debug", @"C:/yerra", true);

            //    ServiceInstaller.InstallAndStart("UnvisibleService", "YerraService", @"C:/yerra/ServiceYerra.exe");
            //    ServiceInstaller.StartService("UnvisibleService");

            //}
        }

        public string searchKey = "";
        public string SearchKey
        {
            get => searchKey;
            set
            {
                searchKey = value;
                this.OnPropertyChanged("SearchKey");
                filteredData = new ObservableCollection<Process>(datasource.Where(d => d.ProcessName.Contains(searchKey)));
                this.OnPropertyChanged("FilteredData");
            }
        }
        public ObservableCollection<Process> datasource = new ObservableCollection<Process>();
        public ObservableCollection<Process> Datasource
        {
            get => datasource;
            set
            {
                datasource = value;
                this.OnPropertyChanged("Datasource");
            }
        }

        public ObservableCollection<Process> filteredData = new ObservableCollection<Process>();
        public ObservableCollection<Process> FilteredData
        {
            get
            {
                return filteredData;
            }
            set
            {
                filteredData = value;
                this.OnPropertyChanged("FilteredData");
            }
        }

        public ObservableCollection<Process> selectedData = new ObservableCollection<Process>();
        public ObservableCollection<Process> SelectedData
        {
            get
            {
                return selectedData;
            }
            set
            {
                selectedData = value;
                this.OnPropertyChanged("SelectedData");
            }
        }
        public Process selectedProcess { get; set; }
        public Process selectedRProcess { get; set; }

  
       
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo desDir = new DirectoryInfo(destDirName);
            if (desDir.Exists)
            {
                return;
            }
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        private void DirectoryDelete(string sourceDirName)
        {
            string[] filePaths = Directory.GetFiles(sourceDirName);
            foreach (string filePath in filePaths)
                File.Delete(filePath);
            Directory.Delete(sourceDirName);
        }
    }

    public class Agent
    {
        public string Id { get; set; }
        public string SystemName { get; set; }
        public string WinVersion { get; set; }
        public string IpAddress { get; set; }
        public string MachineID { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LicensedAt { get; set; }

        public ICollection<ActionLogger> ActionLoggers { get; set; }
        public ICollection<ProcessInfo> ProcesseInfos { get; set; }
    }

    public class ActionLogger
    {
        public int Id { get; set; }
        public bool Type { get; set; }
        public DateTime Time { get; set; }
        public bool IsRead { get; set; }
        public ProcessInfo ProcessInfo { get; set; }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Agent Agent { get; set; }
    }

    public static class ServiceInstaller
    {
        private const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
        private const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        private class SERVICE_STATUS
        {
            public int dwServiceType = 0;
            public ServiceState dwCurrentState = 0;
            public int dwControlsAccepted = 0;
            public int dwWin32ExitCode = 0;
            public int dwServiceSpecificExitCode = 0;
            public int dwCheckPoint = 0;
            public int dwWaitHint = 0;
        }

        #region OpenSCManager
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr OpenSCManager(string machineName, string databaseName, ScmAccessRights dwDesiredAccess);
        #endregion

        #region OpenService
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);
        #endregion

        #region CreateService
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);
        #endregion

        #region CloseServiceHandle
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseServiceHandle(IntPtr hSCObject);
        #endregion

        #region QueryServiceStatus
        [DllImport("advapi32.dll")]
        private static extern int QueryServiceStatus(IntPtr hService, SERVICE_STATUS lpServiceStatus);
        #endregion

        #region DeleteService
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);
        #endregion

        #region ControlService
        [DllImport("advapi32.dll")]
        private static extern int ControlService(IntPtr hService, ServiceControl dwControl, SERVICE_STATUS lpServiceStatus);
        #endregion

        #region StartService
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);
        #endregion

        public static void Uninstall(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.AllAccess);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.AllAccess);
                if (service == IntPtr.Zero)
                    throw new ApplicationException("Service not installed.");

                try
                {
                    StopService(service);
                    if (!DeleteService(service))
                        throw new ApplicationException("Could not delete service " + Marshal.GetLastWin32Error());
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public static bool ServiceIsInstalled(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);

                if (service == IntPtr.Zero)
                    return false;

                CloseServiceHandle(service);
                return true;
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public static void InstallAndStart(string serviceName, string displayName, string fileName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.AllAccess);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.AllAccess);

                if (service == IntPtr.Zero)
                    service = CreateService(scm, serviceName, displayName, ServiceAccessRights.AllAccess, SERVICE_WIN32_OWN_PROCESS, ServiceBootFlag.AutoStart, ServiceError.Normal, fileName, null, IntPtr.Zero, null, null, null);

                if (service == IntPtr.Zero)
                    throw new ApplicationException("Failed to install service.");

                try
                {
                    StartService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public static void StartService(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Start);
                if (service == IntPtr.Zero)
                    throw new ApplicationException("Could not open service.");

                try
                {
                    StartService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public static void StopService(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Stop);
                if (service == IntPtr.Zero)
                    throw new ApplicationException("Could not open service.");

                try
                {
                    StopService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        private static void StartService(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            StartService(service, 0, 0);
            var changedStatus = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Running);
            if (!changedStatus)
                return;
        }

        private static void StopService(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            ControlService(service, ServiceControl.Stop, status);
            var changedStatus = WaitForServiceStatus(service, ServiceState.StopPending, ServiceState.Stopped);
            if (!changedStatus)
                throw new ApplicationException("Unable to stop service");
        }

        public static ServiceState GetServiceStatus(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
                if (service == IntPtr.Zero)
                    return ServiceState.NotFound;

                try
                {
                    return GetServiceStatus(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        private static ServiceState GetServiceStatus(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            if (QueryServiceStatus(service, status) == 0)
                throw new ApplicationException("Failed to query service status.");

            return status.dwCurrentState;
        }

        private static bool WaitForServiceStatus(IntPtr service, ServiceState waitStatus, ServiceState desiredStatus)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            QueryServiceStatus(service, status);
            if (status.dwCurrentState == desiredStatus) return true;

            int dwStartTickCount = Environment.TickCount;
            int dwOldCheckPoint = status.dwCheckPoint;

            while (status.dwCurrentState == waitStatus)
            {
                // Do not wait longer than the wait hint. A good interval is
                // one tenth the wait hint, but no less than 1 second and no
                // more than 10 seconds.

                int dwWaitTime = status.dwWaitHint / 10;

                if (dwWaitTime < 1000) dwWaitTime = 1000;
                else if (dwWaitTime > 10000) dwWaitTime = 10000;

                Thread.Sleep(dwWaitTime);

                // Check the status again.

                if (QueryServiceStatus(service, status) == 0) break;

                if (status.dwCheckPoint > dwOldCheckPoint)
                {
                    // The service is making progress.
                    dwStartTickCount = Environment.TickCount;
                    dwOldCheckPoint = status.dwCheckPoint;
                }
                else
                {
                    if (Environment.TickCount - dwStartTickCount > status.dwWaitHint)
                    {
                        // No progress made within the wait hint
                        break;
                    }
                }
            }
            return (status.dwCurrentState == desiredStatus);
        }

        private static IntPtr OpenSCManager(ScmAccessRights rights)
        {
            IntPtr scm = OpenSCManager(null, null, rights);
            if (scm == IntPtr.Zero)
                throw new ApplicationException("Could not connect to service control manager.");

            return scm;
        }
    }


    public enum ServiceState
    {
        Unknown = -1, // The state cannot be (has not been) retrieved.
        NotFound = 0, // The service is not known on the host server.
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7
    }

    [Flags]
    public enum ScmAccessRights
    {
        Connect = 0x0001,
        CreateService = 0x0002,
        EnumerateService = 0x0004,
        Lock = 0x0008,
        QueryLockStatus = 0x0010,
        ModifyBootConfig = 0x0020,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | Connect | CreateService |
                     EnumerateService | Lock | QueryLockStatus | ModifyBootConfig)
    }

    [Flags]
    public enum ServiceAccessRights
    {
        QueryConfig = 0x1,
        ChangeConfig = 0x2,
        QueryStatus = 0x4,
        EnumerateDependants = 0x8,
        Start = 0x10,
        Stop = 0x20,
        PauseContinue = 0x40,
        Interrogate = 0x80,
        UserDefinedControl = 0x100,
        Delete = 0x00010000,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | QueryConfig | ChangeConfig |
                     QueryStatus | EnumerateDependants | Start | Stop | PauseContinue |
                     Interrogate | UserDefinedControl)
    }

    public enum ServiceBootFlag
    {
        Start = 0x00000000,
        SystemStart = 0x00000001,
        AutoStart = 0x00000002,
        DemandStart = 0x00000003,
        Disabled = 0x00000004
    }

    public enum ServiceControl
    {
        Stop = 0x00000001,
        Pause = 0x00000002,
        Continue = 0x00000003,
        Interrogate = 0x00000004,
        Shutdown = 0x00000005,
        ParamChange = 0x00000006,
        NetBindAdd = 0x00000007,
        NetBindRemove = 0x00000008,
        NetBindEnable = 0x00000009,
        NetBindDisable = 0x0000000A
    }

    public enum ServiceError
    {
        Ignore = 0x00000000,
        Normal = 0x00000001,
        Severe = 0x00000002,
        Critical = 0x00000003
    }


    public static class NativeImports
    {

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);


    }
}
