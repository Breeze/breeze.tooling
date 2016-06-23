using CommandLine;
using CommandLine.Text;

namespace Breeze.PocoMetadata
{
    class CommandLineOptions
    {
        [Option('i', "input-file", HelpText = "The input assembly containing the object model", Required = true)]
        public string InputFile { get; set; }

        [Option('o', "output-file", HelpText = "The name of the output file. If not specified, writes to stdout.")]
        public string OutputFile { get; set; }

        [Option('d', "output-directory", HelpText = "The name of the folder where to save the output file(s). Default value is the current folder.")]
        public string OutputFolder { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
