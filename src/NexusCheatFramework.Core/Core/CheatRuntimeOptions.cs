namespace NexusCheatFramework.Core
{
    public sealed class CheatRuntimeOptions
    {
        /// <summary>
        /// If true and a cheat code provides ExpectedBytes, refuse to apply
        /// when the live bytes don't match. Override per-call with
        /// <see cref="ForceApply"/>.
        /// </summary>
        public bool RequireExpectedBytes { get; set; } = true;

        /// <summary>Restore original bytes when a cheat is disabled.</summary>
        public bool RestoreOriginalBytesOnDisable { get; set; } = true;

        /// <summary>If true, ExpectedBytes mismatches are warnings, not failures.</summary>
        public bool ForceApply { get; set; } = false;

        /// <summary>Don't actually write to memory; report what would happen.</summary>
        public bool DryRun { get; set; } = false;

        /// <summary>If a pattern matches multiple addresses, which to use.</summary>
        public AmbiguousMatchPolicy AmbiguousMatchPolicy { get; set; } = AmbiguousMatchPolicy.Reject;
    }

    /// <summary>
    /// How to handle restore bytes when disabling a cheat.
    /// </summary>
    public enum RestorePolicy
    {
        /// <summary>Use the original bytes captured before the first write. Fall back to explicit restore bytes if null.</summary>
        CapturedOriginalFirst,
        /// <summary>Use explicit restoreBytes from the cheat definition first. Fall back to captured original bytes.</summary>
        ExplicitRestoreBytesFirst,
        /// <summary>Only use explicit restoreBytes. If none provided and no captured original bytes, do nothing.</summary>
        ExplicitOnly,
        /// <summary>Only use captured original bytes. Ignore explicit restoreBytes.</summary>
        CapturedOnly,
    }

    public enum AmbiguousMatchPolicy
    {
        /// <summary>Reject the cheat with a clear error.</summary>
        Reject,
        /// <summary>Use the first match. Logs a warning.</summary>
        FirstMatch,
    }
}


