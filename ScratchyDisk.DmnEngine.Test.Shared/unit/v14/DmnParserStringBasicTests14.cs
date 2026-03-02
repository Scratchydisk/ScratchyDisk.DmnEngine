using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - parser")]
    public class DmnParserStringBasicTests14
    {
        [TestMethod]
        public void ParseStringMethodHappyTest()
        {
            var def = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            def += "<definitions xmlns = \"https://www.omg.org/spec/DMN/20211108/MODEL/\" >";
            def += "</definitions>";

            var parsedDef = DmnParser.ParseString14(def);
            parsedDef.Should().NotBeNull();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void ParseNullOrEmptyDefStringTest(string xmlStr)
        {
            Action act = () => DmnParser.ParseString14(xmlStr);
            act.Should().Throw<DmnParserException>().WithMessage("Missing Dmn Model definition");
        }

        [TestMethod]
        public void ParseWrongStringTest()
        {
            var def = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            def += "<definitions xmlns = \"https://www.omg.org/spec/DMN/20211108/MODEL/\" >";
            def += "</efinitions>";

            Action act = () => DmnParser.ParseString14(def);
            act.Should().Throw<DmnParserException>().WithMessage("Can't parse definition from given string*");
        }
    }
}
