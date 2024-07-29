using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        private HttpClient Client
               = new HttpClient();

        private Timer _timer;
        private Socket socket;

        public string Version { get; private set; } = "0.01";

        private string _ipAddress;
        private bool _connected;
        private float _firmware;
        private int _temperature;
        private string _sysType;

        public string PS4Version { get; private set; }

        public bool Connected => _connected;
        public string IPAddress => _ipAddress;
        public string Firmware => _firmware.ToString("F2");
        public int Temperature => _temperature;
        public string SystemType => _sysType;

        private bool ConnectToBinLoader(string ip, string port)
        {
            WebClient client = new WebClient();
            bool ret = false;

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
                    catch (Exception)
                    {
                        ret = false;
                    }
                }
                else
                {
                    ret = false;
                }
            }
            catch (Exception)
            {
                ret = false;
            }

            return ret;
        }

        private async Task UpdateFirmware(string url)
        {
            try
            {
                var response = await Client.GetAsync(url + "fw");
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                float.TryParse(responseBody, out _firmware);
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        private async Task UpdatePS4Version(string url)
        {
            try
            {
                var response = await Client.GetAsync(url + "version");
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                if (float.TryParse(responseBody, out float number))
                    PS4Version = number.ToString("F2");
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        private async Task UpdateSysType(string url)
        {
            try
            {
                var response = await Client.GetAsync(url + "sysType");
                response.EnsureSuccessStatusCode();

                _sysType = await response.Content.ReadAsStringAsync();

            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        private async Task UpdateTemperature(string url)
        {
            try
            {
                var response = await Client.GetAsync(url + "temp");
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                int.TryParse(responseBody, out _temperature);
            }
            catch (TaskCanceledException)
            {
               return;
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        // add FindConsole and FindAndConnect
        // make return multiple consoles if
        // they exist specify like...
        // var a = FindConsole; var b = var[0];

        public async Task Connect(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                throw new ArgumentException("IP address cannot be null or empty.", nameof(ipAddress));

            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
                throw new ArgumentException("Invalid IP address format.", nameof(ipAddress));

            var url = $"http://{ipAddress}:1337/";

            try
            {
                var connectResponse = await Client.GetAsync(url + "connect");
                connectResponse.EnsureSuccessStatusCode();

                var connectResponseBody = await connectResponse.Content.ReadAsStringAsync();

                if (bool.TryParse(connectResponseBody, out bool connected) && connected)
                {
                    _connected = true;
                    _ipAddress = ipAddress;

                    await UpdateFirmware(url);
                    await UpdatePS4Version(url);
                    await UpdateSysType(url);

                    _timer = new Timer(async _ => await UpdateTemperature(url),
                        null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        public async Task Disconnect()
        {
            if (!_connected) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/disconnect";
                var response = await Client.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
               return;
            }

            _ipAddress = null;
            _connected = false;

            if (_timer != null)
            {
                try
                {
                    _timer.Change(Timeout.Infinite, 0);
                    _timer.Dispose();
                    _timer = null; // Set to null to indicate it has been disposed
                }
                catch (ObjectDisposedException) { /* do nothing */ }
            }
        }

        public async Task Unload()
        {
            if (!_connected) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/unload";
                var response = await Client.GetStringAsync(url);
            }
            catch { return; }

        }

        public async Task Notify(int type = 1, string msg = null)
        {
            if (!_connected && type != -1 || msg == "") return;

            try
            {
                var url = $"http://{_ipAddress}:1337/notify?type={type}&msg={msg}";
                var response = await Client.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        public async Task SetTempThreshold(int limit = -1)
        {
            if (!_connected && limit != -1) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/setTempLimit?limit={limit}";
                var response = await Client.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        public enum BeepType
        {
            Stop,
            Single,
            Double,
            Triple,
            Continuous
        }


        public async Task Beep(BeepType type)
        {
            if (!_connected) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/beep?type={(int)type}";
                var response = await Client.GetAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException)
            {
               return;
            }
            catch (HttpRequestException)
            {
               return;
            }
        }

        public async Task InjectPayload(string ip)
        {
            try
            {
                var url = $"http://{_ipAddress}:1337/";
                var response = await Client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                Console.WriteLine(response);
            }
            catch
            {
                try
                {
                    if (ConnectToBinLoader(ip, "9090"))
                    {
                        socket.SendFile(Path.Combine(
                            Path.GetDirectoryName(Assembly.GetEntryAssembly()
                            .Location), "OrbisControl.bin")); socket.Close();
                    }
                }
                catch (Exception) { return; }
            }
        }
    }
}