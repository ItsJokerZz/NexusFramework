using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using static OrbisControlAPI.Definitions;
using static OrbisControlAPI.Utilities;

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
            // if (IsPortOpen(ip, 1337, TimeSpan.FromSeconds(1)) == true) return;

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

                    var url = $"http://{_ipAddress}:1337/exec_prx?exec={process}&path={sprxPath}";
                    var responseContent = Client.GetStringAsync(url).Result;
                    int handleStartIndex = responseContent.IndexOf(handleKey) + handleKey.Length;
                    int handleEndIndex = responseContent.IndexOf('\n', handleStartIndex);

                    if (handleEndIndex == -1) handleEndIndex = responseContent.Length;

                    string handleString = responseContent.Substring(handleStartIndex, handleEndIndex - handleStartIndex).Trim();

                    if (int.TryParse(handleString, out int handle)) return handle;

                    Console.WriteLine(handleString);

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




        public string TEST(string func)
        {
            string response = null;

            try
            {
                var url = $"http://{_ipAddress}:1337/{func}";
                response = Client.GetStringAsync(url).Result;
            }
            catch
            {
                return null;
            }
            return response;
        }

        public void TEST2(string func, string param)
        {
            _ipAddress = "192.168.137.206";
            try
            {
                var url = $"http://{_ipAddress}:1337/{func}";  // append params to URL
                var client = new HttpClient();

                // Set the header with the parameters, if needed
                var formattedParam = $"params{{{param}}}";  // Not used in the body anymore

                // Add the header with params information
                client.DefaultRequestHeaders.Add("Data", formattedParam);  // Adding custom header at the bottom

                // Send POST request with no body, just the query string in the URL and a header
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    // If you don't want to send any body, make sure to set Content-Length to 0
                    Content = new StringContent(formattedParam)
                };

                // If needed, you can explicitly set the Content-Length header
                requestMessage.Content.Headers.ContentLength = formattedParam.Length;

                // Send the request synchronously by blocking the async SendAsync
                var response = client.SendAsync(requestMessage).Result;  // Using .Result to block

                // Get the response content synchronously
                var responseContent = response.Content.ReadAsStringAsync().Result;  // Blocking call

                // Output
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"Data being sent in the header: {formattedParam}");
                Console.WriteLine($"Response from server: {responseContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }











        public string read_proc_memory(long address, int size)
        {
            string response = null;
            try
            {
                string hexAddress = "0x" + address.ToString("X");
                var url = $"http://{_ipAddress}:1337/read_memory?address={hexAddress}&size={size}";  // Corrected endpoint name
                response = Client.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading memory: " + ex.Message);
                return null;
            }
            return response;
        }

        public string write_proc_memory(long address, byte[] dataBytes)
        {
            string response = null;
            try
            {
                string hexAddress = "0x" + address.ToString("X");

                // Convert data bytes to a hex string
                string data = BitConverter.ToString(dataBytes).Replace("-", "").ToLower();

                // Construct the URI before it gets sent
                var uri = $"http://{_ipAddress}:1337/write_memory?address={hexAddress}&data={data}";

                // Print the generated URI for debugging
                Console.WriteLine("Generated URI: " + uri);

                // Send the GET request (though this is the part we might replace with POST)
                response = Client.GetStringAsync(uri).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing memory: " + ex.Message);
                return null;
            }

            return response;
        }



        public string alloc_proc_memory(int length)
        {
            string response = null;
            try
            {
                // Get PID
                string pidResponse = Client.GetStringAsync($"http://{_ipAddress}:1337/get_proc_info?return=pid").Result;
                string pid = pidResponse.Replace("pid:", "").Trim();

                // Allocating memory
                var url = $"http://{_ipAddress}:1337/alloc_memory?pid={pid}&length={length}";
                response = Client.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error allocating memory: " + ex.Message);
                return null;
            }
            return response;
        }

        public string free_proc_memory(long address, int length)
        {
            string response = null;
            try
            {
                string pidResponse = Client.GetStringAsync($"http://{_ipAddress}:1337/get_proc_info?return=pid").Result;
                string pid = pidResponse.Replace("pid:", "").Trim();

                string hexAddress = "0x" + address.ToString("X");
                var url = $"http://{_ipAddress}:1337/free_memory?pid={pid}&address={hexAddress}&length={length}";
                response = Client.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error freeing memory: " + ex.Message);
                return null;
            }
            return response;
        }


    }
}