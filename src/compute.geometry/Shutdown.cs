using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Hosting;

namespace compute.geometry
{
    class Shutdown
    {
        static System.Threading.Timer _timer;
        internal static Dictionary<int, System.Diagnostics.Process> ParentProcesses { get; set; }
        static int _parentPort = -1;
        static int _idleSpan = -1;
        static DateTime _startTime;

        public static void RegisterParentProcess(int processId)
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            if (ParentProcesses == null)
            {
                ParentProcesses = new Dictionary<int, System.Diagnostics.Process>();
            }
            if (process != null)
                ParentProcesses[processId] = process;
        }

        public static void RegisterStartTime(DateTime dateTime)
        {
            _startTime = dateTime;
        }

        public static void RegisterParentPort(int port)
        {
            _parentPort = port;
        }

        public static void RegisterIdleSpan(int spanSeconds)
        {
            _idleSpan = spanSeconds;
        }

        public static void StartTimer(IHost app)
        {
            bool startTimer = false;
            if (_timer == null && (ParentProcesses != null))
            {
                startTimer = true;
            }
            if (_timer == null && _idleSpan > 0 && _parentPort > 0)
            {
                startTimer = true;
            }

            if (startTimer)
            {
                _timer = new System.Threading.Timer(
                    new System.Threading.TimerCallback(TimerTask), app, 1000, 5000);
            }
        }

        static DateTime _lastSpanCheck = DateTime.Now;

        private async static void TimerTask(object timerState)
        {
            bool shutdown = false;

            if (ParentProcesses != null)
            {
                shutdown = true;
                foreach (var process in ParentProcesses.Values)
                {
                    if (!process.HasExited)
                        shutdown = false;
                }
            }

            if (!shutdown && _parentPort > 0 && _idleSpan > 0)
            {
                var shouldCheckSpan = DateTime.Now - _lastSpanCheck;
                if (shouldCheckSpan.TotalSeconds > _idleSpan)
                {
                    var client = HttpClientFactory.Instance;
                    string url = $"http://localhost:{_parentPort}/idlespan";
                    _lastSpanCheck = DateTime.Now;
                    Serilog.Log.Debug($"The elapsed time has exceeded the idle span limit");
                    Serilog.Log.Debug($"Checking with parent process at {url}.");
                    try
                    {
                        if (!String.IsNullOrEmpty(Config.ApiKey) && !client.DefaultRequestHeaders.Contains("RhinoComputeKey"))
                            client.DefaultRequestHeaders.Add("RhinoComputeKey", Config.ApiKey);
                        HttpResponseMessage response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();    
                        string span = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(span))
                        {
                            int serverIdleSpan = int.Parse(span);
                            Serilog.Log.Debug($"Response received. Parent process idle span is {serverIdleSpan} seconds.");
                            if (serverIdleSpan + (serverIdleSpan * 0.1) > _idleSpan)
                            {
                                Serilog.Log.Debug("Idle span limit reached");
                                shutdown = true;
                            }
                        }
                        else
                        {
                            Serilog.Log.Debug("Idle span value from parent process was null or empty");
                        }
                    }
                    catch(Exception)
                    {
                        Serilog.Log.Debug($"Failed to get response from {url}");
                    }
                }
            }

            if (shutdown)
            {
                Serilog.Log.Debug("Shutting down child process");
                var elapsedTime = DateTime.Now - _startTime;
                Serilog.Log.Information("Total elapsed time for child process is " + string.Format("{0:D2} days, {1:D2} hrs, {2:D2} mins, {3:D2} secs", elapsedTime.Days, elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds));
                var app = timerState as IHost;
                if (app != null)
                    await app.StopAsync();
            }
        }
    }
}
