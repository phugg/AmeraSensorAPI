using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace AmeraSensorAPI
{

    public class AmeraTasks
    {
        static HttpClient client;
        static Dictionary<string, string> RegisteredAmeraIds = new Dictionary<string, string>();
        static HashSet<string> SelectAmeraIds = new HashSet<string>();

        public AmeraTasks(string proxy)
        {
            HttpClientHandler handler;

            if (proxy != null)
            {
                handler = new HttpClientHandler()
                {
                    Proxy = new System.Net.WebProxy(proxy),
                    UseProxy = true,
                };
            }
            else
            {
                handler = new HttpClientHandler();
            }

            client = new HttpClient(handler);
            client.BaseAddress = new Uri("https://auth.meetamera.com");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("X-API-KEY", "3gmrQtYA7tYwO5d2fbv-a-57X17K7QAEQC43m4prO2A=");
            client.DefaultRequestHeaders.Add("X-USER-EMAIL", "ic.crccscsensors-capteurscsccrc.ic@canada.ca");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            RegisteredAmeraIds = getAmeraNames();
        }

        public static bool setAmeraIds(HashSet<string> AmeraIds)
        {
            List<string> AllAmeraIds = new List<string>(RegisteredAmeraIds.Keys);
            SelectAmeraIds = AmeraIds;
            bool ContainsAllItems = !SelectAmeraIds.Except(AllAmeraIds).Any();
            if (!ContainsAllItems)
            {
                Console.Error.WriteLine("Error: no registered Amera(s)!");
                Console.Error.WriteLine("    " + String.Join(",",SelectAmeraIds.Except(AllAmeraIds)));
            }
            return ContainsAllItems;
        }

        // Login status
        public static bool isLoggedIn()
        {
            Task<string> task = CheckLoginAsync();
            task.Wait();
            string jsonResult = task.Result;
            //Console.WriteLine(jsonResult);
            LoginResponse loginRes = JsonConvert.DeserializeObject<LoginResponse>(jsonResult);
            if (loginRes.message == "logged in")
                return true;
            return false;
        }

        // get list of registered ameras
        public static HashSet<string> getAmeraIds()
        {
            Task<string> task = getAmeraIdsAsync();
            task.Wait();
            string jsonResult = task.Result;
            //Console.WriteLine(jsonResult);
            HashSet<string> Ameras = JsonConvert.DeserializeObject<HashSet<string>>(jsonResult);
            return Ameras;
        }

        public static Dictionary<string,string> getAmeraNames()
        {
            Dictionary<string, string> AmeraNames = new Dictionary<string, string>();
            HashSet<string> AmeraIds = AmeraTasks.getAmeraIds();
            Dictionary<string,Task<string>> Tasks = new Dictionary<string, Task<string>>();
            foreach (string AmeraId in AmeraIds)
            {
                Tasks.Add(AmeraId, getAmeraNamesAsync(AmeraId));
            }
            foreach (string AmeraId in AmeraIds)
            {
                Tasks[AmeraId].Wait();
                string jsonResult = Tasks[AmeraId].Result;
                //Console.WriteLine(jsonResult);
                Dictionary<string,string> AmeraName = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonResult);
                AmeraNames.Add(AmeraId, AmeraName["name"]);
            }
            return AmeraNames;
        }

        private static int SensorTimeoutSecs = 45;
        public static Dictionary<string, string> getAmerasStatus()
        {
            Dictionary<string, string> AmeraStatus = new Dictionary<string, string>();
            HashSet<string> AmeraIds = AmeraTasks.getAmeraIds();

            Task<List<AmeraDeviceStatusReport>> task = getAmerasStatusAsync(AmeraIds);
            task.Wait();
            List<AmeraDeviceStatusReport> reports = task.Result;
            foreach (AmeraDeviceStatusReport report in reports)
            {
                //format time
                //amera reports in epoch microseconds UTC
                //expected: 2016-04-05 19:20:36.0000000
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                double timeDoubleMillis = Double.Parse(report.time) / 1000;
                DateTime time = epoch.AddMilliseconds(timeDoubleMillis);

                //detect sensor offline
                //because 'Connected' always return true for now
                //we detect offline status when the more recent update is more than 45 seconds (2 updates missed)
                //by default, sensor updates each 15 seconds
                DateTime utcNow = DateTime.UtcNow;

                TimeSpan t = utcNow - time;
                string status = (t.TotalSeconds < SensorTimeoutSecs ? "Online" : "Offline");
                AmeraStatus.Add(report.amera, status);
            }
            /**
            Dictionary<string, Task<List<AmeraDeviceStatusReport>>> Tasks = new Dictionary<string, Task<List<AmeraDeviceStatusReport>>>();
            foreach (string AmeraId in AmeraIds)
            {
                Tasks.Add(AmeraId, getAmerasStatusAsync(new HashSet<string>() { AmeraId }));
            }
            foreach (string AmeraId in AmeraIds)
            {
                Tasks[AmeraId].Wait();
                List<AmeraDeviceStatusReport> report = Tasks[AmeraId].Result;
                if (report.Count == 1)
                {
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    double timeDoubleMillis = Double.Parse(report.First().time) / 1000;
                    DateTime time = epoch.AddMilliseconds(timeDoubleMillis);
                    DateTime utcNow = DateTime.UtcNow;

                    TimeSpan t = utcNow - time;
                    string status = (t.TotalSeconds < SensorTimeoutSecs ? "Online" : "Offline");
                    AmeraStatus.Add(report[0].amera, status);
                }
                else
                {
                    Console.WriteLine("Error - No status report replied! {0}",report.Count);
                    AmeraStatus.Add(AmeraId, "-Not Avail-");
                }
            }
            **/
            return AmeraStatus;
        }

        // perform task lte monitor
        //TBD private static Dictionary<string, List<LteMonitorReport>> lteReports;
        private static Dictionary<string, string> lteReports;
        //TBD public static Dictionary<string, List<LteMonitorReport>> doLteMonitor(int count)
        public static Dictionary<string, string> doLteMonitor(int count)
        {
            // Ensure selected amera sensor exist
            if (!SelectAmeraIds.Any())
                return null;

            //-----------------------------
            // Start the LTE monitor task
            //-----------------------------
            Dictionary<string, string> Reports = new Dictionary<string, string>();
            Task<string> task = doLteMonitorAsync(count);
            task.Wait();
            string taskID = task.Result;
            if (string.IsNullOrEmpty(taskID))
                return null;

            //-----------------------------
            // Wait for task to complete
            //-----------------------------
            bool StatusOk = waitForTaskCompletion(Timers["LteMonitor"], taskID);
            if (StatusOk == false)
                return null;

            //-----------------------------
            // Get report
            //-----------------------------
            lteReports = getReport("LteMonitorReportFileName", taskID);
            return lteReports;
        }

        private static Dictionary<string, List<FreqCellPair>> FreqCellPairs;
        public static Dictionary<string, List<FreqCellPair>> getFreqCellPairs()
        {
            // ensure lteReports exist
            if (lteReports.Count == 0)
                return null;

            Dictionary<string, List<FreqCellPair>> FreqCellPairs = new Dictionary<string, List<FreqCellPair>>();
            foreach( KeyValuePair<string, string> jreport in lteReports )
            {
                string AmeraId = jreport.Key;
                JToken jlteMonReport = JToken.Parse(jreport.Value);
                //Console.WriteLine(jlteMonReport.Type);
                if (!jlteMonReport.Type.ToString().Equals("Array"))
                {
                    Console.Error.WriteLine("Error: not an jArray!");
                    System.Environment.Exit(1);
                }

                List<FreqCellPair> FreqCellPair = new List<FreqCellPair>();

                JArray items = (JArray)jlteMonReport;
                Console.WriteLine("Number of items in jlteMonReport is {0}", items.Count);
                foreach (JObject jItem in jlteMonReport)
                {
                    //Console.WriteLine(jItem.Type);
                    if (!jItem.Type.ToString().Equals("Object"))
                    {
                        Console.Error.WriteLine("Error: not an jObject!");
                        System.Environment.Exit(1);
                    }

                    foreach (var pair in jItem)
                    {
                        if (!pair.Key.Equals("data"))
                            continue;
                        //Console.WriteLine(pair.Key);
                        JObject jDataItem = JObject.Parse(pair.Value.ToString());
                        JToken command = null;
                        jDataItem.TryGetValue("cmd_sel", out command);
                        //Console.WriteLine("cmd_sel={0}", command);
                        if (!command.ToString().Equals("sib1"))
                            continue;
                        JToken status = null;
                        jDataItem.TryGetValue("status", out status);
                        //Console.WriteLine("  status={0}", status);
                        //if (!status.ToString().Equals("SUCCESS"))
                        //    continue;
                        int freq = int.Parse(jDataItem.GetValue("freq").ToString());
                        int cell_id = int.Parse(jDataItem.GetValue("cell_id").ToString());
                        string strStatus = status.ToString();
                        FreqCellPair.Add(new FreqCellPair() { freq = freq, cell_id = cell_id, status = strStatus });
                        //Console.WriteLine("   Freq/Cell: {0}/{1} {2}", freq, cell_id, strStatus);
                    }
                }

                Console.WriteLine("Freq/Cell pair entries:");
                Console.WriteLine("   count is {0}", FreqCellPair.Count);
                foreach (FreqCellPair pair in FreqCellPair)
                {
                    Console.WriteLine("   {0}/{1}  {2}", pair.freq, pair.cell_id, pair.status);
                }
                FreqCellPairs[AmeraId] = FreqCellPair;
            }
            return FreqCellPairs;
        }

        //TBD private static Dictionary<string, List<LteMonitorReport>> getReport(string filepath, string taskID)
        private static Dictionary<string, string> getReport(string filepath, string taskID)
        {
            if (SelectAmeraIds.Count == 0 || taskID.Length == 0)
            {
                Console.Error.WriteLine("Error: missing required variables!");
                return null;
            }
            //TBD Dictionary<string, List<LteMonitorReport>> lteReports = new Dictionary<string, List<LteMonitorReport>>();
            Dictionary<string, string> lteReports = new Dictionary<string, string>();
            foreach (string AmeraId in SelectAmeraIds)
            {
                Task<string> task = getReportAsync(AmeraId, taskID);
                task.Wait();
                string report = task.Result;
                //TBD lteReports[AmeraId] = JsonConvert.DeserializeObject<List<LteMonitorReport>>(report);
                lteReports[AmeraId] = report;
            }
            return lteReports;
        }


        // Wait for task completion
        private static Dictionary<string, cTimers> Timers = new Dictionary<string, cTimers>()
        {
            { "LteMonitor",
                new cTimers {
                    TimeOut = 1800,        // 1800 seconds = 30 minutes
                    StatusInterval = 10,   // 10 second
                    WaitForReport = 60,    // 60 seconds = 1 minute
                    TaskProgAvail = true
                }
            },
            { "SfOccupancy",
                new cTimers {
                    TimeOut = 120,         // 120 seconds = 2 minutes
                    StatusInterval = 1,    // 1 second
                    WaitForReport = 10,    // 10 seconds
                    TaskProgAvail = false
                }
            }
        };

        private class cTimers
        {
            public int TimeOut;
            public int StatusInterval;
            public int WaitForReport;
            public bool TaskProgAvail;
        }

        private static bool waitForTaskCompletion(cTimers timing, string taskID)
        {
            bool StatusOk = true;
            //Console.WriteLine("Waiting for task to complete...");
            int Timeout = timing.TimeOut;
            int StatusIntervalMs = timing.StatusInterval * 1000;
            int WaitForReportMs = timing.WaitForReport * 1000;

            Dictionary<string, int> percentageComplete = new Dictionary<string, int>();
            Dictionary<string, bool> isCompleted = new Dictionary<string, bool>();
            Dictionary<string, string> sensorStates = new Dictionary<string, string>();
            bool isAllCompleted = false;

            string sensorStateInit = null;
            if (timing.TaskProgAvail)
                sensorStateInit = "NotAvail";
            foreach (string AmeraId in SelectAmeraIds)
            {
                percentageComplete[AmeraId] = -1;
                isCompleted[AmeraId] = false;
                sensorStates[AmeraId] = sensorStateInit;
            }

            DateTime startTime = DateTime.Now;
            int ElapsedTime = DateTime.Now.Second - startTime.Second;
            while (!isAllCompleted && ElapsedTime < timing.TimeOut)
            {
                Thread.Sleep(StatusIntervalMs);
                string jTaskStatus = getTaskStatus(taskID);
#if DEBUG
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine( "{0} Task Status:",DateTime.UtcNow );
                Console.WriteLine(jTaskStatus);
                string fc = "";
#else
                Console.Write("."); Console.Out.Flush();
                string fc = "\n";
#endif
                isAllCompleted = true;
                JObject jObject = JObject.Parse(jTaskStatus);
                foreach( string AmeraId in SelectAmeraIds)
                {
                    JToken TaskStatusToken = jObject.SelectToken(AmeraId + "." + taskID);
                    ctaskstatus TaskStatus = null;
                    if (TaskStatusToken != null)
                    {
                        string TaskStatusStr = TaskStatusToken.ToString();
                        TaskStatus = JsonConvert.DeserializeObject<ctaskstatus>(TaskStatusStr);
                    }

                    if (TaskStatus != null)
                    {
                        if (TaskStatus.state != null)
                        {
                            sensorStates[AmeraId] = TaskStatus.state;
                            if (sensorStates[AmeraId].Equals("waiting"))
                                Console.WriteLine("{0} {1} {2} is in 'waiting' state", fc, DateTime.UtcNow, AmeraId);
                            if (sensorStates[AmeraId].Equals("done") && isCompleted[AmeraId] == false)
                                Console.WriteLine("{0} {1} {2} is Done (state flag)", fc, DateTime.UtcNow, AmeraId);
                            isCompleted[AmeraId] = sensorStates[AmeraId].Equals("done");
                        }
                        if (TaskStatus.status != null)
                        {
                            if (percentageComplete[AmeraId] != TaskStatus.status.percent)
                            {
                                percentageComplete[AmeraId] = TaskStatus.status.percent;
                                Console.WriteLine("{0} {1} {2} is {3}% complete", fc, DateTime.UtcNow, AmeraId, percentageComplete[AmeraId]);
                            }
                        }
                    }
                    else
                    {
                        if( isCompleted[AmeraId]==false && sensorStates[AmeraId].Equals("NotAvail") )
                        {
                            Console.WriteLine("{0} {1} {2} task progress not available yet!", fc, DateTime.UtcNow, AmeraId);
                            isCompleted[AmeraId] = false;
                        }


                        if (isCompleted[AmeraId] == false && !sensorStates[AmeraId].Equals("NotAvail"))
                        {
                            Console.WriteLine("{0} {1} {2} is Done (Task ended)", fc, DateTime.UtcNow, AmeraId);
                            isCompleted[AmeraId] = true;
                        }
                    }
                    if (isCompleted[AmeraId] == false)
                        isAllCompleted = false;
                }
                ElapsedTime = DateTime.Now.Second - startTime.Second;
            }
            //
            // has Timed out 
            if ( !isAllCompleted && ElapsedTime >= timing.TimeOut )
            {
                StatusOk = false;
                deleteTask(taskID);

                List<string> RunningAmeras = new List<string>();
                foreach (KeyValuePair<string, bool> entry in isCompleted)
                {
                    if (entry.Value == false)
                        RunningAmeras.Add(entry.Key);
                }
                string RunningAmerasStr = string.Join(",", RunningAmeras);
                Console.WriteLine("");
                Console.WriteLine("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                Console.WriteLine("Error: Task has timed out! {0} seconds", ElapsedTime);
                Console.WriteLine("       Sensor(s): {0}", RunningAmerasStr);
                Console.WriteLine("       TaskId: {0} DELETED", taskID);
                Console.WriteLine("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            }
            //
            // all completed
            if( StatusOk )
                Thread.Sleep(WaitForReportMs);
            return StatusOk;
        }
        private static string getTaskStatus(string taskID)
        {
            if (SelectAmeraIds.Count==0 || taskID.Length==0)
            {
                Console.Error.WriteLine("Error: missing required variables!");
                return null;
            }
            Task<string> task = getTaskStatusAsync(taskID);
            task.Wait();
            string jsonResponse = task.Result;
            //Console.WriteLine(jsonResponse);
            //string Response = JsonConvert.DeserializeObject<string>(jsonResponse);
            //return Response;
            return jsonResponse;
        }


        private static string deleteTask(string taskID)
        {
            if (SelectAmeraIds.Count == 0 || taskID.Length == 0)
            {
                Console.Error.WriteLine("Error: missing required variables!");
                return null;
            }
            Task<string> task = deleteTaskAsync(taskID);
            task.Wait();
            string jsonResponse = task.Result;
            Console.WriteLine(jsonResponse);
            //string Response = JsonConvert.DeserializeObject<string>(jsonResponse);
            //return Response;
            return jsonResponse;
        }

        /******************************************************/
        /* Thread classes                                     */
        /******************************************************/

        static async Task<string> CheckLoginAsync()
        {
            HttpResponseMessage httpResMsg = await client.GetAsync("api");
            if (!httpResMsg.IsSuccessStatusCode)
                Console.Error.WriteLine("ERROR: unable to check login status!");

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            //Console.WriteLine(jsonResponse);
            return jsonResponse;
        }

        static async Task<string> getAmeraIdsAsync()
        {
            HttpResponseMessage httpResMsg = await client.GetAsync("api/ameras");
            if (!httpResMsg.IsSuccessStatusCode)
                Console.Error.WriteLine("ERROR: unable acquire list of ameras!");

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            //Console.WriteLine(jsonResponse);
            return jsonResponse;
        }

        static async Task<string> getAmeraNamesAsync(string AmeraId)
        {
            HttpResponseMessage httpResMsg = await client.GetAsync("api/ameras/" + AmeraId + "/name");
            if (!httpResMsg.IsSuccessStatusCode)
                Console.Error.WriteLine("ERROR: unable acquire list of ameras!");

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            //Console.WriteLine(jsonResponse);
            return jsonResponse;
        }

        static async Task<List<AmeraDeviceStatusReport>> getAmerasStatusAsync(HashSet<string> AmeraIds)
        {
            List<AmeraDeviceStatusReport> statusReports = new List<AmeraDeviceStatusReport>();

            //retrieve status for all ameras
            string ameraList = String.Join(",", AmeraIds);
            HttpResponseMessage httpResMsg = await client.GetAsync("api/v1/getlatestreports?ameras=" + ameraList + "&schema=csc.device.status");
            if (!httpResMsg.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("ERROR: unable get sensor status reports!");
                return null;
            }

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            //Console.WriteLine("jReport: {0}", JToken.Parse(jsonResponse).ToString(Formatting.Indented));
            statusReports = JsonConvert.DeserializeObject<List<AmeraDeviceStatusReport>>(jsonResponse);

            return statusReports;
        }

        static async Task<string> doLteMonitorAsync(int count)
        {
            Console.Write(DateTime.UtcNow);
            Console.Write(" Starting LTE monitor for sensors: ");
            Console.WriteLine(String.Join(",", SelectAmeraIds));

            // Build the LTE monitor putBody
            cLteMonitorBody lte_monitor = new cLteMonitorBody
            {
                devices = new c_device_id { device_id = new HashSet<string>(SelectAmeraIds) },
                opts = new c_opts { cmd_sel = "lte_monitor" },
                control = new c_control { count = count },
            };

            string jPutBody = JsonConvert.SerializeObject(lte_monitor);
            //Console.WriteLine("LTEmonitor putbody: {0}", jPutBody);
            HttpResponseMessage httpResMsg = await client.PutAsync("/api/v1/task/rat_lte/create", new StringContent(jPutBody, Encoding.UTF8, "application/json"));
            if (!httpResMsg.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("Error: Unable to task LTE monitor!");
                return null;
            }

            //expected template: {"task":"4ecbb592-f883-438b-9712-5af22b9504fc","devices":["0123af178e7f06b9ee"]}
            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();

            AmeraTaskingResult taskingResult = JsonConvert.DeserializeObject<AmeraTaskingResult>(jsonResponse);
            string taskID = taskingResult.task;
            Console.WriteLine("Task started: Id = " + taskID);
            return taskID;
        }

        static async Task<string> getTaskStatusAsync(string taskID)
        {
            // Build the putBody
            cTaskBody PutBody = new cTaskBody
            {
                devices = new c_device_id { device_id = new HashSet<string>(SelectAmeraIds) },
                task_id = new HashSet<string>() { taskID, },
            };

            string jPutBody = JsonConvert.SerializeObject(PutBody);
            HttpResponseMessage httpResMsg = await client.PutAsync("/api/v1/task/status", new StringContent(jPutBody, Encoding.UTF8, "application/json"));
            if (!httpResMsg.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("Error: Unable to get task status!");
                return null;
            }

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            return jsonResponse;
        }

        static async Task<string> getReportAsync(string AmeraId, string taskID)
        {
            HttpResponseMessage httpResMsg = await client.GetAsync("api/v1/getreports/?ameras=" + AmeraId + "&task=" + taskID + "&delta=8640000");
            if (!httpResMsg.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("ERROR: unable to get the report!");
                return null;
            }

            string jReport = await httpResMsg.Content.ReadAsStringAsync();
            Console.WriteLine("jReport: {0}", JToken.Parse(jReport).ToString(Formatting.Indented));
            return jReport;
        }

        static async Task<string> deleteTaskAsync(string taskID)
        {
            // Build the putBody
            cTaskBody PutBody = new cTaskBody
            {
                devices = new c_device_id { device_id = new HashSet<string>(SelectAmeraIds) },
                task_id = new HashSet<string>() { taskID, },
            };

            string jPutBody = JsonConvert.SerializeObject(PutBody);
            HttpResponseMessage httpResMsg = await client.PutAsync("/api/v1/task/delete", new StringContent(jPutBody, Encoding.UTF8, "application/json"));
            if (!httpResMsg.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("Error: Unable to delete task!");
                return null;
            }

            string jsonResponse = await httpResMsg.Content.ReadAsStringAsync();
            return jsonResponse;
        }

        /******************************************************/
        /* JSON (De)serialization classes                     */
        /******************************************************/

        public class LoginResponse
        {
            public string message;
        }

        public class AmeraDeviceStatusReport
        {
            public string schema;
            public string amera;
            public string data;
            public double latitude;
            public string id;
            public string time;
            public double longitude;
        }

        public class c_device_id
        {
            public HashSet<string> device_id;
        }

        public class c_devices
        {
            public c_device_id devices;
        }
        public class c_opts
        {
            public string cmd_sel;
        }
        public class c_control
        {
            public int count;
        }

        public class cLteMonitorBody
        {
            public c_device_id devices;
            public c_opts opts;
            public c_control control;
        }
        public class cTaskBody
        {
            public c_device_id devices;
            public HashSet<string> task_id;
        }

        public class AmeraTaskingResult
        {
            public string task { get; set; }
            public List<string> devices { get; set; }
        }

        public class ctaskstatus
        {
            public string state;
            public cstatus status;
        }
        public class cstatus
        {
            public long freq_l;
            public long freq_h;
            public int cells;
            public int percent;
        }
        public class LteMonitorReport
        {
            public string schema;
            public string amera;
            public string task;
            public string data;
            public double latitude;
            public string id;
            public object time;
            public double longitude;
        }
        public class FreqCellPair
        {
            public int freq;
            public int cell_id;
            public string status;
        }

        /**
        public class LteMonitorReportData
        {
            public string status;
            public int ntx;
            public double rsrq;
            public int cell_id;
            public int phich_duration;
            public double rssi_dbm;
            public int cp_len;
            public string sib_payload_raw;
            public int sib_crc;
            public int ndlrb;
            public string cmd_sel;
            public int ng;
            public string asn1;
            public double rsrp;
            public double snr;
            public int sib_size;
            public int freq;
            public int nframe;
        }
        **/
    }
}

