using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Org.BouncyCastle.Utilities.Encoders
{
    /// <summary>
    /// A replacement for Bouncy Castle's Hex encoder.
    /// </summary>
    public static class Hex
    {
        /// <summary>
        /// Converts byte buffer to a hex string.
        /// </summary>
        /// <param name="buffer">Buffer to convert.</param>
        public static string ToHexString(byte[] buffer)
        {
            var result = new StringBuilder(buffer.Length * 2);
            foreach (var b in buffer)
            {
                result.AppendFormat("{0:x2}", b);
            }

            return result.ToString();
        }
    }
}
