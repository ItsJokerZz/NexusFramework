using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static NexusFramework.Definitions;

namespace NexusFramework
{
    public partial class Library
    {
        /*
              public async Task<ulong> AllocateMemory(uint length)
               {
                   ThrowNotConnectedException();
                   ThrowVersioMismatchException();

                   string? response = await Utilities.RequestAsync<string>(Target, $"allocate_memory?length={length}").ConfigureAwait(false);

                   if (Utilities.JsonParser.TryExtract(response, "Memory allocation response was empty.", out var json))
                       return Utilities.JsonParser.GetHex(json, "RESPONSE");

                   throw new Exception("Unknown error during memory allocation.");
               }
         */

        public async Task<ulong> AllocateMemory(uint length)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var response = await Utilities.RequestAsync<JsonElement?>(Target, $"allocate_memory?length={length}").ConfigureAwait(false);

            if (response.HasValue && response.Value.ValueKind != JsonValueKind.Null)
                return Convert.ToUInt64(response.Value.GetString(), 16);


            throw new Exception("Memory allocation response was null or invalid.");
        }

        public async Task<bool> FreeMemory(ulong address, uint length)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var response = await Utilities.RequestAsync<string>(
                Target, "free_memory", $"address=0x{address:X}&length={length}",
                HttpMethod.Get, null, false).ConfigureAwait(false);

            Debug.WriteLine($"free_memory raw response: [{response}]");

            if (string.IsNullOrEmpty(response))
                return false;

            string raw = response.Trim().Trim('"');
            return !raw.StartsWith("{");
        }

        public async Task<T> ReadMemory<T>(ulong address, uint? length = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            byte[] buffer;

            if (typeof(T) == typeof(string))
            {
                var bytes = new List<byte>();
                ulong ptr = address;

                while (true)
                {
                    var b = await Utilities.RequestAsync<byte[]>(Target, "read_memory", $"address=0x{ptr:X}&length=1", HttpMethod.Get, null, true);
                    if (b == null || b.Length == 0 || b[0] == 0) break;
                    bytes.Add(b[0]);
                    ptr++;
                }

                buffer = bytes.ToArray();
            }
            else
            {
                uint size = length ?? typeof(T) switch
                {
                    Type t when t == typeof(bool) || t == typeof(byte) || t == typeof(char) => 1,
                    Type t when t == typeof(short) || t == typeof(ushort) => 2,
                    Type t when t == typeof(int) || t == typeof(uint) || t == typeof(float) => 4,
                    Type t when t == typeof(long) || t == typeof(ulong) || t == typeof(double) => 8,
                    _ => throw new InvalidOperationException($"Unsupported type {typeof(T)}")
                };

                buffer = await Utilities.RequestAsync<byte[]>(Target, "read_memory", $"address=0x{address:X}&length={size}", HttpMethod.Get, null, true);
                if (buffer == null || buffer.Length == 0) return default!;
            }

            object result = typeof(T) switch
            {
                Type t when t == typeof(string) => Encoding.UTF8.GetString(buffer),
                Type t when t == typeof(ulong) => BitConverter.ToUInt64(buffer, 0),
                Type t when t == typeof(uint) => BitConverter.ToUInt32(buffer, 0),
                Type t when t == typeof(int) => BitConverter.ToInt32(buffer, 0),
                Type t when t == typeof(short) => BitConverter.ToInt16(buffer, 0),
                Type t when t == typeof(byte) => buffer[0],
                Type t when t == typeof(byte[]) => buffer,
                Type t when t == typeof(char) => (char)buffer[0],
                Type t when t == typeof(char[]) => Encoding.UTF8.GetChars(buffer),
                Type t when t == typeof(bool) => buffer[0] != 0,
                Type t when t == typeof(float) => BitConverter.ToSingle(buffer, 0),
                Type t when t == typeof(double) => BitConverter.ToDouble(buffer, 0),
                _ => throw new InvalidOperationException($"Unsupported return type {typeof(T)}")
            };

            Debug.WriteLine($"Memory read of {buffer.Length} byte(s) completed.");

            return (T)result;
        }

        public async Task<T> WriteMemory<T>(ulong address, T value)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            byte[] data = value switch
            {
                byte[] b => b,
                byte b => [b],
                char[] c => Encoding.UTF8.GetBytes(c),
                char ch => [(byte)ch],
                string s => Encoding.UTF8.GetBytes(s),
                bool b => [(byte)(b ? 1 : 0)],
                short s => BitConverter.GetBytes(s),
                ushort us => BitConverter.GetBytes(us),
                int i => BitConverter.GetBytes(i),
                uint u => BitConverter.GetBytes(u),
                long l => BitConverter.GetBytes(l),
                ulong ul => BitConverter.GetBytes(ul),
                float f => BitConverter.GetBytes(f),
                double d => BitConverter.GetBytes(d),
                _ => throw new InvalidOperationException($"Unsupported value type: {typeof(T)}")
            };

            var json = new
            {
                address = $"0x{address:X}",
                data = BitConverter.ToString(data).Replace("-", "").ToLower()
            };

            await Utilities.RequestAsync<string>(Target, "write_memory", "", HttpMethod.Post, JsonSerializer.Serialize(json), false).ConfigureAwait(false);

            return value;
        }

        public async Task<LoadedELF?> LoadELF(string path, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"path={path}"
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "load_elf", query, HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response.HasValue)
            {
                var json = response.Value;

                var elf_vars = new LoadedELF
                {
                    Base = Utilities.JsonParser.GetHex(json, "BASE"),
                    Entry = Utilities.JsonParser.GetHex(json, "ENTRY"),
                    Size = Utilities.JsonParser.GetValue<int>(json, "SIZE")
                };

                return elf_vars;
            }

            return null;
        }

        public async Task UnloadELF(LoadedELF elf, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"base=0x{elf.Base:X}",
                $"entry=0x{elf.Entry:X}",
                $"size={elf.Size}"
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            await Utilities.RequestAsync<JsonElement?>(Target, "unload_elf", query, HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task<MemoryEntry[]?> GetVirtualMemoryMaps()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "get_vm_maps", "", HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response.HasValue)
            {
                var json = response.Value;

                if (!json.TryGetProperty("maps", out var mapsJson) || mapsJson.ValueKind != JsonValueKind.Array)
                    return null;

                var memoryMaps = new List<MemoryEntry>();

                foreach (var entryJson in mapsJson.EnumerateArray())
                {
                    memoryMaps.Add(new MemoryEntry
                    {
                        Name = Utilities.JsonParser.GetValue<string>(entryJson, "NAME"),
                        Start = Utilities.JsonParser.GetValue<ulong>(entryJson, "START"),
                        End = Utilities.JsonParser.GetValue<ulong>(entryJson, "END"),
                        Offset = Utilities.JsonParser.GetValue<ulong>(entryJson, "OFFSET"),
                        Protection = Utilities.JsonParser.GetValue<uint>(entryJson, "PROT")
                    });
                }

                return memoryMaps.ToArray();
            }

            return null;
        }

        public async Task SetMemoryProtection(ulong address, uint length, MemoryProtection protection = MemoryProtection.Default)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"address=0x{address:X}",
                $"length={length}",
                $"prot={protection}"
            };

            string query = string.Join("&", parts);

            await Utilities.RequestAsync<JsonElement?>(Target, "memory_protection", query, HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task<LoadedModule?> LoadModule(string path, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"path={path}"
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "load_module", query, HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response.HasValue)
            {
                var json = response.Value;

                var loadedModule = new LoadedModule
                {
                    Handle = Utilities.JsonParser.GetValue<ulong>(json, "HANDLE"),
                    Module = Utilities.JsonParser.GetValue<string>(json, "MODULE")
                };

                return loadedModule;
            }

            return null;
        }

        public async Task UnloadModule(LoadedModule module, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"handle={module.Handle}"
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            await Utilities.RequestAsync<JsonElement?>(Target, "unload_module", query, HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        // Add GetLoadedModules and create list to store them!
        public async Task<int> GetModuleHandle(string name, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"name={name}"
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "get_module_handle", query, HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response.HasValue)
            {
                var json = response.Value;

                if (json.ValueKind == JsonValueKind.Number)
                    return json.GetInt16();
            }

            return -1;
        }

        public async Task<ulong> ResolveSymbol(int handle, string name, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var parts = new List<string>
            {
                $"handle={handle}",
                $"name={name}",
            };

            if (pid != null)
                parts.Insert(0, $"pid={pid}");

            string query = string.Join("&", parts);

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "resolve_symbol", query, HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response.HasValue)
                return Convert.ToUInt64(response.Value.GetString(), 16);

            return 0;
        }

        // Not yet implemented on the API/payload (backend)
        public async Task<ulong> ArrayOfBytesScan(string[] pattern, ulong start, ulong end, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var sw = Stopwatch.StartNew();

            byte?[] bytes = [.. pattern.Select(b =>
                b == "??" ? (byte?)null : Convert.ToByte(b, 16)
            )];

            int patternLength = bytes.Length;

            int firstSolid = -1;
            byte firstSolidVal = 0;
            for (int i = 0; i < patternLength; i++)
            {
                if (bytes[i].HasValue)
                {
                    firstSolid = i;
                    firstSolidVal = bytes[i]!.Value;
                    break;
                }
            }

            const int chunkSize = 0x40000; // PAGE SIZE

            for (ulong addr = start; addr < end; addr += chunkSize)
            {
                uint readSize = (uint)Math.Min(chunkSize + patternLength, (long)(end - addr));

                Debug.WriteLine($"[AOB] Reading chunk 0x{addr:X} - 0x{addr + readSize:X}...");
                var chunkSw = Stopwatch.StartNew();

                byte[] buffer = await ReadMemory<byte[]>(addr, readSize);

                chunkSw.Stop();
                Debug.WriteLine($"[AOB] Chunk read in {chunkSw.Elapsed.TotalMilliseconds:F1}ms — {(buffer != null ? buffer.Length : 0)} bytes");

                if (buffer == null || buffer.Length < patternLength)
                    continue;

                int limit = buffer.Length - patternLength;

                for (int i = 0; i <= limit; i++)
                {
                    if (firstSolid >= 0 && buffer[i + firstSolid] != firstSolidVal)
                        continue;

                    bool found = true;
                    for (int j = 0; j < patternLength; j++)
                    {
                        if (bytes[j].HasValue && buffer[i + j] != bytes[j].Value)
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        ulong result = addr + (ulong)i;
                        sw.Stop();
                        Debug.WriteLine($"[AOB] Pattern found at 0x{result:X} in {sw.Elapsed.TotalSeconds:F3}s");
                        return result;
                    }
                }
            }

            sw.Stop();
            Debug.WriteLine($"[AOB] Pattern not found in range 0x{start:X} - 0x{end:X} — took {sw.Elapsed.TotalSeconds:F3}s");
            return 0;
        }

        public async Task<LoadedShellcode> InstallShellcode(string shellcodePath, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            if (!File.Exists(shellcodePath))
                throw new FileNotFoundException("Shellcode binary not found", shellcodePath);

            byte[] shellcodeBytes = await File.ReadAllBytesAsync(shellcodePath).ConfigureAwait(false);
            if (shellcodeBytes.Length == 0)
                throw new InvalidOperationException("Shellcode file is empty");

            var json = new
            {
                pid = pid.HasValue ? pid.Value.ToString() : "",
                size = shellcodeBytes.Length.ToString(),
                data = BitConverter.ToString(shellcodeBytes).Replace("-", "").ToLower()
            };

            var response = await Utilities.RequestAsync<string>(
                Target, "install_shellcode", "", HttpMethod.Post,
                JsonSerializer.Serialize(json), false).ConfigureAwait(false);

            Debug.WriteLine($"install_shellcode raw response: [{response}]");

            if (string.IsNullOrEmpty(response))
                throw new Exception("install_shellcode returned empty response.");

            string raw = response.Trim().Trim('"');

            if (raw.StartsWith("{"))
                throw new Exception($"install_shellcode server error: {raw}");

            ulong entry = Convert.ToUInt64(
                raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw : "0x" + raw, 16);

            return new LoadedShellcode
            {
                Entry = entry,
                Size = shellcodeBytes.Length
            };
        }

        public async Task<bool> RemoveShellcode(LoadedShellcode shellcode, int? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            if (shellcode == null || shellcode.Entry == ulong.MinValue || shellcode.Size == int.MinValue)
                throw new ArgumentException("Invalid LoadedShellcode — was it returned from InstallShellcode?");

            bool ok = await FreeMemory(shellcode.Entry, (uint)shellcode.Size).ConfigureAwait(false);

            if (ok)
            {
                shellcode.Entry = ulong.MinValue;
                shellcode.Size = int.MinValue;
            }

            return ok;
        }

        public async Task<ulong> RemoteCallMethod(ulong address, bool persistent = false, params object?[] args)
        {
            List<(ulong addr, uint size)> remoteAllocs = new();
            string[] argsOut = ["0", "0", "0", "0", "0", "0"];
            ulong result = 0;

            try
            {
                for (int i = 0; i < Math.Min(args.Length, 6); i++)
                {
                    object? a = args[i];
                    if (a == null) continue;

                    Type t = a.GetType();

                    if (t.IsValueType && !t.IsPrimitive && !(a is IntPtr) && !(a is UIntPtr))
                    {
                        int size;
                        try { size = Marshal.SizeOf(a); }
                        catch (Exception ex)
                        {
                            continue;
                        }

                        byte[] data = new byte[size];
                        IntPtr ptr = Marshal.AllocHGlobal(size);
                        try
                        {
                            Marshal.StructureToPtr(a, ptr, false);
                            Marshal.Copy(ptr, data, 0, size);
                        }
                        catch (Exception ex)
                        {
                            Marshal.FreeHGlobal(ptr);
                            continue;
                        }
                        finally { Marshal.FreeHGlobal(ptr); }

                        if (size <= 8)
                        {
                            ulong val = 0;
                            for (int b = 0; b < size; b++)
                                val |= (ulong)data[b] << (b * 8);
                            argsOut[i] = $"0x{val:X}";
                        }
                        else
                        {
                            ulong remote = await AllocateMemory((uint)size);
                            await WriteMemory(remote, data);
                            remoteAllocs.Add((remote, (uint)size));
                            argsOut[i] = $"0x{remote:X}";
                        }
                    }
                    else if (a is string s)
                    {
                        byte[] data = Encoding.UTF8.GetBytes(s + "\0");
                        ulong remote = await AllocateMemory((uint)data.Length);
                        await WriteMemory(remote, data);
                        remoteAllocs.Add((remote, (uint)data.Length));
                        argsOut[i] = $"0x{remote:X}";
                    }
                    else if (a is IntPtr ip)
                        argsOut[i] = $"0x{(ulong)ip:X}";
                    else if (a is UIntPtr up)
                        argsOut[i] = $"0x{(ulong)up:X}";
                    else
                        argsOut[i] = a.ToString()?.ToLower() ?? "0";

                }

                string response = await Utilities.RequestAsync<string>(
                    Target,
                    "rpc_call",
                    "",
                    HttpMethod.Post,
                    JsonSerializer.Serialize(new
                    {
                        address = $"0x{address:X}",
                        arg1 = argsOut[0],
                        arg2 = argsOut[1],
                        arg3 = argsOut[2],
                        arg4 = argsOut[3],
                        arg5 = argsOut[4],
                        arg6 = argsOut[5]
                    }),
                    false
                ).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    response = response.Trim();
                    result = response.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToUInt64(response, 16)
                        : ulong.TryParse(response, out var tmp) ? tmp : 0;
                }

                return result;
            }
            finally
            {
                if (!persistent)
                {
                    foreach (var (addr, size) in remoteAllocs)
                    {
                        try { await FreeMemory(addr, size); } catch { }
                    }
                }
            }
        }

        public async Task<bool> InjectStartDetour<T>(string path, T context) where T : struct
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Shellcode binary not found", path);

            byte[] bin = await File.ReadAllBytesAsync(path).ConfigureAwait(false);

            if (bin.Length < 12)
                throw new Exception("InjectStartDetour: Binary too small to contain trailer.");

            uint magic = BitConverter.ToUInt32(bin, bin.Length - 4);
            uint trailerStart = BitConverter.ToUInt32(bin, bin.Length - 8);
            uint count = BitConverter.ToUInt32(bin, bin.Length - 12);

            if (magic != 0x584E4600)
                throw new Exception("InjectStartDetour: No symbol trailer found in binary.");
            if (count == 0 || count > 4096 || trailerStart >= bin.Length)
                throw new Exception("InjectStartDetour: Symbol trailer is malformed.");

            var symbols = new Dictionary<string, uint>();
            int pos = (int)trailerStart;
            for (uint i = 0; i < count; i++)
            {
                int nameStart = pos;
                while (pos < bin.Length && bin[pos] != 0x00) pos++;
                if (pos >= bin.Length) throw new Exception("InjectStartDetour: Trailer parse overran buffer.");
                string name = System.Text.Encoding.ASCII.GetString(bin, nameStart, pos - nameStart);
                pos++;
                if (pos + 4 > bin.Length) throw new Exception("InjectStartDetour: Trailer parse overran buffer.");
                symbols[name] = BitConverter.ToUInt32(bin, pos);
                pos += 4;
            }

            if (!symbols.TryGetValue("_start", out uint entryOffset))
                throw new Exception("InjectStartDetour: _start symbol not found.");

            LoadedShellcode shellcode = await InstallShellcode(path).ConfigureAwait(false);
            if (shellcode.Entry == 0) return false;

            ulong entryAddress = shellcode.Entry + entryOffset;

            var fields = typeof(T).GetFields()
                .Select(f => (Field: f, Attr: f.GetCustomAttribute<HookAttribute>()))
                .Where(x => x.Attr != null)
                .ToList();

            object boxed = context;

            var originalAddresses = fields.ToDictionary(
                x => x.Attr!.ExportName,
                x => (ulong)x.Field.GetValue(boxed)!
            );

            var trampolinePages = new Dictionary<string, ulong>();
            foreach (var (field, attr) in fields)
            {
                ulong page = await AllocateMemory(0x1000).ConfigureAwait(false);
                trampolinePages[attr!.ExportName] = page;
                field.SetValue(boxed, page);
                Debug.WriteLine($"[PreAlloc] {attr.ExportName}: 0x{page:X}");
            }

            context = (T)boxed;
            ulong remoteContextPtr = await AllocateMemory((uint)Utilities.StructureToBytes(context).Length).ConfigureAwait(false);
            await WriteMemory(remoteContextPtr, Utilities.StructureToBytes(context)).ConfigureAwait(false);

            await RemoteCallMethod(entryAddress, true, remoteContextPtr).ConfigureAwait(false);
            Debug.WriteLine($"[!] _start complete. g_ctx=0x{remoteContextPtr:X}");

            foreach (var (field, attr) in fields)
            {
                if (!symbols.TryGetValue(attr!.ExportName, out uint hookOffset))
                {
                    Debug.WriteLine($"[Warn] Symbol not found for hook: {attr.ExportName}");
                    continue;
                }

                ulong hookAddress = shellcode.Entry + hookOffset;
                ulong trampolineHint = trampolinePages[attr!.ExportName];

                string query = string.Join("&", new[]
                {
            $"address=0x{originalAddresses[attr.ExportName]:X}",
            $"destination=0x{hookAddress:X}",
            $"trampoline=0x{trampolineHint:X}"
        });

                var response = await Utilities.RequestAsync<string>(
                    Target, "detour_method", query, HttpMethod.Get, null, false).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(response))
                    throw new Exception($"detour_method returned empty response for {attr.ExportName}");

                string raw = response.Trim().Trim('"');

                if (raw.StartsWith("{"))
                    throw new Exception($"detour_method server error for {attr.ExportName}: {raw}");

                ulong trampoline = Convert.ToUInt64(
                    raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw : "0x" + raw, 16);

                Debug.WriteLine($"[Hooked] {attr.ExportName}: orig=0x{originalAddresses[attr.ExportName]:X} hook=0x{hookAddress:X} tramp=0x{trampoline:X}");
            }

            Debug.WriteLine($"[!] InjectStartDetour complete. All hooks live.");
            return true;
        }

        public async Task SuspendProcess(uint? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, "suspend_process", $"pid={pid}", HttpMethod.Get, null, false).ConfigureAwait(false);
        }
        public async Task ResumeProcess(uint? pid = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, "resume_process", $"pid={pid}", HttpMethod.Get, null, false).ConfigureAwait(false);
        }


    }
}