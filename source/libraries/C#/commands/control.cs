using System;
using System.Threading.Tasks;

namespace NexusFramework
{
    public partial class Library
    {
        public async Task LogMessage(string? message = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            if (message != null)
                message = message.Replace("\\n", Environment.NewLine);

            await Utilities.RequestAsync<string>(Target, "log_message", $"msg={message}", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task SendNotification(string? message = null)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            if (message != null)
                message = message.Replace("\\n", Environment.NewLine);

            await Utilities.RequestAsync<string>(Target, "send_notify", $"msg={message}", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task SetFanThreshold(int limit)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, $"fan_threshold?limit={limit}", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task SetPowerState(Definitions.PowerStates value)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            string state = value switch
            {
                Definitions.PowerStates.Off => "turnoff",
                Definitions.PowerStates.Standby => "standby",
                Definitions.PowerStates.Reboot => "reboot",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };

            await Utilities.RequestAsync<string>(Target, $"system_state?state={state}", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task AlarmBuzzer(Definitions.BuzzerModes mode)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, $"ring_buzzer?type={(int)mode}", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task LaunchApplication(string titleId)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, $"launch_app?tid={titleId}", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task ExitApplication()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, "exit_app", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task LaunchURI(string URI)
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, $"launch_uri?tid={URI}", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

        public async Task DumpKernel()
        {
            ThrowNotConnectedException();
            ThrowVersioMismatchException();

            await Utilities.RequestAsync<string>(Target, "dump_kernel", "", System.Net.Http.HttpMethod.Get, null, true).ConfigureAwait(false);
        }

    }
}