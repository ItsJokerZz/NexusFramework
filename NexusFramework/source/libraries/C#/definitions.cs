using System;
using System.Collections.Generic;

namespace NexusFramework
{
    public class Definitions
    {
        public class Versioning
        {
            public float Version { get; internal set; } = float.MinValue;
            public int BuildNumber { get; internal set; } = int.MinValue;
            public string BuildDate { get; internal set; } = string.Empty;
        }

        public static IReadOnlyList<string> ShellUI_TIDs
        { get; } = new[] { "NPXS40087", "NPSX20001" };

        public enum BuzzerModes
        {
            Continuous = -1, Stop,
            Single, Double, Triple
        }

        public enum PowerStates
        {
            Off, Standby, Reboot
        }

        [Flags]
        public enum MemoryProtection : uint
        {
            None = 0x00,
            Read = 0x01,
            Write = 0x02,
            Execute = 0x04,
            Copy = 0x08,

            ReadWrite = Read | Write,
            All = Read | Write | Execute,
            Default = All
        }

        public class TargetInfo
        {
            public class DiskInfo
            {
                public string Total { get; internal set; } = string.Empty;
                public string Free { get; internal set; } = string.Empty;
                public string Used { get; internal set; } = string.Empty;
                public string PercentageUsed { get; internal set; } = string.Empty;

                internal void Clear()
                {
                    Total = Free = Used = PercentageUsed = string.Empty;
                }
            }

            public DiskInfo Storage { get; } = new DiskInfo();

            public string IP { get; internal set; } = string.Empty;
            public string Name { get; internal set; } = string.Empty;
            public string ConsoleType { get; internal set; } = string.Empty;
            public float Firmware { get; internal set; } = float.MinValue;
            public string Model { get; internal set; } = string.Empty;
            public int CPUTemp { get; internal set; } = int.MinValue;
            public int SoCTemp { get; internal set; } = int.MinValue;
            public float CPUFrequency { get; internal set; } = float.MinValue;
            public string Username { get; internal set; } = string.Empty;
            public string PSID { get; internal set; } = string.Empty;
            public string IDPS { get; internal set; } = string.Empty;
            public bool Connected { get; internal set; } = false;
            public string Uptime { get; internal set; } = string.Empty;
            public List<string>? AppList { get; internal set; } = new List<string>();

            internal bool Initialized = false;

            internal void Clear()
            {
                IP = string.Empty;
                Name = string.Empty;
                ConsoleType = string.Empty;
                Firmware = float.MinValue;
                Model = string.Empty;
                CPUTemp = int.MinValue;
                SoCTemp = int.MinValue;
                CPUFrequency = float.MinValue;
                Username = string.Empty;
                PSID = string.Empty;
                IDPS = string.Empty;
                Connected = false;
                Uptime = string.Empty;

                Storage.Clear();
                AppList?.Clear();
            }
        }

        public class ProcessList
        {
            public int AppId { get; internal set; } = int.MinValue;
            public int PID { get; internal set; } = int.MinValue;
            public string Exec { get; internal set; } = string.Empty;
            public string TitleId { get; internal set; } = string.Empty;
        }

        public class MemoryEntry
        {
            public string Name { get; internal set; } = string.Empty;
            public ulong Start { get; internal set; } = ulong.MinValue;
            public ulong End { get; internal set; } = ulong.MinValue;
            public ulong Offset { get; internal set; } = ulong.MinValue;
            public uint Protection { get; internal set; } = uint.MinValue;
        }

        public class ProcessInfo
        {
            public MemoryEntry[] MemoryMaps { get; internal set; } = new MemoryEntry[0];

            public int AppID { get; internal set; } = int.MinValue;
            public int PID { get; internal set; } = int.MinValue;
            public string TitleID { get; internal set; } = string.Empty;
            public string Name { get; internal set; } = string.Empty;
            public string Region { get; internal set; } = string.Empty;
            public string Executable { get; internal set; } = string.Empty;
            public float Version { get; internal set; } = float.MinValue;
            public float SDKMinimum { get; internal set; } = float.MinValue;
            public string AppType { get; internal set; } = string.Empty;

            internal void Clear()
            {
                MemoryMaps = new MemoryEntry[0];
                AppID = int.MinValue;
                PID = int.MinValue;
                TitleID = string.Empty;
                Name = string.Empty;
                Region = string.Empty;
                Executable = string.Empty;
                Version = float.MinValue;
                AppType = string.Empty;
            }
        }

        public class LoadedELF
        {
            public ulong Base { get; internal set; } = ulong.MinValue;
            public ulong Entry { get; internal set; } = ulong.MinValue;
            public int Size { get; internal set; } = int.MinValue;
        }

        public class LoadedModule
        {
            public ulong Handle { get; internal set; } = ulong.MinValue;
            public string Module { get; internal set; } = string.Empty;
        }

        public class LoadedShellcode
        {
            public ulong Entry { get; internal set; } = ulong.MinValue;
            public int Size { get; internal set; } = int.MinValue;
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class HookAttribute(string exportName) : Attribute
        {
            public string ExportName { get; } = exportName;
        }

    }
}
