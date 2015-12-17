using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace MetadataGenerator {
    class CommandLineOptions {
        [Option('i', "input-file", HelpText = "The input file", Required = true)]
        public string InputFile { get; set; }

        [Option('n', "native", HelpText = "If this flag is present then the output format is native, otherwise is csdl.")]
        public bool Native { get; set; }

        [Option('o', "output-file", HelpText = "The name of the output file. Default value is the FQ type name.")]
        public string OutputFile { get; set; }

        [Option('d', "output-directory", HelpText = "The name of the folder where to save the output file(s). Default value is the current folder.")]
        public string OutputFolder { get; set; }

        [Option('m', "multiple-types", HelpText = "When specified it will create one file per DbContext type found in the assembly.")]
        public bool MultiType { get; set; }

        [Option('t', "type-name", HelpText = "The DbContext Type to extract from the input assembly")]
        public string TypeName { get; set; }

        [Option('r', "resource-prefix", HelpText = "The prefix for the resources. Default is empty string, which will use the first .csdl in the DbContext assembly.", DefaultValue = "")]
        public string ResourcePrefix { get; set; }


        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this,
              (current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
