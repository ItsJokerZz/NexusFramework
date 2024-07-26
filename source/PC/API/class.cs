using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OrbisControlAPI
{
    public class OCAPI
    {
        private string _ipAddress;
        private bool _connected;

        public bool Connected()
        {
            return _connected;
        }
        public void Disconnect()
        {
            Console.WriteLine($"Disconnected from {_ipAddress}:1337.");

            _ipAddress = null;
            _connected = false;
        }

        public async Task Connect(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                throw new ArgumentException("IP address cannot be null or empty.", nameof(ipAddress));

            if (!IPAddress.TryParse(ipAddress, out _))
                throw new ArgumentException("Invalid IP address format.", nameof(ipAddress));

            Console.WriteLine($"Attempting to connect to {ipAddress}...");


            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"http://{ipAddress}:1337/connect";

                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (bool.TryParse(responseBody, out bool result) && result)
                        _connected = true;
                    else _connected = false;

                    if (_connected)
                        _ipAddress = ipAddress;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request error: {e.Message}"); _connected = false;
                }
            }
        }


        public async void Notify(int type, string msg)
        {
            if (_connected)
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        string url = $"http://{_ipAddress}:1337/notify?type={type}&msg={msg}";

                        HttpResponseMessage response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine($"Request error: {e.Message}");
                    }
                }
            }
        }
    }
}