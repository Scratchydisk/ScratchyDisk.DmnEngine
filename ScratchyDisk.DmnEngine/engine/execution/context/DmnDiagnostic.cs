namespace ScratchyDisk.DmnEngine.Engine.Execution.Context
{
    /// <summary>
    /// A semantic error collected during a lenient DMN execution.
    /// </summary>
    public sealed class DmnDiagnostic
    {
        /// <summary>
        /// Name of the decision where the error was detected.
        /// </summary>
        public string DecisionName { get; }
        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; }
        /// <summary>
        /// Exception type that produced the diagnostic.
        /// </summary>
        public string ExceptionType { get; }

        public DmnDiagnostic(string decisionName, string message, string exceptionType)
        {
            DecisionName = decisionName;
            Message = message;
            ExceptionType = exceptionType;
        }
    }
}
