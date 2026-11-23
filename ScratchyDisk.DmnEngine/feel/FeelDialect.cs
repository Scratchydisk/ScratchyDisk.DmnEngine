namespace ScratchyDisk.DmnEngine.Feel
{
    /// <summary>
    /// FEEL dialect selected for an expression.
    /// </summary>
    public enum FeelDialect
    {
        /// <summary>
        /// Standard FEEL.
        /// </summary>
        Feel = 0,
        /// <summary>
        /// DMN 1.6 B-FEEL dialect. Recognition only; evaluation is a separate body of work.
        /// </summary>
        BFeel = 1
    }
}
