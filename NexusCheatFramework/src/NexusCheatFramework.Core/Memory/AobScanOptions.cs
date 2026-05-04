using System;

namespace NexusCheatFramework.Memory
{
    public sealed class AobScanOptions
    {
        /// <summary>Optional lower bound (inclusive). If null, derived from regions.</summary>
        public ulong? Start { get; set; }

        /// <summary>Optional upper bound (exclusive). If null, derived from regions.</summary>
        public ulong? End { get; set; }

        /// <summary>Read chunk size. Default 256 KiB. Must be greater than pattern length.</summary>
        public uint ChunkSize { get; set; } = 0x40000;

        /// <summary>Only scan regions with PROT_EXEC.</summary>
        public bool ScanExecutableOnly { get; set; }

        /// <summary>Only scan readable regions (default true).</summary>
        public bool ScanReadableOnly { get; set; } = true;

        /// <summary>If set, only scan regions whose name contains this substring (case-insensitive).</summary>
        public string? RegionNameContains { get; set; }

        /// <summary>
        /// In strict mode a chunk read failure aborts the scan with an exception.
        /// Otherwise the chunk is skipped and a warning is logged.
        /// </summary>
        public bool Strict { get; set; }

        /// <summary>Cap on results returned from FindAll.</summary>
        public int? MaxResults { get; set; }

        /// <summary>Optional progress callback (bytesScanned, totalBytes).</summary>
        public Action<ulong, ulong>? Progress { get; set; }
    }
}
