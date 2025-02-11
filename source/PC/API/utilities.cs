using System;
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
            catch
            {
                return null;
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

        internal static bool IsPortOpen(string address, int port = 1337)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var result = tcpClient.BeginConnect(address, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(1000);
                    return success && tcpClient.Connected;
                }
            }
            catch
            {
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

        internal static void UploadDaemon(string address)
        {
            try
            {
                string targetDirectory = "/data/GoldHEN/plugins/ItsJokerZz";
                string fileName = "OrbisControl.prx";
                Uri uri = new Uri($"ftp://{address}:2121{targetDirectory}/{fileName}");

                byte[] fileContents;
                using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"OrbisControlAPI.{fileName}"))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        resourceStream.CopyTo(memoryStream);
                        fileContents = memoryStream.ToArray();
                    }
                }

                FtpWebRequest checkFileRequest = (FtpWebRequest)WebRequest.Create(uri);
                checkFileRequest.Method = WebRequestMethods.Ftp.GetFileSize;

                try
                {
                    using (FtpWebResponse response = (FtpWebResponse)checkFileRequest.GetResponse())
                        return;
                }
                catch (WebException ex)
                {
                    if (ex.Response is FtpWebResponse ftpResponse && ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    { } else return;
                }

                FtpWebRequest uploadRequest = (FtpWebRequest)WebRequest.Create(uri);
                uploadRequest.Method = WebRequestMethods.Ftp.UploadFile;
                uploadRequest.ContentLength = fileContents.Length;

                using (Stream requestStream = uploadRequest.GetRequestStream())
                    requestStream.Write(fileContents, 0, fileContents.Length);
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResponse)
                {
                    Console.WriteLine($"FTP error: {ftpResponse.StatusCode} - {ftpResponse.StatusDescription}");
                }
                else
                {
                    Console.WriteLine($"FTP error: {ex.Message}");
                }
            }
        }

        internal static string PerformRequest(string command, string args = "", HttpMethodType method = HttpMethodType.GET, string parameters = "")
        {
            string url = $"http://{Target.IP}:1337/{command}" + (string.IsNullOrEmpty(args) ? "" : $"?{args}");

            try
            {
                using (var client = new WebClient())
                {
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        var formattedParams = $"params{{{parameters}}}";
                        client.Headers.Add("Data", formattedParams);

                        if (method == HttpMethodType.POST)
                        {
                            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                            return client.UploadString(url, formattedParams);
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
            catch (Exception)
            {
                return null;
            }
        }
    }
}
