using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System;


namespace OrbisControlAPI
{
    public class OCAPI2
    {
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

            internal void SetConnectedInternal(bool value)
            {
                Connected = value;
            }

            internal void SetAttachedInternal(bool value)
            {
                Attached = value;
            }

            internal void ClearData()
            {
                int num2 = (SoCTemp = 0);
                int num4 = (CPUTemp = num2);
                int num6 = (ConsoleType = num4);
                float firmware = (Version = num6);
                Firmware = firmware;
                string name = (IP = string.Empty);
                Name = name;
                SetConnectedInternal(value: false);
                SetAttachedInternal(value: false);
            }
        }

        public enum ConsoleType
        {
            CEX,
            KIT,
            TEST
        }

        public enum BuzzerMode
        {
            Continuous = -1,
            Stop,
            Single,
            Double,
            Triple
        }

        private static readonly float Version = 0.1f;
        private static readonly HttpClient Client = new HttpClient();
        private Socket Socket;

        internal TargetInfo Target;

        public bool Connected => Target != null && Target.Connected;

        // Constructor to initialize Target
        public OCAPI2()
        {
            Target = new TargetInfo(); // Ensure Target is initialized here
        }

        public void PrintTargetInfo()
        {
            if (Target == null) return;

            PropertyInfo[] properties = Target.GetType().GetProperties();
            foreach (PropertyInfo propertyInfo in properties)
            {
                Console.WriteLine($"{propertyInfo.Name}: {propertyInfo.GetValue(Target)}");
            }
        }

        public bool ConnectToBinLoader(string ip, string port)
        {
            if (!IPAddress.TryParse(ip, out var address) || !int.TryParse(port, out var result))
            {
                return false;
            }
            try
            {
                using WebClient webClient = new WebClient();
                if (webClient.DownloadString($"http://{address}:9090/status").Contains("{ \"status\": \"ready\" }"))
                {
                    using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        int receiveTimeout = (socket.SendTimeout = 3000);
                        socket.ReceiveTimeout = receiveTimeout;
                        socket.Connect(new IPEndPoint(address, result));
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public bool IsPortOpen(string ip, int port, TimeSpan timeout)
        {
            try
            {
                using TcpClient tcpClient = new TcpClient();
                IAsyncResult asyncResult = tcpClient.BeginConnect(ip, port, null, null);
                if (asyncResult.AsyncWaitHandle.WaitOne(timeout))
                {
                    tcpClient.EndConnect(asyncResult);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public void InjectPayload(string ip)
        {
            try
            {
                if (!IsPortOpen(ip, 1337, TimeSpan.FromSeconds(1.0)))
                {
                    string text = "http://" + Target.IP + ":1337/";
                    Client.GetStringAsync(text).Wait();
                }
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
            if (Target == null || string.IsNullOrEmpty(Target.IP))
            {
                throw new ArgumentException("IP address cannot be null or empty.", "IP");
            }

            string text = "http://" + Target.IP + ":1337/" + command;
            if (!string.IsNullOrEmpty(args))
            {
                text = text + "?" + args;
            }
            try
            {
                HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(text);
                obj.Method = "GET";
                using HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse();
                using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
                return streamReader.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        public void Connect(string address)
        {
            if (Target == null) return; // Check if Target is initialized

            Target.ClearData();
            Target.IP = address;
            Target.SetConnectedInternal(PerformRequest("connect").Contains("done"));
        }

        public void Disconnect()
        {
            if (Target == null || !Target.Connected) return; // Ensure Target is initialized

            PerformRequest("disconnect");
            Target.ClearData();
        }

        public void GetTargetInfo()
        {
            if (Target == null || !Target.Connected) return; // Ensure Target is initialized

            Target.Version = (float.TryParse(PerformRequest("version"), out var result) ? result : 0f);
            Target.Firmware = (float.TryParse(PerformRequest("get_fw_version"), out var result2) ? result2 : 0f);
            switch (PerformRequest("get_sys_type"))
            {
                case "CEX":
                    Target.ConsoleType = 0;
                    break;
                case "KIT":
                    Target.ConsoleType = 1;
                    break;
                case "TEST":
                    Target.ConsoleType = 2;
                    break;
            }
            Target.CPUTemp = (int.TryParse(PerformRequest("get_temperature", "type=cpu"), out var result3) ? result3 : 0);
            Target.SoCTemp = (int.TryParse(PerformRequest("get_temperature", "type=soc"), out var result4) ? result4 : 0);
        }

        public void Unload(string ipAddress)
        {
            if (Target == null || (!Target.Connected && !IsPortOpen(Target.IP, 1337, TimeSpan.FromSeconds(10.0)))) return;

            PerformRequest("disconnect");
            Disconnect();
        }
    }
}