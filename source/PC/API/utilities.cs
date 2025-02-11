using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using static OrbisControlAPI.OCAPI;

namespace OrbisControlAPI
{
    public class Utilities
    {
        internal enum HttpMethodType { GET, POST, PUT }

        internal static readonly HttpClient Client = new HttpClient();

        internal static Socket GetBinLoaderSocket(string ip, int port)
        {
            if (!IPAddress.TryParse(ip, out var address))
            {
                Debug.WriteLine($"Invalid IP address: {ip}");
                return null;
            }
            try
            {
                Debug.WriteLine($"Checking status of BinLoader at http://{address}:9090/status");
                using (var webClient = new WebClient())
                {
                    string status = webClient.DownloadString($"http://{address}:9090/status");
                    if (status.Contains("{ \"status\": \"ready\" }"))
                    {
                        Debug.WriteLine($"BinLoader is ready, attempting to connect to {address}:{port}");
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                        {
                            SendTimeout = 3000,
                            ReceiveTimeout = 3000
                        };
                        socket.Connect(new IPEndPoint(address, port));
                        Debug.WriteLine("Connection successful.");
                        return socket;
                    }
                    else
                    {
                        Debug.WriteLine("BinLoader not ready.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetBinLoaderSocket: {ex.Message}");
            }
            return null;
        }

        internal bool ConnectToBinLoader(string ip, string port)
        {
            if (!int.TryParse(port, out int portNumber))
            {
                Debug.WriteLine($"Invalid port number: {port}");
                return false;
            }
            using (var socket = GetBinLoaderSocket(ip, portNumber))
            {
                if (socket != null)
                {
                    Debug.WriteLine($"Successfully connected to BinLoader at {ip}:{portNumber}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"Failed to connect to BinLoader at {ip}:{portNumber}");
                    return false;
                }
            }
        }

        internal static bool IsPortOpen(string address, int port = 1337)
        {
            try
            {
                Debug.WriteLine($"Checking if port {port} is open on {address}");
                using (var tcpClient = new TcpClient())
                {
                    var result = tcpClient.BeginConnect(address, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(1000);
                    if (success && tcpClient.Connected)
                    {
                        Debug.WriteLine($"Port {port} is open on {address}");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"Port {port} is not open on {address}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking port {port} on {address}: {ex.Message}");
                return false;
            }
        }

        internal static void InjectPayload(string address)
        {
            try
            {
                if (!IsPortOpen(address))
                    Client.GetStringAsync($"http://{Target.IP}:1337/").Wait();
            }
            catch
            {
                using (var binLoaderSocket = GetBinLoaderSocket(address, 9090))
                {
                    if (binLoaderSocket != null)
                    {
                        var resourceName = "OrbisControlAPI.OrbisControl.bin";
                        using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream != null)
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    resourceStream.CopyTo(memoryStream);
                                    memoryStream.Seek(0, SeekOrigin.Begin);
                                    binLoaderSocket.Send(memoryStream.ToArray());
                                }
                            }
                        }

                        binLoaderSocket.Close();
                    }
                }
            }
        }

        internal static string PerformRequest(string command, string args = "", HttpMethodType method = HttpMethodType.GET, string param = "")
        {
            if (string.IsNullOrEmpty(Target.IP))
                throw new ArgumentException("IP address cannot be null or empty.", nameof(Target.IP));

            string url = $"http://{Target.IP}:1337/{command}" + (string.IsNullOrEmpty(args) ? "" : $"?{args}");

            try
            {
                using (var client = new WebClient())
                {
                    if (!string.IsNullOrEmpty(param))
                    {
                        var formattedParam = $"params{{{param}}}";
                        client.Headers.Add("Data", formattedParam);

                        if (method == HttpMethodType.POST)
                        {
                            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                            return client.UploadString(url, formattedParam);
                        }
                    }

                    return client.DownloadString(url);
                }
            }
            catch (WebException ex) when (ex.Message.Contains("connection was closed"))
            {
                Target.Clear();
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
