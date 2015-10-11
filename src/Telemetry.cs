using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.ApplicationInsights;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Reports anonymous usage through ApplicationInsights
    /// </summary>
    public static class Telemetry
    {
        private static TelemetryClient _telemetry = GetAppInsightsClient();
        private const string TELEMETRY_KEY = "367cd134-ade0-4111-a928-c7a1e3b0bb00";
        private static DTEEvents _events;

        private static TelemetryClient GetAppInsightsClient()
        {
            TelemetryClient client = new TelemetryClient();
            client.InstrumentationKey = TELEMETRY_KEY;
            client.Context.Component.Version = ImageOptimizerPackage.Version;
            client.Context.Session.Id = Guid.NewGuid().ToString();
            client.Context.User.Id = (Environment.UserName + Environment.MachineName).GetHashCode().ToString();

            return client;
        }

        /// <summary>
        /// The device name is what identifies what kind of device is calling
        /// </summary>
        public static void Initialize(DTE2 dte)
        {
            _telemetry.Context.Device.Model = dte.Edition;

            if (_events == null)
            {
                _events = dte.Events.DTEEvents;
                _events.OnBeginShutdown += delegate { _telemetry.Flush(); };
            }
        }

        /// <summary>Tracks an event to ApplicationInsights.</summary>
        public static void TrackEvent(string key)
        {
#if !DEBUG
            _telemetry.TrackEvent(key);
#endif
        }

        /// <summary>Tracks any exception.</summary>
        public static void TrackException(Exception ex)
        {
#if !DEBUG
            var telex = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex);
            telex.HandledAt = Microsoft.ApplicationInsights.DataContracts.ExceptionHandledAt.UserCode;
            _telemetry.TrackException(telex);
#endif
        }
    }
}
