using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using murrayju.ProcessExtensions;
using System.Timers;

namespace ServiceYerra
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        //private Timer aTimer;

        protected override void OnStart(string[] args)
        {
            try
            {
                logger("started service");
                Action();
                //aTimer = new System.Timers.Timer(10000);
                //aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                //aTimer.Interval = 60000;
                //aTimer.Enabled = true;
            }
            catch (Exception evt)
            {
                logger(evt.Message.ToString() + "---start");
            }
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Process[] pname = Process.GetProcessesByName("YerrAgent");
            if (pname.Length == 0)
                Action();
        }

        protected override void OnStop()
        {
            logger("yerra service is Stopped");
        }

        protected void logger(string log)
        {
            string path = @"C:/yerra/log.txt";
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(log);
                }
            }
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(log);
            }
        }

        protected void Action()
        {
            try
            {
                ProcessExtensions.StartProcessAsCurrentUser(@"C:\yerra\YerraAgentApp.exe", null, null, false);
            }
            catch (Exception e)
            {
                logger(e.Message.ToString());
            }
        }

     
    }

  
}
