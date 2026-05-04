using System;

namespace NexusCheatFramework.Memory
{
    /// <summary>A pending or applied memory patch.</summary>
    public sealed class MemoryPatch
    {
        public ulong Address { get; }
        public byte[] PatchBytes { get; }
        public byte[]? OriginalBytes { get; private set; }
        public bool Applied { get; private set; }

        public MemoryPatch(ulong address, byte[] patchBytes)
        {
            if (patchBytes is null || patchBytes.Length == 0)
                throw new ArgumentException("Patch bytes must be non-empty.", nameof(patchBytes));
            Address = address;
            PatchBytes = (byte[])patchBytes.Clone();
        }

        internal void MarkApplied(byte[] originalBytes)
        {
            OriginalBytes = (byte[])originalBytes.Clone();
            Applied = true;
        }

        internal void MarkRestored() => Applied = false;
    }
}
