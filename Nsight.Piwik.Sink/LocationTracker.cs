using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nsight.Piwik.Sink
{
    /// <summary>
    /// User location tracker.
    /// </summary>
    internal sealed class LocationTracker
    {
        /// <summary>
        /// Gets or sets URL where user is now.
        /// </summary>
        public Uri CurrentUrl { get; set; }

        /// <summary>
        /// Gets or sets URL where user had came from.
        /// </summary>
        public Uri ReferrerUrl { get; set; }
    }
}
