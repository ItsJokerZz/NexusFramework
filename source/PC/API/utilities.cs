using System;
using System.Net.Sockets;
using System.Net;

using static OrbisControlAPI.Definitions;

namespace OrbisControlAPI
{
    public class Utilities
    {
        public static bool ConnectToBinLoader(string ip, string port, bool ret = false)
        {
            WebClient client = new WebClient();

            if (!System.Net.IPAddress.TryParse(ip, out var ipAddress)) return false;
            if (!int.TryParse(port, out var portNumber)) return false;

            try
            {
                string response = client.DownloadString($"http://{ipAddress}:9090/status");

                if (response.Contains("{ \"status\": \"ready\" }"))
                {
                    try
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.ReceiveTimeout = 3000;
                        socket.SendTimeout = 3000;
                        socket.Connect(new IPEndPoint(ipAddress, portNumber));

                        ret = true;
                    }
                    catch
                    {
                        ret = false;
                    }
                }
                else ret = false;
            }
            catch
            {
                ret = false;
            }

            return ret;
        }

        public static bool IsPortOpen(string ip, int port, TimeSpan timeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ip, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeout);
                    if (!success) return false;

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void UpdateFirmware(string url)
        {
            try
            {
                var response = Client.GetStringAsync(url + "fw").Result;
                float.TryParse(response, out _firmware);
            }
            catch
            {
                return;
            }
        }

        public static void UpdateSprxVersion(string url)
        {
            try
            {
                var response = Client.GetStringAsync(url + "version").Result;

                if (float.TryParse(response, out float number))
                    _sprxVersion = number.ToString("F2");
            }
            catch
            {
                return;
            }
        }

        public static void UpdateTemperature(string url)
        {
            if (!_connected) return;

            try
            {
                var response = Client.GetStringAsync(url + "temp?type=cpu").Result;
                int.TryParse(response, out _cpuTemp);

                var _response = Client.GetStringAsync(url + "temp?type=soc").Result;
                int.TryParse(_response, out _socTemp);
            }
            catch { return; }
        }

        public static void UpdateSysType(string url)
        {
            try
            {
                _sysType = Client.GetStringAsync(url + "sys_type").Result;
            }
            catch { return; }
        }
    }
}
