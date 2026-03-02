using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Test.Unit.Feel
{
    [TestClass]
    [TestCategory("FEEL Types")]
    public class FeelTypeTests
    {
        // ==================== FeelTime ====================

        [TestMethod]
        public void FeelTime_CreateAndAccess()
        {
            var t = new FeelTime(14, 30, 0);
            t.Time.Should().Be(new TimeOnly(14, 30, 0));
            t.HasOffset.Should().BeFalse();
            t.Offset.Should().BeNull();
        }

        [TestMethod]
        public void FeelTime_WithOffset()
        {
            var t = new FeelTime(14, 30, 0, TimeSpan.FromHours(2));
            t.HasOffset.Should().BeTrue();
            t.Offset.Should().Be(TimeSpan.FromHours(2));
        }

        [TestMethod]
        public void FeelTime_CompareLocal()
        {
            var t1 = new FeelTime(10, 0, 0);
            var t2 = new FeelTime(14, 0, 0);
            (t1 < t2).Should().BeTrue();
            (t2 > t1).Should().BeTrue();
            (t1 == t2).Should().BeFalse();
        }

        [TestMethod]
        public void FeelTime_CompareWithOffset()
        {
            // 14:00+02:00 = 12:00 UTC
            var t1 = new FeelTime(14, 0, 0, TimeSpan.FromHours(2));
            // 13:00+01:00 = 12:00 UTC
            var t2 = new FeelTime(13, 0, 0, TimeSpan.FromHours(1));
            t1.CompareTo(t2).Should().Be(0);
        }

        [TestMethod]
        public void FeelTime_Subtraction()
        {
            var t1 = new FeelTime(14, 0, 0);
            var t2 = new FeelTime(10, 0, 0);
            (t1 - t2).Should().Be(TimeSpan.FromHours(4));
        }

        [TestMethod]
        public void FeelTime_AddDuration()
        {
            var t = new FeelTime(10, 30, 0);
            var result = t + TimeSpan.FromHours(2);
            result.Time.Should().Be(new TimeOnly(12, 30, 0));
        }

        [TestMethod]
        public void FeelTime_ToString_NoOffset()
        {
            var t = new FeelTime(14, 30, 15);
            t.ToString().Should().Be("14:30:15");
        }

        [TestMethod]
        public void FeelTime_ToString_Utc()
        {
            var t = new FeelTime(14, 30, 15, TimeSpan.Zero);
            t.ToString().Should().Be("14:30:15Z");
        }

        [TestMethod]
        public void FeelTime_ToString_PositiveOffset()
        {
            var t = new FeelTime(14, 30, 15, TimeSpan.FromHours(5));
            t.ToString().Should().Be("14:30:15+05:00");
        }

        // ==================== FeelYmDuration ====================

        [TestMethod]
        public void FeelYmDuration_CreateFromYearsMonths()
        {
            var d = new FeelYmDuration(2, 3);
            d.Years.Should().Be(2);
            d.Months.Should().Be(3);
            d.TotalMonths.Should().Be(27);
        }

        [TestMethod]
        public void FeelYmDuration_CreateFromTotalMonths()
        {
            var d = new FeelYmDuration(15);
            d.Years.Should().Be(1);
            d.Months.Should().Be(3);
            d.TotalMonths.Should().Be(15);
        }

        [TestMethod]
        public void FeelYmDuration_Negative()
        {
            var d = new FeelYmDuration(-14);
            d.Years.Should().Be(-1);
            d.Months.Should().Be(-2);
            d.TotalMonths.Should().Be(-14);
        }

        [TestMethod]
        public void FeelYmDuration_Addition()
        {
            var d1 = new FeelYmDuration(1, 6);
            var d2 = new FeelYmDuration(0, 8);
            var result = d1 + d2;
            result.Years.Should().Be(2);
            result.Months.Should().Be(2);
        }

        [TestMethod]
        public void FeelYmDuration_Subtraction()
        {
            var d1 = new FeelYmDuration(2, 0);
            var d2 = new FeelYmDuration(0, 8);
            var result = d1 - d2;
            result.TotalMonths.Should().Be(16);
        }

        [TestMethod]
        public void FeelYmDuration_Negate()
        {
            var d = new FeelYmDuration(1, 6);
            var result = -d;
            result.TotalMonths.Should().Be(-18);
        }

        [TestMethod]
        public void FeelYmDuration_Multiply()
        {
            var d = new FeelYmDuration(1, 0);
            var result = d * 3;
            result.TotalMonths.Should().Be(36);
        }

        [TestMethod]
        public void FeelYmDuration_Divide()
        {
            var d = new FeelYmDuration(2, 0);
            var result = d / 4;
            result.TotalMonths.Should().Be(6);
        }

        [TestMethod]
        public void FeelYmDuration_DivideByZero()
        {
            var d = new FeelYmDuration(1, 0);
            Action act = () => { var _ = d / 0; };
            act.Should().Throw<DivideByZeroException>();
        }

        [TestMethod]
        public void FeelYmDuration_Compare()
        {
            var d1 = new FeelYmDuration(1, 6);
            var d2 = new FeelYmDuration(2, 0);
            (d1 < d2).Should().BeTrue();
        }

        [TestMethod]
        public void FeelYmDuration_AddToDate()
        {
            var d = new FeelYmDuration(1, 6);
            var date = new DateOnly(2023, 1, 15);
            d.AddTo(date).Should().Be(new DateOnly(2024, 7, 15));
        }

        [TestMethod]
        public void FeelYmDuration_ToString()
        {
            new FeelYmDuration(2, 3).ToString().Should().Be("P2Y3M");
            new FeelYmDuration(2, 0).ToString().Should().Be("P2Y");
            new FeelYmDuration(0, 5).ToString().Should().Be("P5M");
            new FeelYmDuration(-1, -6).ToString().Should().Be("-P1Y6M");
        }

        // ==================== FeelRange ====================

        [TestMethod]
        public void FeelRange_ClosedContains()
        {
            var r = new FeelRange(1m, true, 10m, true);
            r.Contains(1m).Should().Be(true);
            r.Contains(5m).Should().Be(true);
            r.Contains(10m).Should().Be(true);
            r.Contains(0m).Should().Be(false);
            r.Contains(11m).Should().Be(false);
        }

        [TestMethod]
        public void FeelRange_OpenContains()
        {
            var r = new FeelRange(1m, false, 10m, false);
            r.Contains(1m).Should().Be(false);
            r.Contains(5m).Should().Be(true);
            r.Contains(10m).Should().Be(false);
        }

        [TestMethod]
        public void FeelRange_HalfOpen()
        {
            var r = new FeelRange(1m, true, 10m, false);
            r.Contains(1m).Should().Be(true);
            r.Contains(10m).Should().Be(false);
        }

        [TestMethod]
        public void FeelRange_NullValue()
        {
            var r = new FeelRange(1m, true, 10m, true);
            r.Contains(null).Should().BeNull();
        }

        [TestMethod]
        public void FeelRange_UnboundedLow()
        {
            var r = new FeelRange(null, false, 10m, true);
            r.Contains(5m).Should().Be(true);
            r.Contains(-1000m).Should().Be(true);
            r.Contains(11m).Should().Be(false);
        }

        [TestMethod]
        public void FeelRange_StringRange()
        {
            var r = new FeelRange("a", true, "m", true);
            r.Contains("c").Should().Be(true);
            r.Contains("z").Should().Be(false);
        }

        [TestMethod]
        public void FeelRange_ToString()
        {
            new FeelRange(1m, true, 10m, true).ToString().Should().Be("[1..10]");
            new FeelRange(1m, false, 10m, false).ToString().Should().Be("(1..10)");
            new FeelRange(1m, true, 10m, false).ToString().Should().Be("[1..10)");
        }

        [TestMethod]
        public void FeelRange_Equality()
        {
            var r1 = new FeelRange(1m, true, 10m, false);
            var r2 = new FeelRange(1m, true, 10m, false);
            r1.Equals(r2).Should().BeTrue();
        }

        // ==================== FeelContext ====================

        [TestMethod]
        public void FeelContext_PutAndGet()
        {
            var ctx = new FeelContext();
            ctx["name"] = "John";
            ctx["age"] = 30m;
            ctx["name"].Should().Be("John");
            ctx["age"].Should().Be(30m);
            ctx.Count.Should().Be(2);
        }

        [TestMethod]
        public void FeelContext_PreservesInsertionOrder()
        {
            var ctx = new FeelContext();
            ctx["b"] = 2;
            ctx["a"] = 1;
            ctx["c"] = 3;
            ctx.Keys.Should().ContainInOrder("b", "a", "c");
        }

        [TestMethod]
        public void FeelContext_OverwritePreservesPosition()
        {
            var ctx = new FeelContext();
            ctx["a"] = 1;
            ctx["b"] = 2;
            ctx["a"] = 10;
            ctx.Keys.Should().ContainInOrder("a", "b");
            ctx["a"].Should().Be(10);
        }

        [TestMethod]
        public void FeelContext_MissingKeyReturnsNull()
        {
            var ctx = new FeelContext();
            ctx["missing"].Should().BeNull();
        }

        [TestMethod]
        public void FeelContext_Remove()
        {
            var ctx = new FeelContext();
            ctx["a"] = 1;
            ctx["b"] = 2;
            ctx["c"] = 3;
            ctx.Remove("b").Should().BeTrue();
            ctx.Count.Should().Be(2);
            ctx.Keys.Should().ContainInOrder("a", "c");
        }

        [TestMethod]
        public void FeelContext_Merge()
        {
            var ctx1 = new FeelContext();
            ctx1["a"] = 1;
            ctx1["b"] = 2;

            var ctx2 = new FeelContext();
            ctx2["b"] = 20;
            ctx2["c"] = 30;

            ctx1.Merge(ctx2);
            ctx1["a"].Should().Be(1);
            ctx1["b"].Should().Be(20);
            ctx1["c"].Should().Be(30);
            ctx1.Count.Should().Be(3);
        }

        [TestMethod]
        public void FeelContext_Equality()
        {
            var ctx1 = new FeelContext();
            ctx1["a"] = 1;
            ctx1["b"] = "hello";

            var ctx2 = new FeelContext();
            ctx2["a"] = 1;
            ctx2["b"] = "hello";

            ctx1.Equals(ctx2).Should().BeTrue();
        }

        // ==================== FeelFunction ====================

        [TestMethod]
        public void FeelFunction_InvokePositional()
        {
            var fn = new FeelFunction("add", new[] { "a", "b" },
                args => (decimal)args[0] + (decimal)args[1]);
            fn.Invoke(3m, 4m).Should().Be(7m);
        }

        [TestMethod]
        public void FeelFunction_InvokeNamed()
        {
            var fn = new FeelFunction("sub", new[] { "a", "b" },
                args => (decimal)args[0] - (decimal)args[1]);
            var result = fn.InvokeNamed(new Dictionary<string, object> { { "b", 3m }, { "a", 10m } });
            result.Should().Be(7m);
        }

        [TestMethod]
        public void FeelFunction_ToString()
        {
            var fn = new FeelFunction("test", new[] { "x", "y" }, _ => null);
            fn.ToString().Should().Be("function(x, y)");
        }

        // ==================== FeelValueComparer ====================

        [TestMethod]
        public void FeelEqual_NullNull_True()
        {
            FeelValueComparer.FeelEqual(null, null).Should().Be(true);
        }

        [TestMethod]
        public void FeelEqual_NullValue_False()
        {
            FeelValueComparer.FeelEqual(null, 5m).Should().Be(false);
            FeelValueComparer.FeelEqual(5m, null).Should().Be(false);
        }

        [TestMethod]
        public void FeelEqual_SameNumbers()
        {
            FeelValueComparer.FeelEqual(5m, 5m).Should().Be(true);
            FeelValueComparer.FeelEqual(5m, 6m).Should().Be(false);
        }

        [TestMethod]
        public void FeelEqual_CrossNumericTypes()
        {
            FeelValueComparer.FeelEqual(5, 5m).Should().Be(true);
            FeelValueComparer.FeelEqual(5L, 5.0).Should().Be(true);
        }

        [TestMethod]
        public void FeelEqual_Strings()
        {
            FeelValueComparer.FeelEqual("hello", "hello").Should().Be(true);
            FeelValueComparer.FeelEqual("hello", "world").Should().Be(false);
        }

        [TestMethod]
        public void Compare_Numbers()
        {
            FeelValueComparer.Compare(3m, 5m).Should().BeNegative();
            FeelValueComparer.Compare(5m, 5m).Should().Be(0);
            FeelValueComparer.Compare(7m, 5m).Should().BePositive();
        }

        [TestMethod]
        public void Compare_Strings()
        {
            FeelValueComparer.Compare("apple", "banana").Should().BeNegative();
        }

        [TestMethod]
        public void Compare_NullReturnsNull()
        {
            FeelValueComparer.Compare(null, 5m).Should().BeNull();
            FeelValueComparer.Compare(5m, null).Should().BeNull();
        }

        [TestMethod]
        public void FeelAnd_ThreeValued()
        {
            FeelValueComparer.FeelAnd(true, true).Should().Be(true);
            FeelValueComparer.FeelAnd(true, false).Should().Be(false);
            FeelValueComparer.FeelAnd(false, null).Should().Be(false);
            FeelValueComparer.FeelAnd(true, null).Should().BeNull();
            FeelValueComparer.FeelAnd(null, null).Should().BeNull();
        }

        [TestMethod]
        public void FeelOr_ThreeValued()
        {
            FeelValueComparer.FeelOr(true, false).Should().Be(true);
            FeelValueComparer.FeelOr(false, false).Should().Be(false);
            FeelValueComparer.FeelOr(true, null).Should().Be(true);
            FeelValueComparer.FeelOr(false, null).Should().BeNull();
            FeelValueComparer.FeelOr(null, null).Should().BeNull();
        }

        [TestMethod]
        public void FeelNot_ThreeValued()
        {
            FeelValueComparer.FeelNot(true).Should().Be(false);
            FeelValueComparer.FeelNot(false).Should().Be(true);
            FeelValueComparer.FeelNot(null).Should().BeNull();
        }

        // ==================== FeelTypeCoercion ====================

        [TestMethod]
        public void CoerceToFeel_IntToDecimal()
        {
            FeelTypeCoercion.CoerceToFeel(42).Should().Be(42m);
        }

        [TestMethod]
        public void CoerceToFeel_DoubleToDecimal()
        {
            FeelTypeCoercion.CoerceToFeel(3.14).Should().Be(3.14m);
        }

        [TestMethod]
        public void CoerceToFeel_DateTimeToDateTimeOffset()
        {
            var dt = new DateTime(2023, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            var result = FeelTypeCoercion.CoerceToFeel(dt);
            result.Should().BeOfType<DateTimeOffset>();
            ((DateTimeOffset)result).DateTime.Should().Be(dt);
        }

        [TestMethod]
        public void CoerceToFeel_NullReturnsNull()
        {
            FeelTypeCoercion.CoerceToFeel(null).Should().BeNull();
        }

        [TestMethod]
        public void CoerceToClr_DecimalToInt()
        {
            FeelTypeCoercion.CoerceToClr(42m, typeof(int)).Should().Be(42);
        }

        [TestMethod]
        public void CoerceToClr_StringToDecimal()
        {
            FeelTypeCoercion.CoerceToClr("3.14", typeof(decimal)).Should().Be(3.14m);
        }

        [TestMethod]
        public void CoerceToClr_SingletonListToScalar()
        {
            var list = new List<object> { 42m };
            FeelTypeCoercion.CoerceToClr(list, typeof(decimal)).Should().Be(42m);
        }

        [TestMethod]
        public void GetFeelTypeName_Tests()
        {
            FeelTypeCoercion.GetFeelTypeName(42m).Should().Be("number");
            FeelTypeCoercion.GetFeelTypeName(42).Should().Be("number");
            FeelTypeCoercion.GetFeelTypeName("hello").Should().Be("string");
            FeelTypeCoercion.GetFeelTypeName(true).Should().Be("boolean");
            FeelTypeCoercion.GetFeelTypeName(new DateOnly(2023, 1, 1)).Should().Be("date");
            FeelTypeCoercion.GetFeelTypeName(new FeelTime(10, 0, 0)).Should().Be("time");
            FeelTypeCoercion.GetFeelTypeName(DateTimeOffset.Now).Should().Be("date and time");
            FeelTypeCoercion.GetFeelTypeName(new FeelYmDuration(1, 6)).Should().Be("years and months duration");
            FeelTypeCoercion.GetFeelTypeName(TimeSpan.FromHours(5)).Should().Be("days and time duration");
            FeelTypeCoercion.GetFeelTypeName(null).Should().Be("null");
        }
    }
}
