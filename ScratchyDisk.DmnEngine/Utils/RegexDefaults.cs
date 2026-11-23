using System;

namespace ScratchyDisk.DmnEngine.Utils
{
    /// <summary>
    /// Shared regular-expression limits.
    /// </summary>
    public static class RegexDefaults
    {
        /// <summary>
        /// Match timeout used for FEEL string and temporal parsing.
        /// </summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    }
}
