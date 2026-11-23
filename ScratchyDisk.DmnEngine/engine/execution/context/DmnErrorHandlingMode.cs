namespace ScratchyDisk.DmnEngine.Engine.Execution.Context
{
    /// <summary>
    /// DMN 1.6 §7.3.8 error handling mode.
    /// </summary>
    public enum DmnErrorHandlingMode
    {
        /// <summary>
        /// Collect semantic errors and continue evaluation (default).
        /// </summary>
        Lenient = 0,
        /// <summary>
        /// Halt evaluation on the first semantic error.
        /// </summary>
        Strict = 1
    }
}
