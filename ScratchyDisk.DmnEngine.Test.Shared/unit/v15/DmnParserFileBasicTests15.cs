using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - parser")]
    public class DmnParserFileBasicTests15 : DmnParserFileBasicTests14
    {
        protected override SourceEnum Source => SourceEnum.File15;

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public override void ParseNullOrEmptyFileTest(string file)
        {
            Action act = () => DmnParser.Parse15(file);
            act.Should().Throw<DmnParserException>().WithMessage("filePath is null or empty");
        }

        [TestMethod]
        public override void ParseFileNotExistTest()
        {
            Action act = () => DmnParser.Parse15("dsadas");
            act.Should().Throw<DmnParserException>().WithMessage("File * doesn't exist");
        }

        [TestMethod]
        public override void ParseWrongFileTest()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var file = Path.Combine(dir ?? string.Empty, "nlog.config");
            Action act = () => DmnParser.Parse15(file);
            act.Should().Throw<DmnParserException>().WithMessage("Can't parse file *");
        }
    }
}
