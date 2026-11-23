using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Test.Unit.Feel
{
    [TestClass]
    [TestCategory("FEEL Types")]
    public class FeelTypeConformanceTests
    {
        [TestMethod]
        public void Number_DoesNotConformToString()
        {
            FeelTypeConformance.IsCompatible(2m, "string").Should().BeFalse();
        }

        [TestMethod]
        public void String_DoesNotConformToNumber()
        {
            FeelTypeConformance.IsCompatible("2", "number").Should().BeFalse();
        }

        [TestMethod]
        public void Number_IsWidenedToDecimal()
        {
            FeelTypeConformance.TryCoerce(10, "number", null, out var result).Should().BeTrue();
            result.Should().Be(10m);
        }

        [TestMethod]
        public void SingletonStringList_CoercesToString()
        {
            FeelTypeConformance.TryCoerce(new List<object> { "foo" }, "string", null, out var result)
                .Should().BeTrue();
            result.Should().Be("foo");
        }

        [TestMethod]
        public void NestedList_IsNotUnwrappedTwice()
        {
            FeelTypeConformance.IsCompatible(new List<object> { new List<object> { 10m } }, "number")
                .Should().BeFalse();
        }

        [TestMethod]
        public void Scalar_CoercesToListOfConformingType()
        {
            FeelTypeConformance.TryCoerce(10m, "list<number>", null, out var result).Should().BeTrue();
            result.Should().BeEquivalentTo(new List<object> { 10m });
        }

        [TestMethod]
        public void Scalar_DoesNotCoerceToListOfOtherType()
        {
            FeelTypeConformance.IsCompatible("foo", "list<number>").Should().BeFalse();
        }

        [TestMethod]
        public void List_RequiresEveryElementToConform()
        {
            var mixed = new List<object> { 1m, 2m, "foo" };
            FeelTypeConformance.IsCompatible(mixed, "list<number>").Should().BeFalse();
        }

        [TestMethod]
        public void Context_AllowsAdditionalComponents()
        {
            var value = new FeelContext();
            value.Put("name", "foo");
            value.Put("age", 10m);
            value.Put("surname", "bar");

            FeelTypeConformance.IsCompatible(value, "context<name:string,age:number>").Should().BeTrue();
        }

        [TestMethod]
        public void Context_RequiresAllDeclaredComponents()
        {
            var value = new FeelContext();
            value.Put("name", "foo");

            FeelTypeConformance.IsCompatible(value, "context<name:string,age:number>").Should().BeFalse();
        }

        [TestMethod]
        public void TypeAlias_IsResolved()
        {
            var aliases = new Dictionary<string, string>
            {
                ["tNameAndAge"] = "context<name:string,age:number>"
            };
            var value = new FeelContext();
            value.Put("name", "foo");
            value.Put("age", 10m);

            FeelTypeConformance.IsCompatible(value, "tNameAndAge", aliases).Should().BeTrue();
        }

        [TestMethod]
        public void Date_CoercesToDateTime()
        {
            var date = new DateOnly(2024, 5, 1);
            FeelTypeConformance.TryCoerce(date, "date and time", null, out var result).Should().BeTrue();
            result.Should().Be(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        [TestMethod]
        public void Boolean_DoesNotConformToString()
        {
            FeelTypeConformance.IsCompatible(true, "string").Should().BeFalse();
        }
    }
}
