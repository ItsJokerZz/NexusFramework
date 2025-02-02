using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;

namespace OrbisControlAPI
{
    internal class Utilities
    {
        internal static readonly HttpClient Client = new HttpClient();

        internal static string ConvertConsoleTypeToString()
        {
            if (OCAPI.Target?.ConsoleType == (int)OCAPI.ConsoleTypes.CEX)
                return "CEX";
            if (OCAPI.Target?.ConsoleType == (int)OCAPI.ConsoleTypes.KIT)
                return "KIT";
            if (OCAPI.Target?.ConsoleType == (int)OCAPI.ConsoleTypes.TEST)
                return "TEST";

            return null;
        }

        internal static void PrintTargetInfo(/* remove me later */)
        {
            foreach (var property in typeof(OCAPI.TargetInfo).GetProperties())
                Console.WriteLine($"{property.Name}: {property.GetValue(OCAPI.Target)}");
        }

        internal static Socket GetBinLoaderSocket(string ip, int port)
        {
            if (!IPAddress.TryParse(ip, out var address))
                return null;
            try
            {
                using (var webClient = new WebClient())
                {
                    string status = webClient.DownloadString($"http://{address}:9090/status");
                    if (status.Contains("{ \"status\": \"ready\" }"))
                    {
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                        {
                            SendTimeout = 3000,
                            ReceiveTimeout = 3000
                        };
                        socket.Connect(new IPEndPoint(address, port));
                        return socket;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return null;
        }

        internal bool ConnectToBinLoader(string ip, string port)
        {
            if (!int.TryParse(port, out int portNumber))
                return false;
            using (var socket = GetBinLoaderSocket(ip, portNumber))
                return socket != null;
        }

        internal static bool IsPortOpen(TimeSpan timeout, string address, int port = 1337)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    IAsyncResult asyncResult = tcpClient.BeginConnect(address, port, null, null);
                    if (asyncResult.AsyncWaitHandle.WaitOne(timeout))
                    {
                        tcpClient.EndConnect(asyncResult);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error checking port: " + ex.Message);
            }
            return false;
        }

        internal static void InjectPayload(string address)
        {
            try
            {
                if (!IsPortOpen(TimeSpan.FromSeconds(10), address))
                    Client.GetStringAsync($"http://{OCAPI.Target.IP}:1337/").Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine("InjectPayload error: " + ex.Message);
                using (var binLoaderSocket = GetBinLoaderSocket(address, 9090))
                {
                    if (binLoaderSocket != null)
                    {
                        string filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "OrbisControl.bin");
                        binLoaderSocket.SendFile(filePath);
                        binLoaderSocket.Close();
                    }
                }
            }
        }

        internal static string PerformRequest(string command, string args = "")
        {
            if (string.IsNullOrEmpty(OCAPI.Target.IP))
                throw new ArgumentException("IP address cannot be null or empty.", nameof(OCAPI.Target.IP));

            string url = $"http://{OCAPI.Target.IP}:1337/{command}" + (string.IsNullOrEmpty(args) ? "" : $"?{args}");

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    return reader.ReadToEnd();
            }
            catch (WebException ex) when (ex.Message.Contains("connection was closed"))
            {
                OCAPI.Target.Clear();
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in PerformRequest: " + ex.Message);
                return null;
            }
        }

    }
}
