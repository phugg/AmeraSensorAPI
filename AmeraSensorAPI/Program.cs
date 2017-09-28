using System;
using System.Collections.Generic;
using System.Linq;


namespace AmeraSensorAPI
{
    class Program
    {
        private const string VERSION = "1.0";
        private static List<string> TASKS = new List<string>()
            { "", "LTEmonitor", "LTEsfOccupancy", "LTEsfChest" };
        static void Main(string[] args)
        {
            if (args.Length != 2 && args.Length != 3)
            {
                Console.WriteLine("Amera sensor API (version " + VERSION + ")");
                Console.WriteLine("Usage: AmeraSensorApi DoTask 1stAmeraId,2ndAmeraId,... [HTTP_PROXY]");
                Console.WriteLine("  List of tasks:");
                foreach (string task in TASKS)
                {
                    if (!String.IsNullOrEmpty(task))
                        Console.WriteLine("    {0}-{1}", TASKS.IndexOf(task), task);
                }
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("Example: $ AmeraTasker LTEmonitor 0123af178e7f06b9ee http://127.0.0.1:8888");
                Console.WriteLine("         $ AmeraTasker 1 0123af178e7f06b9ee http://127.0.0.1:8888");
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("The ID for a given Amera sensor can be found using the Amera Web Portal");
                Console.WriteLine("https://manager.meetamera.com/ameras");

                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("Press a Key to exit");
                Console.ReadKey();
                System.Environment.Exit(1);
            }

            // The task to perform
            string DoTask = args[0];
            List<string> tasknames = (from s in TASKS select s.ToLower()).ToList();
            if (!tasknames.Contains(DoTask.ToLower()))
            {
                Console.Error.WriteLine("Error: no task exist! {0}", DoTask);
                System.Environment.Exit(1);
            }

            //target sensor
            HashSet<string> Sensors = new HashSet<string>(args[1].ToLower().Split(',').ToList());

            //optional proxy
            string proxy = null;
            if (args.Length == 3)
                proxy = args[2];

            Console.WriteLine("AmeraSensorAPI task {0} started...", DoTask);
            Console.WriteLine("Sensors: " + String.Join(",", Sensors));

            AmeraTasks ameraTasks = new AmeraTasks(proxy);

            //
            // is logged in
            bool Ok = AmeraTasks.isLoggedIn();
            if (!Ok)
            {
                Console.Error.WriteLine("Error: not logged in!");
                System.Environment.Exit(1);
            }

            //
            // get registered sensors
            HashSet<string> AmeraIds = AmeraTasks.getAmeraIds();
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Number of registered sensors: {0}", AmeraIds.Count);
            //Console.WriteLine(String.Join(",", AmeraIds));

            //
            // get sensor names
            Dictionary<string,string> AmeraNames = AmeraTasks.getAmeraNames();
            //Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Number of named sensors: {0}", AmeraNames.Count);
            //Console.WriteLine("    "+String.Join("\n    ", AmeraNames));

            //
            // get sensor status
            Dictionary<string, string> AmerasStatus = AmeraTasks.getAmerasStatus();
            //Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Number of sensors that report their status: {0}", AmerasStatus.Count);
            //Console.WriteLine("    " + String.Join("\n    ", AmerasStatus));

            //
            // Generate new list (Id, Name, Status)
            List<cAmeraStatus> AmeraStatusList = new List<cAmeraStatus>();
            foreach (string AmeraId in AmeraIds)
            {
                cAmeraStatus AmeraStatus = new cAmeraStatus();
                AmeraStatus.Id = AmeraId;
                if (AmeraNames.ContainsKey(AmeraId))
                    AmeraStatus.Name = AmeraNames[AmeraId];
                else
                    AmeraStatus.Name = "-NotAvail-";
                if (AmerasStatus.ContainsKey(AmeraId))
                    AmeraStatus.Status = AmerasStatus[AmeraId];
                else
                    AmeraStatus.Status = "-NotAvail-";
                AmeraStatusList.Add(AmeraStatus);
            }
            //Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Sensor details: {0}", AmeraStatusList.Count);
            Console.WriteLine("Id  Name  State");
            foreach (cAmeraStatus AmeraStatus in AmeraStatusList.OrderBy(n=>n.Name).ToList())
            {
                Console.WriteLine("{0} {1} {2}", AmeraStatus.Id, AmeraStatus.Name, AmeraStatus.Status);
            }

//#if IGNORE_FOR_NOW
            //
            // do LTE monitor
            bool SensorSelect = AmeraTasks.setAmeraIds(Sensors);
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Status of setAmeraIds:" + SensorSelect.ToString() );
            //TBD Dictionary<string, List<AmeraTasks.LteMonitorReport>> LteMonitorReports = AmeraTasks.doLteMonitor(1);
            Dictionary<string, string> LteMonitorReports = AmeraTasks.doLteMonitor(1);
            Dictionary<string, List<AmeraTasks.FreqCellPair>> FreqCellPairs = AmeraTasks.getFreqCellPairs();
            //#endif

            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Press a Key to exit");
            Console.ReadKey();

            System.Environment.Exit(0);
        }
    }
    class cAmeraStatus
    {
        public string Id;
        public string Name;
        public string Status;
    }
}
