namespace ScratchyDisk.DmnEngine.Engine.Execution.Context
{
    /// <summary>
    /// Execution configuration options
    /// </summary>
    public interface IDmnExecutionContextOptions
    {
        /// <summary>
        /// Flag whether to record snapshots of variables and results during the execution (false by default).
        /// This option can be used for execution tracking/audit and/or debugging
        /// </summary>
        bool RecordSnapshots { get; }
        /// <summary>
        /// Flag whether to evaluate the table rules in parallel (true by default).
        /// This option can be used for performance tuning.
        /// </summary>
        bool EvaluateTableRulesInParallel { get; }
        /// <summary>
        /// Flag whether to evaluate the table outputs for positive rules in parallel (false by default).
        /// This option can be used for performance tuning.
        /// </summary>
        bool EvaluateTableOutputsInParallel { get; }
        /// <summary>
        /// Scope of the parsed expression cache (Cache parsed expressions for definition cross contexts by default)
        /// </summary>
        ParsedExpressionCacheScopeEnum ParsedExpressionCacheScope { get; }
        /// <summary>
        /// DMN 1.6 error handling mode (lenient by default).
        /// </summary>
        DmnErrorHandlingMode ErrorHandlingMode { get; }
        /// <summary>
        /// Flag whether to propagate input parameter values to expression alias variables.
        /// </summary>
        bool ResolveInputAliases { get; }
    }
}