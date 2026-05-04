using System;

namespace NexusCheatFramework.Input
{
    /// <summary>
    /// Pure state machine for shortcut detection. Feed it pad samples via
    /// <see cref="Update"/> and it raises <see cref="OnTrigger"/> when the
    /// configured combo fires. Time is supplied by the caller so the logic
    /// can be unit-tested deterministically.
    ///
    /// The detection rules are modeled after etaHEN's
    /// shellui/src/HookFunctions.cpp shortcut block, but reimplemented from
    /// the public behavior — no etaHEN code is copied.
    /// </summary>
    public sealed class ShortcutDetector
    {
        private readonly ShortcutConfig _cfg;
        private bool _heldComboActive;
        private DateTime _heldComboSince;
        private bool _comboTriggered;
        private DateTime _lastFired = DateTime.MinValue;

        // Share-button single-tap state
        private bool _shareDown;
        private DateTime _shareDownAt;

        public ShortcutDetector(ShortcutConfig config)
        {
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
        }

        public event Action? OnTrigger;

        public void Update(PadButton buttons, DateTime now)
        {
            if (!_cfg.Enabled || _cfg.Mode == CheatsShortcutMode.Off) return;
            if (now - _lastFired < _cfg.Debounce && _lastFired != DateTime.MinValue)
            {
                // still in debounce window; reset transient state but don't fire
            }

            switch (_cfg.Mode)
            {
                case CheatsShortcutMode.HoldR3L3:
                    HandleHoldCombo(buttons, PadButton.R3 | PadButton.L3, requireLong: false, now);
                    break;
                case CheatsShortcutMode.HoldL2Triangle:
                    HandleHoldCombo(buttons, PadButton.L2 | PadButton.Triangle, requireLong: false, now);
                    break;
                case CheatsShortcutMode.LongHoldOptions:
                    HandleHoldCombo(buttons, PadButton.Options, requireLong: true, now);
                    break;
                case CheatsShortcutMode.LongHoldShare:
                    HandleHoldCombo(buttons, PadButton.Share, requireLong: true, now);
                    break;
                case CheatsShortcutMode.SingleTapShare:
                    HandleSingleTapShare(buttons, now);
                    break;
            }
        }

        private void HandleHoldCombo(PadButton buttons, PadButton required, bool requireLong, DateTime now)
        {
            bool held = (buttons & required) == required;
            if (held)
            {
                if (!_heldComboActive)
                {
                    _heldComboActive = true;
                    _heldComboSince = now;
                    _comboTriggered = false;
                }
                else if (!_comboTriggered)
                {
                    var dur = now - _heldComboSince;
                    bool ok = requireLong ? dur >= _cfg.HoldDuration : dur >= _cfg.HoldDuration;
                    if (ok)
                    {
                        Fire(now);
                        _comboTriggered = true;
                    }
                }
            }
            else
            {
                _heldComboActive = false;
                _comboTriggered = false;
            }
        }

        private void HandleSingleTapShare(PadButton buttons, DateTime now)
        {
            bool down = (buttons & PadButton.Share) != 0;
            if (down && !_shareDown)
            {
                _shareDown = true;
                _shareDownAt = now;
            }
            else if (!down && _shareDown)
            {
                _shareDown = false;
                if (now - _shareDownAt <= _cfg.TapMaxDuration)
                    Fire(now);
            }
        }

        private void Fire(DateTime now)
        {
            if (now - _lastFired < _cfg.Debounce) return;
            _lastFired = now;
            OnTrigger?.Invoke();
        }
    }
}
