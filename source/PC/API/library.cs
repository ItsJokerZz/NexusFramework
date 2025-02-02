using System;
using System.Web.UI.WebControls;
using static OrbisControlAPI.Utilities;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        private static readonly float CurrentVersion = 0.35f;

        public enum ConsoleTypes { CEX, KIT, TEST }
        public enum BuzzerModes { Continuous = -1, Stop, Single, Double, Triple }

        public class TargetInfo
        {
            public float Version { get; private set; }
            public string IP { get; private set; } = string.Empty;
            public string Name { get; private set; } = string.Empty;
            public int ConsoleType { get; private set; }
            public float Firmware { get; private set; }
            public int CPUTemp { get; private set; }
            public int SoCTemp { get; private set; }
            public string Username { get; private set; } = string.Empty;
            public bool Connected { get; private set; }
            public bool Attached { get; private set; }

            internal void SetVersion(float version) => Version = version;
            internal void SetIP(string ip) => IP = ip;
            internal void SetName(string name) => Name = name;
            internal void SetConsoleType(int consoleType) => ConsoleType = consoleType;
            internal void SetFirmware(float firmware) => Firmware = firmware;
            internal void SetCPUTemp(int cpuTemp) => CPUTemp = cpuTemp;
            internal void SetSoCTemp(int socTemp) => SoCTemp = socTemp;
            internal void SetUsername(string username) => Username = username;
            internal void SetConnected(bool connected) => Connected = connected;
            internal void SetAttached(bool attached) => Attached = attached;

            internal void Clear()
            {
                Version = 0;
                Firmware = 0;
                ConsoleType = 0;
                CPUTemp = 0;
                SoCTemp = 0;

                IP = string.Empty;
                Name = string.Empty;
                Username = string.Empty;

                Connected = false;
                Attached = false;
            }

        }

        public class ProcessInfo
        {
            public class Current
            {
                public int PID { get; private set; }
                public int ID { get; private set; }
                public string TitleID { get; private set; }
                public string Name { get; private set; }
                public string Region { get; private set; }
                public string Exec { get; private set; }
                public string Executable { get; private set; }
                public string Version { get; private set; }
                public string SDKMinimum { get; private set; }
                public string AppType { get; private set; }
                public string CoverImageURL { get; private set; }
                public Image CoverImage { get; private set; }
            }

            public string[] Processes { get; private set; }

        }

        public static TargetInfo Target = new TargetInfo();
        public static ProcessInfo Process = new ProcessInfo();

        public string Version => CurrentVersion.ToString("0.00");
        public string Firmware => Target.Firmware.ToString("0.00");
        public string ConsoleType => Utilities.ConvertConsoleTypeToString();
        public string Username => Target.Username;
        public bool Connected => Target.Connected;
        public bool Attached => Target.Attached;

        /* ADD FIND CONSOLE FUNCTION HERE */

        public void InjectPayload(string address)
        {
            Target.SetIP(address);
            Utilities.InjectPayload(Target.IP);
        }

        public void GetTargetInfo() // make string, add enum, and add arg for return type
        {
            if (!Target.Connected) return;

            Target.SetVersion(float.TryParse(PerformRequest("version"), out var version) ? version : 0f);
            Target.SetFirmware(float.TryParse(PerformRequest("get_fw_version"), out var fw) ? fw : 0f);

            var consoleType = PerformRequest("get_sys_type");
            switch (consoleType)
            {
                case "CEX":
                    Target.SetConsoleType((int)ConsoleTypes.CEX);
                    break;
                case "KIT":
                    Target.SetConsoleType((int)ConsoleTypes.KIT);
                    break;
                case "TEST":
                    Target.SetConsoleType((int)ConsoleTypes.TEST);
                    break;
                default:
                    Target.SetConsoleType(-1);
                    break;
            }

            Target.SetCPUTemp(int.TryParse(PerformRequest("get_temperature", "type=cpu"), out var cpuTemp) ? cpuTemp : 0);
            Target.SetSoCTemp(int.TryParse(PerformRequest("get_temperature", "type=soc"), out var socTemp) ? socTemp : 0);

            Target.SetConnected(PerformRequest("connect")?.Contains("true") == true);
            Target.SetUsername(PerformRequest("get_username"));

            PrintTargetInfo();
        }

        public void GetProcessInfo() // make string, add enum, and add arg for return type
        {
            if (!Target.Connected && !Target.Attached) return;
        }

        public void Connect()
        {
            Target.SetConnected(true);
            PerformRequest("connect");

            GetTargetInfo();
        }
        
        public void Connect(string address)
        {
            Target.Clear();
            Target.SetIP(address);
            PerformRequest("connect");
            Target.SetConnected(true);
            GetTargetInfo();
        }

        public void Disconnect()
        {
            if (!Target.Connected) return;
            PerformRequest("disconnect");
            Target.Clear();
        }

        public void Disconnect(string address)
        {
            if (!Target.Connected) return;
            Target.SetIP(address.Trim());
            PerformRequest("disconnect");
            Target.Clear();
        }

        public void Unload()
        {
            if (!IsPortOpen(TimeSpan.FromSeconds(5), Target.IP)) return;
            PerformRequest("unload");
            Target.Clear();
        }
        
        public void Unload(string address)
        {
            Target.SetIP(address.Trim());
            if (!IsPortOpen(TimeSpan.FromSeconds(5), Target.IP)) return;
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
    }
}