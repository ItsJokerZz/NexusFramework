using System;
using System.Threading;
using System.Threading.Tasks;
using NexusCheatFramework.Input;
using NexusCheatFramework.Logging;
using NexusCheatFramework.Nexus;

namespace NexusCheatFramework.Services
{
    /// <summary>
    /// Polls the console pad state at a configurable interval and drives the
    /// <see cref="ShortcutDetector"/> state machine. When a shortcut fires,
    /// the <see cref="OnMenuTrigger"/> event is raised.
    ///
    /// This service is optional — it requires payload-side support for
    /// the /pad_state endpoint. If unavailable, callers can still drive
    /// the ShortcutDetector from other sources.
    /// </summary>
    public sealed class PadStatePollingService : IDisposable
    {
        private readonly INexusClient _client;
        private readonly ShortcutDetector _detector;
        private readonly ShortcutConfig _config;
        private readonly ILogger _log;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;

        public event Action? OnMenuTrigger;

        public PadStatePollingService(
            INexusClient client,
            ShortcutDetector detector,
            ShortcutConfig config,
            ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = logger ?? NullLogger.Instance;

            _detector.OnTrigger += HandleTrigger;
        }

        private void HandleTrigger()
        {
            _log.Info("Controller shortcut triggered!");
            OnMenuTrigger?.Invoke();
        }

        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var interval = _config.PollInterval.TotalMilliseconds > 0
                ? (int)_config.PollInterval.TotalMilliseconds
                : 33;

            _pollTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var pad = await _client.GetPadStateAsync(token).ConfigureAwait(false);
                        if (pad != null)
                        {
                            _detector.Update((PadButton)pad.Buttons, DateTime.UtcNow);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        // Payload /pad_state not available — log once and slow down
                        _log.Warn($"Pad polling failed: {ex.Message}");
                        await Task.Delay(1000, token).ConfigureAwait(false);
                        continue;
                    }

                    await Task.Delay(interval, token).ConfigureAwait(false);
                }
            }, token);
        }

        public void Stop()
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
            Stop();
            _detector.OnTrigger -= HandleTrigger;
        }
    }
}
