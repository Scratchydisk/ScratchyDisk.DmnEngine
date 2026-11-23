using System;

namespace ScratchyDisk.DmnEngine.Parser
{
    /// <summary>
    /// Shared presentation of <see cref="DmnParser.DmnVersionEnum"/> for tooling.
    /// Keeps the CLI and testbed version labels consistent.
    /// </summary>
    public static class DmnVersionInfo
    {
        /// <summary>
        /// Gets the human-readable DMN version label, for example "DMN 1.6".
        /// </summary>
        public static string GetDisplayName(DmnParser.DmnVersionEnum version)
        {
            return version switch
            {
                DmnParser.DmnVersionEnum.V1_1 => "DMN 1.1",
                DmnParser.DmnVersionEnum.V1_3 => "DMN 1.3",
                DmnParser.DmnVersionEnum.V1_3ext => "DMN 1.3ext",
                DmnParser.DmnVersionEnum.V1_4 => "DMN 1.4",
                DmnParser.DmnVersionEnum.V1_5 => "DMN 1.5",
                DmnParser.DmnVersionEnum.V1_6 => "DMN 1.6",
                _ => $"DMN {version}"
            };
        }

        /// <summary>
        /// Gets the metadata version string, for example "1.6".
        /// </summary>
        public static string GetMetadataVersion(DmnParser.DmnVersionEnum version)
        {
            return version switch
            {
                DmnParser.DmnVersionEnum.V1_1 => "1.1",
                DmnParser.DmnVersionEnum.V1_3 => "1.3",
                DmnParser.DmnVersionEnum.V1_3ext => "1.3",
                DmnParser.DmnVersionEnum.V1_4 => "1.4",
                DmnParser.DmnVersionEnum.V1_5 => "1.5",
                DmnParser.DmnVersionEnum.V1_6 => "1.6",
                _ => version.ToString()
            };
        }
    }
}
