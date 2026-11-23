using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.4.4 List functions, list replace.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15ListReplaceTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_4_4_ListReplaceByPosition()
        {
            Eval("list replace([2, 4, 7, 8], 3, 6)")
                .Should().BeAssignableTo<List<object>>().Which.Should().Equal(2m, 4m, 6m, 8m);
        }

        [TestMethod]
        public void Dmn15_10_3_4_4_ListReplaceByNegativePosition()
        {
            Eval("list replace([2, 4, 7, 8], -1, 6)")
                .Should().BeAssignableTo<List<object>>().Which.Should().Equal(2m, 4m, 7m, 6m);
        }

        [TestMethod]
        public void Dmn15_10_3_4_4_ListReplaceByMatchFunction()
        {
            Eval("list replace([2, 4, 7, 8], function(item, newItem) item < newItem, 5)")
                .Should().BeAssignableTo<List<object>>().Which.Should().Equal(5m, 5m, 7m, 8m);
        }

        [TestMethod]
        public void Dmn15_10_3_4_4_ListReplaceInvalidPositionReturnsNull()
        {
            Eval("list replace([2, 4, 7, 8], 99, 6)").Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_4_4_ListReplaceInvalidMatchReturnsNull()
        {
            Eval("list replace([2, 4, 7, 8], \"not a function\", 6)").Should().BeNull();
        }
    }
}
