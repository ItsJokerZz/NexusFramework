using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static OrbisControlAPI.Utilities;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        #region Variables
        private static readonly float CurrentVersion = 0.63f;

        private readonly string ConsoleList =
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "OrbisControlAPI", "Consoles.cfg");

        private readonly Dictionary<string, FoundConsole>
            FoundConsoles = new Dictionary<string, FoundConsole>();

        public enum ConsoleTypes { CEX, KIT, TEST }

        public enum BuzzerModes
        {
            Continuous = -1, Stop,
            Single, Double, Triple
        }

        public enum PowerStates
        {
            Off = 31, Reboot = 30, RestMode = 1
        }

        public class ConsoleEntry
        {
            public string IP { get; set; }
            public string CustomName { get; set; }
            public string Name { get; set; }
        }

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

            public class DiskInfo
            {
                public string Total { get; private set; } = string.Empty;
                public string Free { get; private set; } = string.Empty;
                public string Used { get; private set; } = string.Empty;
                public string PercentageUsed { get; private set; } = string.Empty;

                internal void SetTotal(string amount) => Total = amount;
                internal void SetFree(string amount) => Free = amount;
                internal void SetUsed(string amount) => Used = amount;
                internal void SetPercentageUsed(string amount) => PercentageUsed = amount;
            }

            public static DiskInfo Storage = new DiskInfo();

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

        public class FoundConsole
        {
            public string IP { get; set; }
            public string Firmware { get; set; }
            public string SystemName { get; set; }
            public string OrbisControl { get; set; }
            public string ConsoleType { get; set; }
        }

        public class NotificationImages
        {
            public const string PlayStationButtons = "cxml://psnotification/tex_icon_system";
            public const string IconBan = "cxml://psnotification/tex_icon_ban";
            public const string DefaultIconNotification = "cxml://psnotification/tex_default_icon_notification";
            public const string DefaultIconMessage = "cxml://psnotification/tex_default_icon_message";
            public const string DefaultIconFriend = "cxml://psnotification/tex_default_icon_friend";
            public const string DefaultIconTrophy = "cxml://psnotification/tex_default_icon_trophy";
            public const string DefaultIconDownload = "cxml://psnotification/tex_default_icon_download";
            public const string DefaultIconUpload16_9 = "cxml://psnotification/tex_default_icon_upload_16_9";
            public const string DefaultIconCloudClient = "cxml://psnotification/tex_default_icon_cloud_client";
            public const string DefaultIconActivity = "cxml://psnotification/tex_default_icon_activity";
            public const string DefaultIconSmaps = "cxml://psnotification/tex_default_icon_smaps";
            public const string DefaultIconSharePlay = "cxml://psnotification/tex_default_icon_shareplay";
            public const string DefaultIconTips = "cxml://psnotification/tex_default_icon_tips";
            public const string DefaultIconEvents = "cxml://psnotification/tex_default_icon_events";
            public const string DefaultIconShareScreen = "cxml://psnotification/tex_default_icon_share_screen";
            public const string DefaultIconCommunity = "cxml://psnotification/tex_default_icon_community";
            public const string DefaultIconLfps = "cxml://psnotification/tex_default_icon_lfps";
            public const string DefaultIconTournament = "cxml://psnotification/tex_default_icon_tournament";
            public const string DefaultIconTeam = "cxml://psnotification/tex_default_icon_team";
            public const string DefaultAvatar = "cxml://psnotification/tex_default_avatar";
            public const string IconCapture = "cxml://psnotification/tex_icon_capture";
            public const string IconStartRec = "cxml://psnotification/tex_icon_start_rec";
            public const string IconStopRec = "cxml://psnotification/tex_icon_stop_rec";
            public const string IconLiveProhibited = "cxml://psnotification/tex_icon_live_prohibited";
            public const string IconLiveStart = "cxml://psnotification/tex_icon_live_start";
            public const string IconLoading = "cxml://psnotification/tex_icon_loading";
            public const string IconLoading16_9 = "cxml://psnotification/tex_icon_loading_16_9";
            public const string IconCountdown = "cxml://psnotification/tex_icon_countdown";
            public const string IconParty = "cxml://psnotification/tex_icon_party";
            public const string IconSharePlay = "cxml://psnotification/tex_icon_shareplay";
            public const string IconBroadcast = "cxml://psnotification/tex_icon_broadcast";
            public const string IconPsnowToast = "cxml://psnotification/tex_icon_psnow_toast";
            public const string AudioDeviceHeadphone = "cxml://psnotification/tex_audio_device_headphone";
            public const string AudioDeviceHeadset = "cxml://psnotification/tex_audio_device_headset";
            public const string AudioDeviceMic = "cxml://psnotification/tex_audio_device_mic";
            public const string AudioDeviceMorpheus = "cxml://psnotification/tex_audio_device_morpheus";
            public const string DeviceBattery0 = "cxml://psnotification/tex_device_battery_0";
            public const string DeviceBattery1 = "cxml://psnotification/tex_device_battery_1";
            public const string DeviceBattery2 = "cxml://psnotification/tex_device_battery_2";
            public const string DeviceBattery3 = "cxml://psnotification/tex_device_battery_3";
            public const string DeviceBatteryNoCharge = "cxml://psnotification/tex_device_battery_nocharge";
            public const string DeviceCompApp = "cxml://psnotification/tex_device_comp_app";
            public const string DeviceController = "cxml://psnotification/tex_device_controller";
            public const string DeviceJediUsb = "cxml://psnotification/tex_device_jedi_usb";
            public const string DeviceBlaster = "cxml://psnotification/tex_device_blaster";
            public const string DeviceKeyboard = "cxml://psnotification/tex_device_keyboard";
            public const string DeviceMouse = "cxml://psnotification/tex_device_mouse";
            public const string DeviceMove = "cxml://psnotification/tex_device_move";
            public const string DeviceRemote = "cxml://psnotification/tex_device_remote";
            public const string DeviceOmit = "cxml://psnotification/tex_device_omit";
            public const string IconConnect = "cxml://psnotification/tex_icon_connect";
            public const string IconEventToast = "cxml://psnotification/tex_icon_event_toast";
            public const string MorpheusTrophyBronze = "cxml://psnotification/tex_morpheus_trophy_bronze";
            public const string MorpheusTrophySilver = "cxml://psnotification/tex_morpheus_trophy_silver";
            public const string MorpheusTrophyGold = "cxml://psnotification/tex_morpheus_trophy_gold";
            public const string MorpheusTrophyPlatinum = "cxml://psnotification/tex_morpheus_trophy_platinum";
            public const string IconChampionsLeague = "cxml://psnotification/tex_icon_champions_league";
        }

        public static TargetInfo Target = new TargetInfo();
        public static ProcessInfo Process = new ProcessInfo();

        #endregion

        #region Properties
        public List<ConsoleEntry> Consoles
        { get; private set; } = new List<ConsoleEntry>();

        public ConsoleEntry this[int index] => index >= 0
            && index < Consoles.Count ? Consoles[index] : null;

        public string Version => CurrentVersion.ToString("0.00");
        public string Firmware => Target.Firmware.ToString("0.00");
        public string Username => Target.Username;
        public bool Connected => Target.Connected;
        public bool Attached => Target.Attached;
        private int NumberOfConsole => Consoles.Count;
        #endregion

        #region Console Management
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

            string json = PerformRequest("setup");
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement response = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE");

                string firmware = response.GetProperty("FW").GetString();
                string systemName = response.GetProperty("NAME").GetString();
                string orbisControl = response.GetProperty("OCAPI").GetString();
                string consoleType = response.GetProperty("TYPE").GetString();

                foreach (var console in FoundConsoles.Values)
                {
                    console.Firmware = firmware;
                    console.SystemName = systemName;
                    console.OrbisControl = orbisControl;
                    console.ConsoleType = consoleType;
                }
            }

            onComplete?.Invoke(FoundConsoles.Values.ToArray());
        }

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

        public void AddConsole(string ip, string customName, string name = "")
        {
            if (string.IsNullOrWhiteSpace(ip) || Consoles.Any(c => c.IP == ip)) return;
            Consoles.Add(new ConsoleEntry { IP = ip, CustomName = customName, Name = name });
            SaveConsoles();

            Console.WriteLine(ip + Environment.NewLine + customName + Environment.NewLine + name);
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

        public void RenameConsole(string ip, string name)
        {
            var console = Consoles.FirstOrDefault(c => c.IP == ip);
            if (console != null)
            {
                console.CustomName = name;
                Debug.WriteLine(console.CustomName);
                SaveConsoles();
            }
        }

        #endregion

        public OCAPI() => LoadConsoles();

        #region Connection Management
        public bool GetConnectionStatus(string address)
        {
            Target.SetIP(address);

            if (!IsPortOpen(Target.IP)) return false;

            return !string.IsNullOrEmpty(PerformRequest("status"));
        }

        public void SetupConsole(string address, bool autoAtach = false)
        {
            // add more exception handling here

            Target.Clear();
            Target.SetIP(address);

            string systemName = PerformRequest("get_console_name") == null ? "PS4" : PerformRequest("get_console_name");

            AddConsole(Target.IP, null, systemName);
            UploadDaemon(Target.IP);

            if (!IsPortOpen(Target.IP))
                InjectPayload(Target.IP);

            if (!IsPortOpen(Target.IP))
                throw new Exception("Failed to properly inject the payload. Please try again, this time you may simply try to run, Connect(...) and then InjectPayload(...).");

            if (GetConnectionStatus(Target.IP))
            {
                Connect(Target.IP);

                if (ProcessInfo.Current.Name != "SceShellUI" && autoAtach)
                    Attach();
            }
            else
                throw new Exception("Finished setting up, but failed to connect! Re-try calling SetupConsole(...) and/or now simply try to connect.");
        }

        public void InjectPayload(string address)
        {
            if (Target.Connected || IsPortOpen(Target.IP)) return;
            Target.SetIP(address);
            Utilities.InjectPayload(Target.IP);
        }

        public void Connect(string address = null)
        {
            if (address != null)
            {
                Target.Clear();
                Target.SetIP(address);
            }

            string check = PerformRequest("connect");

            if (!string.IsNullOrEmpty(check))
            {
                Target.SetConnected(true);
                GetTargetInfo();
                GetProcessInfo();
            }
            else throw new Exception("Failed to connect to the target. Please check connect and try again.");
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
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

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

        public void GetTargetInfo()
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            Target.SetVersion(float.TryParse(PerformRequest("version"), out var version) ? version : 0f);
            Target.SetName(PerformRequest("get_console_name"));

            string sysTypeResponse = PerformRequest("get_sys_type");

            if (!string.IsNullOrEmpty(sysTypeResponse))
            {
                using (JsonDocument doc = JsonDocument.Parse(sysTypeResponse))
                {
                    string sysType = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE").GetString();

                    switch (sysType)
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
                }

                string fwResponse = PerformRequest("get_fw_version");
                using (JsonDocument doc = JsonDocument.Parse(fwResponse))
                {
                    string fwVersionString = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE").GetString();
                    float fwVersion = float.TryParse(fwVersionString, out var fw) ? fw : 0f;
                    Target.SetFirmware(fwVersion);
                }

                string diskInfo = PerformRequest("get_disk_info", "return=all");
                using (JsonDocument doc = JsonDocument.Parse(diskInfo))
                {
                    var data = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE");

                    string totalSpace = data.GetProperty("totalSpace").GetString();
                    string freeSpace = data.GetProperty("freeSpace").GetString();
                    string usedSpace = data.GetProperty("usedSpace").GetString();
                    string percentage = data.GetProperty("percentUsed").GetString();

                    TargetInfo.Storage.SetTotal(totalSpace);
                    TargetInfo.Storage.SetFree(freeSpace);
                    TargetInfo.Storage.SetUsed(usedSpace);
                    TargetInfo.Storage.SetPercentageUsed(percentage);
                }

                Target.SetCPUTemp(int.TryParse(PerformRequest("get_temperature", "type=cpu"), out var cpuTemp) ? cpuTemp : 0);
                Target.SetSoCTemp(int.TryParse(PerformRequest("get_temperature", "type=soc"), out var socTemp) ? socTemp : 0);
                Target.SetConnected(PerformRequest("connect")?.Contains("true") == true);

                string usernameResponse = PerformRequest("get_username");
                using (JsonDocument doc = JsonDocument.Parse(usernameResponse))
                {
                    string username = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE").GetString();
                    Target.SetUsername(username);
                }
            }
        }

        #endregion

        #region System Control
        public void SendNotification(string message = null, string image = NotificationImages.DefaultIconNotification)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            PerformRequest("send_notify", $"image={image}&msg={message}");
        }

        public void AlarmBuzzer(BuzzerModes mode)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            PerformRequest("ring_buzzer", $"type={(int)mode}");
        }

        public void SetFanThreshold(int limit)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            PerformRequest("set_temp_limit", $"limit={limit}");
        }

        public void SetPowerState(PowerStates state)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            PerformRequest("set_power_state", $"state={(int)state}");
        }

        #endregion

        #region Process Management
        public void GetProcessList()
        {
            var json = PerformRequest("get_proc_list");

            if (!string.IsNullOrEmpty(json))
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var data = doc.RootElement.GetProperty("DATA");

                    List<string> list = new List<string>();

                    foreach (var item in data.EnumerateObject())
                        list.Add(item.Value.GetString());

                    ProcessInfo.SetList(list.ToArray());
                }
            }
        }

        public void GetProcessInfo()
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

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

        public int GetProcessIdByName(string name)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            string jsonResponse = PerformRequest("get_pid_by_name", $"name={name}");
            JsonDocument doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("DATA").GetProperty("PID").GetInt32();
        }

        public string GetNameOfProcessByID(int pid)
        {
            if (!Target.Connected)
                throw new Exception("Please check the connection to the target before proceeding!");

            string jsonResponse = PerformRequest("get_name_of_pid", $"pid={pid}");
            JsonDocument doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("DATA").GetProperty("NAME").GetString();
        }

        public ulong AllocateMemory(int size)
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");

            string response = PerformRequest("alloc_memory", $"length={size}");

            using (JsonDocument doc = JsonDocument.Parse(response))
            {
                string addressString = doc.RootElement.GetProperty("DATA").GetProperty("RESPONSE").GetString();

                if (addressString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    addressString = addressString.Substring(2);

                if (ulong.TryParse(addressString, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
                    return address;
                else return 0;

            }
        }

        public void FreeMemory(ulong address, int length)
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");

            string hexAddress = $"0x{address:X}";
            PerformRequest("free_memory", $"address={hexAddress}&length={length}");
        }

        public T ReadMemory<T>(ulong address, int? length = null)
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");

            if (typeof(T) == typeof(string))
            {
                if (length == null)
                {
                    StringBuilder str = new StringBuilder();
                    ulong offset = 0;

                    while (true)
                    {
                        string byteResponse = PerformRequest("read_memory", $"address=0x{address + offset:X}&size=1").ToLower();

                        using (JsonDocument doc = JsonDocument.Parse(byteResponse))
                        {
                            string memoryDataString = doc.RootElement.GetProperty("data").GetProperty("response").GetString();

                            if (byteResponse.Contains("error") || byteResponse.Contains("failed"))
                                break;

                            byte value = Convert.ToByte(memoryDataString, 16);
                            if (value == 0) break;

                            str.Append(Convert.ToChar(value));
                            offset++;
                        }
                    }

                    return (T)(object)str.ToString();
                }
                else
                {
                    byte[] byteArray = ReadMemory<byte[]>(address, length.Value);
                    StringBuilder str = new StringBuilder();

                    for (int i = 0; i < byteArray.Length; i++)
                    {
                        if (byteArray[i] == 0) break;
                        str.Append(Convert.ToChar(byteArray[i]));
                    }

                    return (T)(object)str.ToString();
                }
            }

            if (length == null)
                throw new ArgumentException("Size is required for non-string types");

            string response = PerformRequest("read_memory", $"address=0x{address:X}&size={length.Value}").ToLower();

            using (JsonDocument doc = JsonDocument.Parse(response))
            {
                string memoryDataString = doc.RootElement.GetProperty("data").GetProperty("response").GetString();

                if (response.Contains("error") || response.Contains("failed"))
                    return default;

                if (memoryDataString.Length % 2 == 0)
                {
                    byte[] byteArray = new byte[memoryDataString.Length / 2];
                    for (int i = 0; i < byteArray.Length; i++)
                        byteArray[i] = Convert.ToByte(memoryDataString.Substring(i * 2, 2), 16);

                    if (typeof(T) != typeof(byte[]) && typeof(T) != typeof(string))
                        Array.Reverse(byteArray);

                    if (typeof(T) == typeof(ulong))
                        return (T)(object)BitConverter.ToUInt64(byteArray, 0);
                    else if (typeof(T) == typeof(uint))
                        return (T)(object)BitConverter.ToUInt32(byteArray, 0);
                    else if (typeof(T) == typeof(int))
                        return (T)(object)BitConverter.ToInt32(byteArray, 0);
                    else if (typeof(T) == typeof(short))
                        return (T)(object)BitConverter.ToInt16(byteArray, 0);
                    else if (typeof(T) == typeof(byte))
                        return (T)(object)byteArray[0];
                    else if (typeof(T) == typeof(byte[]))
                        return (T)(object)byteArray;
                    else if (typeof(T) == typeof(char))
                        return (T)(object)(char)byteArray[0];
                    else if (typeof(T) == typeof(char[]))
                        return (T)(object)Encoding.UTF8.GetChars(byteArray);
                    else if (typeof(T) == typeof(bool))
                        return (T)(object)(byteArray[0] != 0);
                    else if (typeof(T) == typeof(float))
                        return (T)(object)BitConverter.ToSingle(byteArray, 0);
                    else if (typeof(T) == typeof(double))
                        return (T)(object)BitConverter.ToDouble(byteArray, 0);
                    else
                        throw new InvalidOperationException($"Unsupported return type: {typeof(T)}");
                }
                else
                    return default;
            }
        }

        public void WriteMemory(ulong address, byte[] data)
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");

        }

        #endregion

        #region Module Management
        public int LoadModule(string processName, string modulePath)
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");

            if (int.TryParse(PerformRequest("load_module", $"process={processName}&module={modulePath}"), out int result))
                Debug.WriteLine(result);
                
                return result;

            return -1;
        }

        public void UnloadModule(int handle) { }

        public void LoadPlugin()
        {
            if (!Target.Connected && !Target.Attached)
                throw new Exception("Please check the connection/attachment to the target before proceeding!");
        }

        public void UnloadPlugin() { }

        #endregion

    }
}
