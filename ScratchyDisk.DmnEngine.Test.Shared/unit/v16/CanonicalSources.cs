namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical OMG specification locations used by conformance tests.
    /// </summary>
    public static class CanonicalSources
    {
        public const string Dmn14 = "https://www.omg.org/spec/DMN/1.4";
        public const string Dmn15 = "https://www.omg.org/spec/DMN/1.5";
        public const string Dmn16 = "https://www.omg.org/spec/DMN/1.6";

        /// <summary>
        /// Prefixes a section reference with the applicable specification page. A
        /// reference may cite several versions; the oldest one it names wins, because
        /// that is the version in which the clause was introduced.
        /// </summary>
        public static string For(string reference)
        {
            var url = reference.Contains("DMN 1.4")
                ? Dmn14
                : reference.Contains("DMN 1.5")
                    ? Dmn15
                    : Dmn16;
            return $"{url} — {reference}";
        }
    }
}
