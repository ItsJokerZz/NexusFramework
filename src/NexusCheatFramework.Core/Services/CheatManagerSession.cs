using System;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Input;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Services
{
    /// <summary>
    /// Manages the lifecycle of a cheat manager session: detecting open/close
    /// triggers from the controller (via <see cref="PadStatePollingService"/>),
    /// and providing thread-safe Open/Close methods that any front-end
    /// (CLI, WebUI, future overlay) can call.
    ///
    /// Thread safety: uses a single <see cref="SemaphoreSlim"/>(1,1) around
    /// state transitions. All async public methods accept CancellationToken.
    /// </summary>
    public sealed class CheatManagerSession : IDisposable
    {
        private readonly INexusClient _client;
        private readonly ShortcutConfig _config;
        private readonly ILogger _log;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly ShortcutDetector _openDetector;
        private readonly ShortcutDetector _closeDetector;
        private PadStatePollingService? _pollingService;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _disposed;

        private bool _isOpen;
        private string? _lastError;
        private string? _currentTitleId;

        /// <summary>Raised when the open trigger fires (controller shortcut detected).</summary>
        public event Action? OpenRequested;

        /// <summary>Raised when the close trigger fires while the session is open.</summary>
        public event Action? CloseRequested;

        /// <summary>Raised when the session state changes (open/close).</summary>
        public event Action<bool>? StateChanged;

        /// <summary>Whether the cheat manager session is currently open.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Last error message, if any.</summary>
        public string? LastError => _lastError;

        /// <summary>Current title ID from the active process, if available.</summary>
        public string? CurrentTitleId => _currentTitleId;

        /// <summary>Polling interval in ms. Default 60.</summary>
        public int PollIntervalMs { get; set; } = 60;

        public CheatManagerSession(
            INexusClient client,
            ShortcutConfig config,
            ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = logger ?? NullLogger.Instance;

            // Build open detector
            _openDetector = BuildDetector(config, config.CheatManagerTrigger);
            _openDetector.OnTrigger += HandleOpenTrigger;

            // Build close detector
            _closeDetector = BuildDetector(config, config.CloseTrigger);
            _closeDetector.OnTrigger += HandleCloseTrigger;
        }

        private static ShortcutDetector BuildDetector(ShortcutConfig config, CheatManagerShortcut trigger)
        {
            if (trigger == CheatManagerShortcut.Off)
                return new ShortcutDetector(new ShortcutConfig { Enabled = false, Mode = CheatsShortcutMode.Off });

            var holdMs = (int)config.GetEffectiveHoldDuration(trigger).TotalMilliseconds;
            var cfg = new ShortcutConfig
            {
                Enabled = true,
                Mode = MapToCheatsShortcutMode(trigger),
                HoldDuration = TimeSpan.FromMilliseconds(holdMs),
                Debounce = config.Debounce,
                TapMaxDuration = config.TapMaxDuration,
            };
            return new ShortcutDetector(cfg);
        }

        private static CheatsShortcutMode MapToCheatsShortcutMode(CheatManagerShortcut trigger)
        {
            return trigger switch
            {
                CheatManagerShortcut.HoldR3L3 => CheatsShortcutMode.HoldR3L3,
                CheatManagerShortcut.HoldL2Triangle => CheatsShortcutMode.HoldL2Triangle,
                CheatManagerShortcut.LongHoldOptions => CheatsShortcutMode.LongHoldOptions,
                CheatManagerShortcut.LongHoldShare => CheatsShortcutMode.LongHoldShare,
                CheatManagerShortcut.HoldL1R1Square => CheatsShortcutMode.Off, // custom handling
                CheatManagerShortcut.HoldL1R1Triangle => CheatsShortcutMode.Off, // custom handling
                CheatManagerShortcut.Custom => CheatsShortcutMode.Off, // custom handling
                _ => CheatsShortcutMode.Off,
            };
        }

        /// <summary>
        /// Starts the pad polling loop. If /pad_state is unavailable, logs a
        /// single warning and stops polling — does not spam.
        /// </summary>
        public void StartPolling()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var interval = PollIntervalMs > 0 ? PollIntervalMs : 60;
            bool warnedOnce = false;

            _pollTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var pad = await _client.GetPadStateAsync(token).ConfigureAwait(false);
                        if (pad != null)
                        {
                            warnedOnce = false;
                            var buttons = (PadButton)pad.Buttons;

                            // For HoldL1R1Square and HoldL1R1Triangle, we need custom detection
                            // since ShortcutDetector doesn't natively support these combos
                            if (_config.CheatManagerTrigger == CheatManagerShortcut.HoldL1R1Square)
                                HandleCustomCombo(buttons, PadButton.L1 | PadButton.R1 | PadButton.Square,
                                    _config.GetEffectiveHoldDuration(CheatManagerShortcut.HoldL1R1Square), _openDetector);
                            else if (_config.CheatManagerTrigger == CheatManagerShortcut.HoldL1R1Triangle)
                                HandleCustomCombo(buttons, PadButton.L1 | PadButton.R1 | PadButton.Triangle,
                                    _config.GetEffectiveHoldDuration(CheatManagerShortcut.HoldL1R1Triangle), _openDetector);
                            else if (_config.CheatManagerTrigger == CheatManagerShortcut.Custom)
                            {
                                var required = PadButton.None;
                                foreach (var b in _config.CustomOpenChord)
                                    required |= b;
                                HandleCustomCombo(buttons, required,
                                    _config.GetEffectiveHoldDuration(CheatManagerShortcut.Custom), _openDetector);
                            }
                            else
                            {
                                _openDetector.Update(buttons, DateTime.UtcNow);
                            }

                            // Close detector
                            if (_isOpen)
                            {
                                if (_config.CloseTrigger == CheatManagerShortcut.HoldL1R1Square)
                                    HandleCustomCombo(buttons, PadButton.L1 | PadButton.R1 | PadButton.Square,
                                        _config.GetEffectiveHoldDuration(CheatManagerShortcut.HoldL1R1Square), _closeDetector);
                                else if (_config.CloseTrigger == CheatManagerShortcut.HoldL1R1Triangle)
                                    HandleCustomCombo(buttons, PadButton.L1 | PadButton.R1 | PadButton.Triangle,
                                        _config.GetEffectiveHoldDuration(CheatManagerShortcut.HoldL1R1Triangle), _closeDetector);
                                else if (_config.CloseTrigger == CheatManagerShortcut.Custom)
                                {
                                    var required = PadButton.None;
                                    foreach (var b in _config.CustomOpenChord)
                                        required |= b;
                                    HandleCustomCombo(buttons, required,
                                        _config.GetEffectiveHoldDuration(CheatManagerShortcut.Custom), _closeDetector);
                                }
                                else
                                {
                                    _closeDetector.Update(buttons, DateTime.UtcNow);
                                }
                            }
                        }
                        else if (!warnedOnce)
                        {
                            _log.Warn("Pad polling: /pad_state returned null — controller shortcut unavailable. Use WebUI hotkey or HTTP trigger instead.");
                            warnedOnce = true;
                            // Stop polling since pad_state is unavailable
                            break;
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        if (!warnedOnce)
                        {
                            _log.Warn($"Pad polling failed: {ex.Message} — controller shortcut unavailable. Use WebUI hotkey or HTTP trigger instead.");
                            warnedOnce = true;
                            break;
                        }
                        await Task.Delay(1000, token).ConfigureAwait(false);
                        continue;
                    }

                    await Task.Delay(interval, token).ConfigureAwait(false);
                }
            }, token);
        }

        // Custom combo state tracking
        private bool _customHeld;
        private DateTime _customHeldSince;

        private void HandleCustomCombo(PadButton buttons, PadButton required, TimeSpan holdDuration, ShortcutDetector detector)
        {
            bool held = (buttons & required) == required;
            if (held)
            {
                if (!_customHeld)
                {
                    _customHeld = true;
                    _customHeldSince = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - _customHeldSince >= holdDuration)
                {
                    // Fire the detector's trigger
                    detector.Update(buttons, DateTime.UtcNow);
                    _customHeld = false;
                }
            }
            else
            {
                _customHeld = false;
            }
        }

        private void HandleOpenTrigger()
        {
            _log.Info("cheat-manager: Controller open trigger detected");
            _ = OpenAsync();
        }

        private void HandleCloseTrigger()
        {
            _log.Info("cheat-manager: Controller close trigger detected");
            _ = CloseAsync();
        }

        /// <summary>
        /// Opens the cheat manager session. Idempotent — calling open twice
        /// is a no-op (returns same state).
        /// </summary>
        public async Task<CheatManagerState> OpenAsync(CancellationToken ct = default)
        {
            await _stateLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_isOpen)
                {
                    _log.Debug("cheat-manager: OpenAsync called but already open (no-op)");
                    return GetState();
                }

                _log.Info("cheat-manager: Opening session");

                // Refresh title info
                try
                {
                    var process = await _client.GetActiveProcessAsync(ct).ConfigureAwait(false);
                    _currentTitleId = process.TitleId;
                }
                catch (Exception ex)
                {
                    _log.Warn($"cheat-manager: Failed to get active process: {ex.Message}");
                    _currentTitleId = null;
                }

                _isOpen = true;
                _lastError = null;
                _log.Info($"cheat-manager: Session opened (TID={_currentTitleId ?? "unknown"})");

                StateChanged?.Invoke(true);
                OpenRequested?.Invoke();
                return GetState();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _log.Error("cheat-manager: Failed to open session", ex);
                return GetState();
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Closes the cheat manager session. Idempotent.
        /// </summary>
        public async Task<CheatManagerState> CloseAsync(CancellationToken ct = default)
        {
            await _stateLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_isOpen)
                {
                    _log.Debug("cheat-manager: CloseAsync called but already closed (no-op)");
                    return GetState();
                }

                _log.Info("cheat-manager: Closing session");
                _isOpen = false;
                _lastError = null;

                StateChanged?.Invoke(false);
                CloseRequested?.Invoke();
                _log.Info("cheat-manager: Session closed");
                return GetState();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _log.Error("cheat-manager: Failed to close session", ex);
                return GetState();
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Returns the current session state without acquiring the lock.
        /// </summary>
        public CheatManagerState GetState()
        {
            return new CheatManagerState
            {
                IsOpen = _isOpen,
                TitleId = _currentTitleId,
                LastError = _lastError,
            };
        }

        public void StopPolling()
        {
            if (_cts == null) return;
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch { /* best effort */ }
            _cts = null;
            _pollTask = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopPolling();
            _openDetector.OnTrigger -= HandleOpenTrigger;
            _closeDetector.OnTrigger -= HandleCloseTrigger;
            _stateLock.Dispose();
        }
    }

    /// <summary>
    /// Snapshot of the cheat manager session state.
    /// </summary>
    public sealed record CheatManagerState
    {
        public bool IsOpen { get; init; }
        public string? TitleId { get; init; }
        public string? LastError { get; init; }
    }
}
