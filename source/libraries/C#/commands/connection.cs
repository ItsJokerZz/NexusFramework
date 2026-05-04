using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexusFramework
{
    public partial class Library
    {
        public async Task<bool> IsRunning(string? address = null)
        {
            UpdateTargetIPAddress(address);

            if (!await Utilities.IsPortOpen(Target).ConfigureAwait(false))
                return false;

            return await Utilities.RequestAsync<JsonElement?>(Target, "status", "", System.Net.Http.HttpMethod.Get, null, true) != null;
        }

        public async Task Unload(string? address = null, bool clearTarget = true)
        {
            UpdateTargetIPAddress(address);
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<JsonElement?>(Target, "unload", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);

            if (clearTarget)
                Target.Clear();

            Process.Clear();
        }

        public async Task Connect(string? address = null)
        {
            UpdateTargetIPAddress(address);

            await GetAPIVersioning();

            await Utilities.RequestAsync<JsonElement?>(Target, "connect", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);

            Target.Connected = true;
        }

        public async Task Disconnect(string? address = null, bool unload = false, bool clearTarget = true)
        {
            UpdateTargetIPAddress(address);
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<JsonElement?>(Target, "disconnect", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);

            if (unload)
                await Unload(address).ConfigureAwait(false);

            if (clearTarget)
                Target.Clear();

            Process.Clear();
        }

        public async Task SendPayload(string? address = null, string? path = null)
        {
            UpdateTargetIPAddress(address);

            int elfldr_port = 0;
            if (await Utilities.IsPortOpen(Target, 9020).ConfigureAwait(false))
                elfldr_port = 9020;

            if (await Utilities.IsPortOpen(Target, 9021).ConfigureAwait(false))
                elfldr_port = 9021;

            bool binloader_running = await Utilities.IsPortOpen(Target, 9090).ConfigureAwait(false);

            bool elfldr_running = elfldr_port > 0;

            if (await IsRunning(address).ConfigureAwait(false))
            {
                if (!Target.Connected)
                    await Connect(address).ConfigureAwait(false);

                await Unload(address, false).ConfigureAwait(false); 
            }

            bool is_playstation5 = !binloader_running && elfldr_running;
            if (!is_playstation5)
            {
                if (binloader_running && !elfldr_running)
                    Utilities.SendPayload(Target, 9090, "nexus-ps4.elf");

                if (binloader_running && elfldr_running)
                    Utilities.SendPayload(Target, elfldr_port, "nexus-ps4.elf");
            }
            else
            {
                if (elfldr_running)
                    Utilities.SendPayload(Target, elfldr_port, "nexus-ps5.elf");
            }
        }

        internal async Task GetAPIVersioning()
        {
            var response = await Utilities.RequestAsync<JsonElement?>(Target, "version", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response == null)
                return;

            var json = response.Value;

            APIVersion.Version = Utilities.JsonParser.GetValue<float>(json, "VERSION");
            APIVersion.BuildNumber = Utilities.JsonParser.GetValue<int>(json, "NUMBER");
            APIVersion.BuildDate = Utilities.JsonParser.GetValue<string>(json, "DATE");
        }

        public async Task GetTargetInfo()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "get_sys_info", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response == null)
                return;

            var json = response.Value;

            Target.Name = Utilities.JsonParser.GetValue<string>(json, "NAME");
            Target.Model = Utilities.JsonParser.GetValue<string>(json, "MODEL");
            Target.Username = Utilities.JsonParser.GetValue<string>(json, "USER");
            Target.ConsoleType = Utilities.JsonParser.GetValue<string>(json, "TYPE");
            Target.Firmware = Utilities.JsonParser.GetValue<float>(json, "FW");
            Target.PSID = Utilities.JsonParser.GetValue<string>(json, "PSID");
            Target.IDPS = Utilities.JsonParser.GetValue<string>(json, "IDPS");
            Target.CPUFrequency = Utilities.JsonParser.GetValue<float>(json, "CPU_FREQ");
            Target.Uptime = Utilities.JsonParser.GetValue<string>(json, "UPTIME");

            var temps = Utilities.JsonParser.GetNested(json, "TEMPS");
            Target.CPUTemp = Utilities.JsonParser.GetValue<int>(temps, "CPU");
            Target.SoCTemp = Utilities.JsonParser.GetValue<int>(temps, "SOC");

            var disk = Utilities.JsonParser.GetNested(json, "DISK");
            Target.Storage.Total = Utilities.JsonParser.GetValue<string>(disk, "TOTAL");
            Target.Storage.Free = Utilities.JsonParser.GetValue<string>(disk, "FREE");
            Target.Storage.Used = Utilities.JsonParser.GetValue<string>(disk, "USED");
            Target.Storage.PercentageUsed = Utilities.JsonParser.GetValue<string>(disk, "%");

            await GetAPIVersioning();
        }

        public async Task GetProcessList()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            ProcessList.Clear();

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "get_proc_list", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response == null)
                return;

            var json = response.Value;

            ProcessList = new List<Definitions.ProcessList>();

            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("LIST", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    var proc = new Definitions.ProcessList
                    {
                        AppId = Utilities.JsonParser.GetValue<int>(item, "AID"),
                        PID = Utilities.JsonParser.GetValue<int>(item, "PID"),
                        Exec = Utilities.JsonParser.GetValue<string>(item, "EXEC"),
                        TitleId = Utilities.JsonParser.GetValue<string>(item, "TID")
                    };

                    ProcessList.Add(proc);

                }
            }
        }

        public async Task GetProcessInfo()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            Process.Clear();

            var response = await Utilities.RequestAsync<JsonElement?>(Target, "get_proc_info", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
            if (response == null)
                return;

            var json = response.Value;

            Process.AppID = Utilities.JsonParser.GetValue<int>(json, "AID");
            Process.PID = Utilities.JsonParser.GetValue<int>(json, "PID");
            Process.TitleID = Utilities.JsonParser.GetValue<string>(json, "TID");
            Process.Executable = Utilities.JsonParser.GetValue<string>(json, "EXEC");
            Process.Name = Utilities.JsonParser.GetValue<string>(json, "NAME");
            Process.Region = Utilities.JsonParser.GetValue<string>(json, "REGION");
            Process.SDKMinimum = Utilities.JsonParser.GetValue<float>(json, "SDK");
            Process.AppType = Utilities.JsonParser.GetValue<string>(json, "TYPE");
            Process.Version = Utilities.JsonParser.GetValue<float>(json, "VER");

            if (!Definitions.ShellUI_TIDs.Contains(Process.TitleID))
                Process.MemoryMaps = await GetVirtualMemoryMaps();
        }

    }
}