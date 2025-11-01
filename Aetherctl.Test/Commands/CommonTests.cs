using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Aetherctl.Commands;
using Xunit;

namespace Aetherctl.Test.Commands
{
    public class CommonTests
    {
        [Fact]
        public void IsJsonOutput_ReturnsFalse_WhenNotSet()
        {
            var root = new RootCommand();
            var jsonOpt = new Option<bool>("--json");
            root.AddGlobalOption(jsonOpt);
            Common.JsonOption = jsonOpt;

            var result = root.Parse("");
            Assert.False(Common.IsJsonOutput(result));
        }

        [Fact]
        public void IsJsonOutput_ReturnsTrue_WhenSet()
        {
            var root = new RootCommand();
            var jsonOpt = new Option<bool>("--json");
            root.AddGlobalOption(jsonOpt);
            Common.JsonOption = jsonOpt;

            var result = root.Parse("--json");
            Assert.True(Common.IsJsonOutput(result));
        }

        [Fact]
        public void IsVerbose_ReturnsFalse_WhenNotSet()
        {
            var root = new RootCommand();
            var verboseOpt = new Option<bool>("--verbose");
            root.AddGlobalOption(verboseOpt);
            Common.VerboseOption = verboseOpt;

            var result = root.Parse("");
            Assert.False(Common.IsVerbose(result));
        }

        [Fact]
        public void IsVerbose_ReturnsTrue_WhenSet()
        {
            var root = new RootCommand();
            var verboseOpt = new Option<bool>("--verbose");
            root.AddGlobalOption(verboseOpt);
            Common.VerboseOption = verboseOpt;

            var result = root.Parse("--verbose");
            Assert.True(Common.IsVerbose(result));
        }

        [Fact]
        public void IsQuiet_ReturnsFalse_WhenNotSet()
        {
            var root = new RootCommand();
            var quietOpt = new Option<bool>("--quiet");
            root.AddGlobalOption(quietOpt);
            Common.QuietOption = quietOpt;

            var result = root.Parse("");
            Assert.False(Common.IsQuiet(result));
        }

        [Fact]
        public void IsQuiet_ReturnsTrue_WhenSet()
        {
            var root = new RootCommand();
            var quietOpt = new Option<bool>("--quiet");
            root.AddGlobalOption(quietOpt);
            Common.QuietOption = quietOpt;

            var result = root.Parse("--quiet");
            Assert.True(Common.IsQuiet(result));
        }
    }
}

