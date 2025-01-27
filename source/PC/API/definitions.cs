using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using static OrbisControlAPI.OCAPI;
using static OrbisControlAPI.Utilities;

namespace OrbisControlAPI
{
    public class Definitions
    {
        public static HttpClient Client
               = new HttpClient();

        public static Timer _timer;
        public static Socket socket;

        public static readonly int[]
            Ports = { 9090 };

        public static bool _connected;

        public static string
            _ipAddress, _sysType;

        public static float _firmware;

        public static int
            _cpuTemp, _socTemp, _sprxHandle;

        public static string _version { get; private set; } = "0.01";
        public static string _sprxVersion { get; set; }

        public static int Temperature(Temp temp)
        {
            UpdateTemperature($"http://{_ipAddress}:1337/");
            if (temp == Temp.getCPU) return _cpuTemp;
            if (temp == Temp.getSOC) return _socTemp;

            return -1;
        }
    }
}