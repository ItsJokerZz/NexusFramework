using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using static OrbisControlAPI.Utilities;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        private static readonly float CurrentVersion = 0.35f;

        private readonly string ConsoleList = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbisControlAPI", "Consoles.cfg");

        public class ConsoleEntry
        {
            public string IP { get; set; }
            public string CustomName { get; set; }
            public string Name { get; set; }
        }

        public List<ConsoleEntry> Consoles { get; private set; } = new List<ConsoleEntry>();
        public ConsoleEntry this[int index] => index >= 0 && index < Consoles.Count ? Consoles[index] : null;

        public enum ConsoleTypes { CEX, KIT, TEST }
        public enum BuzzerModes { Continuous = -1, Stop, Single, Double, Triple }

        public class TargetInfo
        {
            public float Version { get; private set; }
            public string IP { get; private set; } = string.Empty;
            public string Name { get; private set; } = string.Empty;
            public string ConsoleType { get; private set; }
            public float Firmware { get; private set; }
            public int CPUTemp { get; private set; }
            public int SoCTemp { get; private set; }
            public string Username { get; private set; } = string.Empty;
            public bool Connected { get; private set; }
            public bool Attached { get; private set; }

            internal void SetVersion(float version) => Version = version;
            internal void SetIP(string ip) => IP = ip;
            internal void SetName(string name) => Name = name;
            internal void SetConsoleType(string consoleType) => ConsoleType = consoleType;
            internal void SetFirmware(float firmware) => Firmware = firmware;
            internal void SetCPUTemp(int cpuTemp) => CPUTemp = cpuTemp;
            internal void SetSoCTemp(int socTemp) => SoCTemp = socTemp;
            internal void SetUsername(string username) => Username = username;
            internal void SetConnected(bool connected) => Connected = connected;
            internal void SetAttached(bool attached) => Attached = attached;

            internal void Clear()
            {
                Version = Firmware = CPUTemp = SoCTemp = 0;
                IP = Name = ConsoleType = Username = string.Empty;
                Connected = Attached = false;
            }
        }

        public class ProcessInfo
        {
            public class CurrentProcess
            {
                public int PID { get; private set; }
                public string TitleID { get; private set; }
                public string Name { get; private set; }
                public string Region { get; private set; }
                public string Exec { get; private set; }
                public string Executable { get; private set; }
                public string Version { get; private set; }
                public string SDKMinimum { get; private set; }
                public string AppType { get; private set; }
                public string CoverImageURL { get; private set; }

                // public Image CoverImage { get; private set; }
                public void SetPID(int pid) => PID = pid;
                public void SetTitleID(string id) => TitleID = id;
                public void SetName(string name) => Name = name;
                public void SetRegion(string region) => Region = region;
                public void SetExecutable(string exec) => Exec = Executable = exec;
                public void SetVersion(string version) => Version = version;
                public void SetSDKMinimum(string version) => SDKMinimum = version;
                public void SetAppType(string appType) => AppType = appType;
                public void SetCoverImageURL(string url) => CoverImageURL = url;

            }

            public static CurrentProcess Current = new CurrentProcess();

            public static string[] List { get; private set; }

            public static void SetList(string[] list) => List = list;
        }

        public static TargetInfo Target = new TargetInfo();
        public static ProcessInfo Process = new ProcessInfo();

        public string Version => CurrentVersion.ToString("0.00");
        public string Firmware => Target.Firmware.ToString("0.00");
        public string Username => Target.Username;
        public bool Connected => Target.Connected;
        public bool Attached => Target.Attached;

        public class FoundConsole
        {
            public string IP { get; set; }
            public string Firmware { get; set; }
            public string SystemName { get; set; }
            public string OrbisControl { get; set; }
            public string ConsoleType { get; set; }
        }

        public OCAPI() => LoadConsoles();

        private void LoadConsoles()
        {
            Consoles.Clear();
            if (File.Exists(ConsoleList))
                Consoles.AddRange(File.ReadAllLines(ConsoleList)
                    .Select(line => line.Split('|'))
                    .Where(parts => parts.Length >= 1)
                    .Select(parts => new ConsoleEntry
                    {
                        IP = parts[0],
                        CustomName = parts.ElementAtOrDefault(1) ?? "Unnamed",
                        Name = parts.ElementAtOrDefault(2) ?? "Unknown"
                    }));
        }

        private void SaveConsoles() =>
            File.WriteAllLines(ConsoleList, Consoles.Select(c => $"{c.IP}|{c.CustomName}|{c.Name}"));

        private int NumberOfConsole => Consoles.Count;
       
        private Dictionary<string, FoundConsole> 
            FoundConsoles = new Dictionary<string, FoundConsole>();
       
        public async Task FindConsoles(Action<FoundConsole[]> onComplete)
        {
            const int listenPort = 13337;

            using (UdpClient udpListener = new UdpClient(listenPort))
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, listenPort);
                UdpReceiveResult result = await udpListener.ReceiveAsync();

                string consoleIP = result.RemoteEndPoint.Address.ToString();
                if (!FoundConsoles.ContainsKey(consoleIP))
                    FoundConsoles[consoleIP] = new FoundConsole { IP = consoleIP };

                Target.SetIP(consoleIP);
            }

            var json = PerformRequest("setup");
            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, 
                Dictionary<string, Dictionary<string, string>>>>(json);

            var response = jsonObject["DATA"]["RESPONSE"];
            string firmware = response["FW"];
            string systemName = response["NAME"];
            string orbisControl = response["OCAPI"];
            string consoleType = response["TYPE"];

            foreach (var console in FoundConsoles.Values)
            {
                console.Firmware = firmware;
                console.SystemName = systemName;
                console.OrbisControl = orbisControl;
                console.ConsoleType = consoleType;
            }

            onComplete?.Invoke(FoundConsoles.Values.ToArray());
        }

        public void AddConsole(string ip, string customName, string name = "")
        {
            if (string.IsNullOrWhiteSpace(ip) || Consoles.Any(c => c.IP == ip)) return;
            Consoles.Add(new ConsoleEntry { IP = ip, CustomName = customName, Name = name });
            SaveConsoles();
        }

        public void RemoveConsole(string ip)
        {
            var console = Consoles.FirstOrDefault(c => c.IP == ip);
            if (console != null)
            {
                Consoles.Remove(console);
                SaveConsoles();
            }
        }

        public void InjectPayload(string address)
        {
            if (Target.Connected || IsPortOpen(Target.IP)) return;

            Target.SetIP(address);
            Debug.WriteLine(address);

            Utilities.InjectPayload(Target.IP);
        }

        public void GetTargetInfo()
        {
            if (!Target.Connected) return;

            Target.SetVersion(float.TryParse(PerformRequest("version"), out var version) ? version : 0f);
            Target.SetName(PerformRequest("get_console_name"));

            switch (PerformRequest("get_sys_type"))
            {
                case "CEX":
                    Target.SetConsoleType(ConsoleTypes.CEX.ToString());
                    break;
                case "KIT":
                    Target.SetConsoleType(ConsoleTypes.KIT.ToString());
                    break;
                case "TEST":
                    Target.SetConsoleType(ConsoleTypes.TEST.ToString());
                    break;
            }

            Target.SetFirmware(float.TryParse(PerformRequest("get_fw_version"), out var fw) ? fw : 0f);
            Target.SetCPUTemp(int.TryParse(PerformRequest("get_temperature", "type=cpu"), out var cpuTemp) ? cpuTemp : 0);
            Target.SetSoCTemp(int.TryParse(PerformRequest("get_temperature", "type=soc"), out var socTemp) ? socTemp : 0);
            Target.SetConnected(PerformRequest("connect")?.Contains("true") == true);
            Target.SetUsername(PerformRequest("get_username"));
        }

        public void Connect(string address = null)
        {
            if (address != null)
            {
                Target.Clear();
                Target.SetIP(address);
            }
            else if (string.IsNullOrEmpty(Target.IP)) return;

            Target.SetConnected(true);
            PerformRequest("connect");
            GetTargetInfo();
            GetProcessInfo();
        }

        public void Attach(string address = null)
        {
            if (address != null) Target.SetIP(address);
            Target.SetAttached(true);
            PerformRequest("attach");
            GetProcessInfo();
        }

        public void Disconnect(string address = null)
        {
            if (!Target.Connected) return;
            if (address != null) Target.SetIP(address.Trim());
            PerformRequest("disconnect");
            Target.Clear();
        }

        public void Unload(string address = null)
        {
            if (address != null) Target.SetIP(address.Trim());
            if (!IsPortOpen(Target.IP)) return;
            PerformRequest("unload");
            Target.Clear();
        }

        public void Notify(int type = 1, string msg = null)
        {
            if ((!Target.Connected && type != -1) || string.IsNullOrEmpty(msg)) return;
            PerformRequest("send_notify", $"type={type}&msg={msg}");
        }

        public void AlarmBuzzer(BuzzerModes mode)
        {
            if (!Target.Connected) return;
            PerformRequest("ring_buzzer", $"type={(int)mode}");
        }

        public void SetFanThreshold(int limit)
        {
            if (!Target.Connected) return;
            PerformRequest("set_temp_limit", $"limit={limit}");
        }

        public void GetProcessInfo()
        {
            if (!Target.Connected) return;

            var return_string = "pid";
            var pidResponse = PerformRequest("get_proc_info", $"return={return_string}");
            int.TryParse(pidResponse, out int pid);
            ProcessInfo.Current.SetPID(pid);

            return_string = "titleID";
            var titleIdResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetTitleID(titleIdResponse);

            return_string = "name";
            var nameResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetName(nameResponse);

            return_string = "region";
            var regionResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetRegion(regionResponse);

            return_string = "exec";
            var execResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetExecutable(execResponse);

            return_string = "version";
            var versionResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetVersion(versionResponse);

            return_string = "minFW";
            var minFwResponse = PerformRequest("get_proc_info", $"return={return_string}");
            ProcessInfo.Current.SetVersion(minFwResponse);

            GetProcessList();
        }

        public void GetProcessList()
        {
            var json = PerformRequest("get_proc_list");

            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);

            var data = jsonObject["DATA"];
            string[] result = new string[data.Count];
            data.Values.CopyTo(result, 0);

            List<string> list = new List<string>();
            foreach (var item in result)
                list.Add(item);

            ProcessInfo.SetList(list.ToArray());
        }

        public int LoadModule(string path)
        {
            if (!Target.Connected && !Target.Attached) return -1;
            string handle = PerformRequest("load_module", $"path={path}");
            return int.TryParse(handle, out var value) ? value : -1;
        }

        public int LoadModule(string executable, string path, bool searchForExecutable = false)
        {
            if (!Target.Connected) return -1;
            if (!searchForExecutable) return 0;

            string handle = PerformRequest("load_module", $"path={path}&exec={executable}");
            return int.TryParse(handle, out var value) ? value : -1;
        }

        public void LoadPlugin()
        {
            if (!Target.Connected && !Target.Attached) return;
        }


    }
}
