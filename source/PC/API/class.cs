using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using static OrbisControlAPI.Utilities;
using static OrbisControlAPI.Definitions;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        public string Version = _version;
        public string IPAddress => _ipAddress;
        public string SprxVersion => _sprxVersion;
        public string Firmware => _firmware.ToString("F2");
        public string SystemType => _sysType;

        public int Temperature(Temp temp) => Definitions.Temperature(temp);

        public bool Connected => _connected;

        public enum Temp { getCPU, getSOC, SetTreshold }
        public enum BuzzType { Continuous = -1, Stop, Single, Double, Triple }
        public enum ConsoleType { CEX, KIT, TEST }


        public List<string> FindConsoles(string[] subnets, int startRange, int endRange)
        {
            var consoles = new List<string>();
            foreach (var subnet in subnets)
            {
                for (int i = startRange; i <= endRange; i++)
                {
                    var ip = $"{subnet}.{i}";
                    foreach (var port in Ports)
                    {
                        if (IsPortOpen(ip, port, TimeSpan.FromSeconds(1)))
                        {
                            lock (consoles) consoles.Add(ip); break;
                        }
                    }
                }
            }

            return consoles;
        }

        public void InjectPayload(string ip)
        {
            if (IsPortOpen(ip, 1337, TimeSpan.FromSeconds(1)) == true) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/";
                var response = Client.GetStringAsync(url).Result;
            }
            catch
            {
                try
                {
                    if (ConnectToBinLoader(ip, "9090"))
                    {
                        socket.SendFile(Path.Combine(
                            Path.GetDirectoryName(Assembly.GetEntryAssembly()
                            .Location), "OrbisControl.bin"));
                        socket.Close();
                    }
                }
                catch
                {
                    return;
                }
            }
        }

        public void Connect(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                throw new ArgumentException("IP address cannot be null or empty.", nameof(ipAddress));

            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
                throw new ArgumentException("Invalid IP address format.", nameof(ipAddress));

            var url = $"http://{ipAddress}:1337/";

            try
            {
                var connectResponse = Client.GetStringAsync(url + "connect").Result;
                if (bool.TryParse(connectResponse, out bool connected) && connected)
                {
                    _connected = true;
                    _ipAddress = ipAddress;

                    UpdateFirmware(url);
                    UpdateSprxVersion(url);
                    UpdateTemperature(url);
                    UpdateSysType(url);
                }
            }
            catch (AggregateException)
            {
                return;
            }
        }

        public void Disconnect()
        {
            if (_connected)
            {
                try
                {
                    var url = $"http://{_ipAddress}:1337/disconnect";
                    Client.GetStringAsync(url).Wait();
                }
                catch
                {
                    return;
                }
            }

            _ipAddress = null;
            _connected = false;

            _firmware = 0;
            _sysType = null;
            _cpuTemp = -1;
            _socTemp = -1;
        }
       
        public void Unload()
        {
            try
            {
                var url = $"http://{_ipAddress}:1337/unload";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        } 

        public void Notify(int type = 1, string msg = null)
        {
            if (!_connected && type != -1 || msg == "") return;

            try
            {
                var url = $"http://{_ipAddress}:1337/send_notify?type={type}&msg={msg}";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        }

        public void SetTempThreshold(int limit = -1)
        {
            if (!_connected && limit != -1) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/set_temp_limit?limit={limit}";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        }

        public void Beep(BuzzType type)
        {
            if (!_connected) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/ring_buzzer?type={(int)type}";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        }

        public int LoadModule(string process, string sprxPath)
        {
            if (_connected && !string.IsNullOrWhiteSpace(process) && !string.IsNullOrWhiteSpace(sprxPath))
            {
                try
                {
                    string handleKey = "Handle: ";

                    var url = $"http://{_ipAddress}:1337/lSPRX?process={process}&path={sprxPath}";
                    var responseContent = Client.GetStringAsync(url).Result;
                    int handleStartIndex = responseContent.IndexOf(handleKey) + handleKey.Length;
                    int handleEndIndex = responseContent.IndexOf('\n', handleStartIndex);

                    if (handleEndIndex == -1) handleEndIndex = responseContent.Length;

                    string handleString = responseContent.Substring(handleStartIndex, handleEndIndex - handleStartIndex).Trim();

                    if (int.TryParse(handleString, out int handle)) return handle;
                }
                catch
                {
                    return -1;
                }
            }
            return -1;
        }

        public void UnloadModule(string process, int sprxPath)
        {
            if (!_connected && string.IsNullOrWhiteSpace(process)) return;

            try
            {
                var url = $"http://{_ipAddress}:1337/unlSPRX?process={process}&path={sprxPath}";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        }




        public void TEST()
        {
            try
            {
                var url = $"http://{_ipAddress}:1337/test";
                Client.GetStringAsync(url).Wait();
            }
            catch
            {
                return;
            }
        }

    }
}