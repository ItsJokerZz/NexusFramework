using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;

namespace OrbisControlAPI
{
    public class OCAPI2
    {
        private static readonly float Version = 0.10f;
        private static readonly HttpClient Client = new HttpClient();
        private static Socket Socket;

        public class TargetInfo
        {
            public float Firmware { get; set; }
            public float Version { get; set; }
            public int ConsoleType { get; set; }
            public int CPUTemp { get; set; }
            public int SoCTemp { get; set; }
            public string Name { get; set; }
            public string IP { get; set; }

            public bool Connected { get; private set; }
            public bool Attached { get; private set; }

            internal void SetConnectedInternal(bool value) => Connected = value;
            internal void SetAttachedInternal(bool value) => Attached = value;

            internal void ClearData()
            {
                Firmware = Version = ConsoleType = CPUTemp = SoCTemp = 0;
                Name = IP = string.Empty;
                SetConnectedInternal(false);
                SetAttachedInternal(false);
            }
        }

        internal static TargetInfo Target;

        public enum ConsoleType { CEX, KIT, TEST }
        public enum BuzzerMode { Continuous = -1, Stop, Single, Double, Triple }

        public void PrintTargetInfo()
        {
            foreach (var property in Target.GetType().GetProperties())
            {
                Console.WriteLine($"{property.Name}: {property.GetValue(Target)}");
            }
        }

        public bool ConnectToBinLoader(string ip, string port)
        {
            if (!IPAddress.TryParse(ip, out var ipAddress) || !int.TryParse(port, out var portNumber)) return false;

            try
            {
                using (var client = new WebClient())
                {
                    string response = client.DownloadString($"http://{ipAddress}:9090/status");
                    if (response.Contains("{ \"status\": \"ready\" }"))
                    {
                        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                        {
                            socket.ReceiveTimeout = socket.SendTimeout = 3000;
                            socket.Connect(new IPEndPoint(ipAddress, portNumber));
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public bool IsPortOpen(string ip, int port, TimeSpan timeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ip, port, null, null);
                    if (result.AsyncWaitHandle.WaitOne(timeout))
                    {
                        client.EndConnect(result);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void InjectPayload(string ip)
        {
            try
            {
                if (IsPortOpen(ip, 1337, TimeSpan.FromSeconds(1))) return;

                var url = $"http://{Target.IP}:1337/";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                if (ConnectToBinLoader(ip, "9090"))
                {
                    Socket.SendFile(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "OrbisControl.bin"));
                    Socket.Close();
                }
            }
        }

        public string PerformRequest(string command, string args = "")
        {
            if (string.IsNullOrEmpty(Target.IP)) throw new ArgumentException("IP address cannot be null or empty.", nameof(Target.IP));

            var url = $"http://{Target.IP}:1337/{command}";
            if (!string.IsNullOrEmpty(args)) url += $"?{args}";

            try
            {
                // Create an HttpWebRequest for synchronous request
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }

        public void Connect(string address)
        {
            Target.ClearData();
            Target.IP = address;

            Target.SetConnectedInternal(PerformRequest("connect").Contains("done"));
        }

        public void Disconnect()
        {
            if (!Target.Connected) return;

            PerformRequest("disconnect");
            Target.ClearData();
        }

        public void GetTargetInfo()
        {
            if (!Target.Connected) return;

            Target.Version = float.TryParse(PerformRequest("version"), out var version) ? version : 0;
            Target.Firmware = float.TryParse(PerformRequest("get_fw_version"), out var firmware) ? firmware : 0;

            string type = PerformRequest("get_sys_type");

            switch (type)
            {
                case "CEX":
                    Target.ConsoleType = (int)ConsoleType.CEX;
                    break;
                case "KIT":
                    Target.ConsoleType = (int)ConsoleType.KIT;
                    break;
                case "TEST":
                    Target.ConsoleType = (int)ConsoleType.TEST;
                    break;
            }

            Target.CPUTemp = int.TryParse(PerformRequest("get_temperature", "type=cpu"), out var cpuTemp) ? cpuTemp : 0;
            Target.SoCTemp = int.TryParse(PerformRequest("get_temperature", "type=soc"), out var socTemp) ? socTemp : 0;
        }

        public void Unload(string ipAddress)
        {
            if (!Target.Connected && !IsPortOpen(Target.IP, 1337, TimeSpan.FromSeconds(10))) return;

            PerformRequest("disconnect");
            Disconnect();
        }
    }
}
