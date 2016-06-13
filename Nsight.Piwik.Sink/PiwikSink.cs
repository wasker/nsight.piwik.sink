using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;

namespace Nsight.Piwik.Sink
{
    /// <summary>
    /// Implements activity telemetry sink using Piwik API.
    /// </summary>
    public sealed class PiwikSink
    {
        /// <summary>
        /// Sink options.
        /// </summary>
        private readonly PiwikSinkOptions options;

        /// <summary>
        /// Lock for read/write operations on shared state.
        /// </summary>
        private readonly AsyncReaderWriterLock padlock = new AsyncReaderWriterLock();

        /// <summary>
        /// Gets base app URL.
        /// </summary>
        internal Uri BaseAppUrl { get; }

        /// <summary>
        /// Gets current user location information.
        /// </summary>
        internal LocationTracker Location { get; } = new LocationTracker();

        /// <summary>
        /// Gets current session information.
        /// </summary>
        internal Api.PiwikSessionInfo Session { get; private set; }

        /// <summary>
        /// Gets current environment information.
        /// </summary>
        internal Api.PiwikEnvironmentInfo EnvironmentInfo { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PiwikSink"/> class.
        /// </summary>
        /// <param name="options">Sink options.</param>
        public PiwikSink(PiwikSinkOptions options)
        {
            if (null == options)
            {
                throw new ArgumentNullException(nameof(options));
            }
            options.Validate();

            this.options = options;

            options.TelemetrySource.Activity.OnBeginSessionAsync += Activity_OnBeginSessionAsync;
            options.TelemetrySource.Activity.OnSetEnvironmentInfoAsync += Activity_OnSetEnvironmentInfoAsync;
            options.TelemetrySource.Activity.OnReportViewAsync += Activity_OnReportViewAsync;
            options.TelemetrySource.Activity.OnReportActionAsync += Activity_OnReportActionAsync;
            options.TelemetrySource.Activity.OnEndSessionAsync += Activity_OnEndSessionAsync;

            BaseAppUrl = new Uri($"app://{options.AppHostName}/");
        }

        #region Activity telemetry source listeners

        private async Task Activity_OnBeginSessionAsync(object sender, Core.SessionInfo session)
        {
            string visitorIdHash = null;
            if (!string.IsNullOrEmpty(session.UniqueVisitorId))
            {
                visitorIdHash = 
                    Hex.ToHexString(DigestUtilities.CalculateDigest("MD5", Encoding.UTF8.GetBytes(session.UniqueVisitorId)));
            }

            using (await padlock.WriterLockAsync())
            {
                Session = new Api.PiwikSessionInfo()
                {
                    FirstVisit = session.FirstVisit,
                    LastVisit = session.LastVisit,
                    VisitsCount = session.VisitsCount,
                    UniqueVisitorId = visitorIdHash,
                    UserId = session.UserId
                };
            }
        }

        private async Task Activity_OnEndSessionAsync(object sender, Core.SessionInfo session)
        {
            using (await padlock.WriterLockAsync())
            {
                Session = null;
            }
        }

        private async Task Activity_OnSetEnvironmentInfoAsync(object sender, Core.EnvironmentInfo environment)
        {
            using (await padlock.WriterLockAsync())
            {
                EnvironmentInfo = new Api.PiwikEnvironmentInfo()
                {
                    DeviceName = environment.DeviceName,
                    DeviceType = environment.DeviceType,
                    OperatingSystem = environment.OperatingSystem,
                };
                if (null != environment.DeviceScreen)
                {
                    EnvironmentInfo.DeviceScreen = new Api.PiwikScreenResolution()
                    {
                        Dpi = environment.DeviceScreen.Dpi,
                        Height = environment.DeviceScreen.Height,
                        Width = environment.DeviceScreen.Width
                    };
                }
            }
        }

        private async Task Activity_OnReportViewAsync(object sender, Core.ViewInfo view)
        {
            using (await padlock.WriterLockAsync())
            {
                var viewUrl = new UriBuilder(BaseAppUrl);
                viewUrl.Path = view.AbsolutePath ?? "/";

                //  Advance user in their journey.
                Location.ReferrerUrl = Location.CurrentUrl;
                Location.CurrentUrl = viewUrl.Uri;

                await options.Api.ReportViewAsync(new Api.PiwikViewInfo(Location.CurrentUrl.ToString())
                {
                    ViewName = view.Title,
                    ReferrerUrl = Location.ReferrerUrl?.ToString(),
                    ViewTime = view.Time,
                    Session = Session,
                    EnvironmentInfo = EnvironmentInfo
                });
            }
        }

        private async Task Activity_OnReportActionAsync(object sender, Core.ActionInfo action)
        {
            using (await padlock.ReaderLockAsync())
            {
                await options.Api.ReportEventAsync(new Api.PiwikEventInfo(Location.CurrentUrl?.ToString() ?? BaseAppUrl.ToString(), action.Category ?? "Unknown", action.Verb ?? action.Name)
                {
                    Name = action.Name,
                    ReferrerUrl = Location.ReferrerUrl?.ToString(),
                    Session = Session,
                    EnvironmentInfo = EnvironmentInfo
                });
            }
        }

        #endregion
    }
}
