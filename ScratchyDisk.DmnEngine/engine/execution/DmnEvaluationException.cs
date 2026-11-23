namespace ScratchyDisk.DmnEngine.Engine.Execution
{
    /// <summary>
    /// Semantic error raised while parsing, evaluating or converting a DMN expression.
    /// Lenient execution converts this into a <see cref="Context.DmnDiagnostic"/> and
    /// continues; strict execution propagates it.
    /// </summary>
    public class DmnEvaluationException : DmnExecutorException
    {
        public DmnEvaluationException(string message, System.Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
