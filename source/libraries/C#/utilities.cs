using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexusFramework
{
    internal static class Utilities
    {
        private static readonly HttpClient Http = new HttpClient();

        internal static async Task<bool> IsUrlAlive(string url)
        {
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };

                var response = await client.GetAsync(url);

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static async Task<bool> IsPortOpen(Definitions.TargetInfo target, int port = 2567, int timeoutMs = 1000)
        {
            if (port == 9090) // GoldHEN's garbage loader...
                return await IsUrlAlive($"http://{target.IP}:9090");

            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(target.IP, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));

                if (completedTask != connectTask)
                    return false;

                return tcpClient.Connected;
            }
            catch
            {
                return false;
            }
        }

        internal static byte[] GetResourceBytes(string resourceName)
        {
            try
            {
                string payloadSource = "NexusFramework.payloads";
                string fullName = $"{payloadSource}.{resourceName}";

                Debug.WriteLine($"[GetResourceBytes] Resolving resource: {fullName}");

                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullName)
                    ?? throw new FileNotFoundException("Resource not found: " + resourceName);

                using (stream)
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byte[] data = ms.ToArray();

                    Debug.WriteLine($"[GetResourceBytes] Loaded {data.Length} bytes from embedded resource: {resourceName}");

                    return data;
                }
            }
            catch
            {
                throw;
            }
        }

        internal static void SendBytesTcp(Definitions.TargetInfo target, int port, byte[] data)
        {
            try
            {
                Debug.WriteLine($"[SendBytesTcp] Connecting to {target.IP}:{port}");
                Debug.WriteLine($"[SendBytesTcp] Payload size: {data.Length} bytes");

                using (TcpClient client = new TcpClient())
                {
                    client.Connect(target.IP, port);
                    Debug.WriteLine("[SendBytesTcp] Connected");

                    using (NetworkStream stream = client.GetStream())
                    {
                        Debug.WriteLine("[SendBytesTcp] Sending data...");
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                        Debug.WriteLine("[SendBytesTcp] Data sent successfully");
                    }

                    client.Close();
                    Debug.WriteLine("[SendBytesTcp] Connection closed");
                }
            }
            catch
            {
                throw;
            }
        }

        internal static void SendPayload(Definitions.TargetInfo target, int port, string path)
        {
            try
            {
                Debug.WriteLine($"[SendPayload] Address: {target.IP}, Port: {port}, Path: {path}");

                if (string.IsNullOrEmpty(path))
                    throw new ArgumentNullException(nameof(path));

                byte[] payloadData;
                int targetPort = port;

                switch (path)
                {
                    case "elfldr-ps4.elf":
                    case "elfldr-ps5.elf":
                    case "nexus-ps4.elf":
                    case "nexus-ps5.elf":
                        Debug.WriteLine($"[SendPayload] Loading embedded payload: {path}");
                        payloadData = GetResourceBytes(path);
                        break;

                    default:
                        Debug.WriteLine($"[SendPayload] Loading payload from disk: {path}");

                        if (!File.Exists(path))
                            throw new FileNotFoundException(path);

                        payloadData = File.ReadAllBytes(path);

                        Debug.WriteLine($"[SendPayload] Loaded {payloadData.Length} bytes from file");
                        break;
                }

                Debug.WriteLine("[SendPayload] Sending payload...");
                SendBytesTcp(target, targetPort, payloadData);
                Debug.WriteLine("[SendPayload] Done");
            }
            catch
            {
                throw;
            }
        }

        internal static async Task<T> RequestAsync<T>(Definitions.TargetInfo target, string command, string? query = null, HttpMethod? method = null, string? body = null, bool raw = false)
        {
            // if (string.IsNullOrWhiteSpace(target.IP))
            //    throw new TargetNotConnectedException();

            method ??= HttpMethod.Get;
            string url = $"http://{target.IP}:2567/{command}" + (string.IsNullOrEmpty(query) ? "" : $"?{query}" + "#library");

            using var req = new HttpRequestMessage(method, url);
            if (body != null)
                req.Content = new StringContent(body, Encoding.UTF8, raw ? "application/vnd.memory" : "application/json");

            try
            {
                using var res = await Http.SendAsync(req).ConfigureAwait(false);
                res.EnsureSuccessStatusCode();

                if (typeof(T) == typeof(byte[]))
                    return (T)(object)await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                string str = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

                using var doc = JsonDocument.Parse(str);
                if (doc.RootElement.TryGetProperty("ERROR", out JsonElement errorEl))
                    throw new Exception($"API Error: {errorEl.GetString()}");


                JsonElement responseEl = doc.RootElement.GetProperty("RESPONSE");

                if (typeof(T) == typeof(string))
                {
                    if (responseEl.ValueKind == JsonValueKind.String || responseEl.ValueKind == JsonValueKind.Number)
                        return (T)(object)responseEl.ToString();
                    else
                        return (T)(object)responseEl.GetRawText();
                }

                if (typeof(T) == typeof(JsonElement) || typeof(T) == typeof(JsonElement?))
                {
                    return (T)(object)responseEl.Clone();
                }

                var responseJson = responseEl.GetRawText();
                return JsonSerializer.Deserialize<T>(responseJson)!;
            }
            catch
            {
                throw;
            }
        }

        internal static byte[] StructureToBytes<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        internal static class JsonParser
        {
            internal static bool TryExtract(string? json, string error, out JsonElement result)
            {
                result = default;
                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidResponseException(error);

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    result = doc.RootElement.TryGetProperty("RESPONSE", out var r) ? r.Clone() : doc.RootElement.Clone();
                    return true;
                }
                catch (JsonException ex)
                {
                    throw new InvalidJsonException("Invalid JSON.", ex);
                }
            }

            internal static T GetValue<T>(JsonElement e, string key)
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var v))
                    return default;

                object result;

                if (typeof(T) == typeof(string))
                {
                    if (v.ValueKind == JsonValueKind.String)
                        result = v.GetString() ?? "";
                    else if (v.ValueKind == JsonValueKind.Number)
                        result = v.ToString();
                    else
                        result = v.ToString();
                }
                else if (typeof(T) == typeof(int))
                {
                    int.TryParse(v.GetRawText().Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out int i);
                    result = i;
                }
                else if (typeof(T) == typeof(float))
                {
                    float.TryParse(v.GetRawText().Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out float f);
                    result = f;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    string val = v.GetRawText().Trim('"');
                    if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        val = val.Substring(2);
                    ulong.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong ul);
                    result = ul;
                }
                else
                {
                    throw new NotSupportedException("Type " + typeof(T) + " is not supported.");
                }

                return (T)result;
            }

            internal static ulong GetHex(JsonElement e, string key)
            {
                string val = GetValue<string>(e, key);
                if (string.IsNullOrEmpty(val)) return 0;
                if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) val = val[2..];
                return ulong.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ? r : 0;
            }

            internal static JsonElement GetNested(JsonElement e, string key)
                => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : default;

        }

    }
}