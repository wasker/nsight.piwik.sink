using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nsight.Piwik.Sink
{
    /// <summary>
    /// Sink options.
    /// </summary>
    public sealed class PiwikSinkOptions
    {
        /// <summary>
        /// Gets or sets app "host" name (e.g. "myapp").
        /// </summary>
        public string AppHostName { get; set; }

        /// <summary>
        /// Gets or sets Piwik API instance.
        /// </summary>
        public Api.IPiwikApi Api { get; set; }

        /// <summary>
        /// Gets or sets telemetry source.
        /// </summary>
        public Core.Impl.ITelemetrySource TelemetrySource { get; set; }

        /// <summary>
        /// Validates sink options.
        /// </summary>
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(AppHostName))
            {
                throw new ArgumentException("Application host name must be specified.", nameof(AppHostName));
            }
            if (null == Api)
            {
                throw new ArgumentNullException(nameof(Api));
            }
            if (null == TelemetrySource)
            {
                throw new ArgumentNullException(nameof(TelemetrySource));
            }
        }
    }
}
