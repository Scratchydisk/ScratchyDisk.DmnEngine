namespace ScratchyDisk.DmnEngine.Feel
{
    /// <summary>
    /// Bounds on the work a single FEEL expression may request.
    /// </summary>
    /// <remarks>
    /// A FEEL range literal is not bounded by its text length: <c>1..1000000000</c> is
    /// eight characters and a billion values. Iteration constructs (<c>for</c>,
    /// <c>some</c>, <c>every</c>, boxed iteration) therefore apply a limit, so an
    /// untrusted or mistaken expression fails with an evaluation error instead of
    /// exhausting the process. Raise the limit if a model legitimately needs more.
    /// </remarks>
    public static class FeelEvaluationLimits
    {
        /// <summary>
        /// Maximum number of items a range iteration may produce and maximum number of
        /// results a single iteration expression may collect across all of its
        /// iteration clauses. Defaults to 1,000,000. Set to 0 (or a negative value) to
        /// disable the limit.
        /// </summary>
        public static int MaxIterationItems { get; set; } = 1_000_000;
    }
}
