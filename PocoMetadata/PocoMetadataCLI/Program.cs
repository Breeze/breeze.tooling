using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Breeze.PocoMetadata
{
    class Program
    {
        static readonly CommandLineOptions Options = new CommandLineOptions();

        static void Main(string[] args)
        {
            var procname = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            Console.WriteLine(procname + ' ' + string.Join(" ", args));
            var parser = new CommandLine.Parser(ps => { ps.MutuallyExclusive = true; ps.HelpWriter = Console.Out; });

         if (!parser.ParseArguments(args, Options))
         {
            return;
         }
		 
         var assemblyNames = Options.InputFile
            .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
            .Select(name => GetAssemblyName(name.Trim('\"')))
            .ToArray();

         if (!assemblyNames.All(File.Exists))
         {
            Console.WriteLine("The specified file {0} cannot be found", assemblyNames.FirstOrDefault());
            return;
         }

         string outfile = GetFilePath();
         EntityDescriptor descriptor = new EntityDescriptor();
         if (!string.IsNullOrEmpty(Options.EntityDescriptor))
         {
            var descriptorAssemblyName = GetAssemblyName(Options.EntityDescriptor);
            var descriptorAssembly = Assembly.LoadFrom(descriptorAssemblyName);
            var descriptorTypes = descriptorAssembly.GetExportedTypes()
               .Where(t => typeof(EntityDescriptor).IsAssignableFrom(t))
               .ToArray();

            if (descriptorTypes.Length > 1)
               throw new ArgumentException("Found more than one EntityDescriptor implementation");
            else if (descriptorTypes.Length == 0)
               throw new ArgumentException("No EntityDescriptor implementation found");

            descriptor = (EntityDescriptor) Activator.CreateInstance(descriptorTypes[0]);
         }
         else if (assemblyNames.Any(a => a.Contains("Northwind")))
            descriptor = new NorthwindEntityDescriptor();

            var metadata = Generator.Generate(assemblyNames, descriptor);
            var json = ToJson(metadata);

            if (outfile != null)
            {
                Console.WriteLine("Writing to " + outfile);
                File.WriteAllText(outfile, json);
            }
            else
            {
                Console.WriteLine(json);
            }
            Console.WriteLine("Done");
        }

        private static string GetFilePath()
        {
            var fileName = Options.OutputFile;
            if (string.IsNullOrEmpty(fileName)) return null;

            return string.IsNullOrEmpty(Options.OutputFolder) ?
                Path.GetFullPath(fileName) : Path.Combine(Options.OutputFolder, fileName);
        }

        private static string ToJson(Metadata metadata)
        {
            var serializerSettings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            return json;

        }

        private static string GetAssemblyName(string fileName)
        {
            var path = Path.GetFullPath(fileName);
            var extension = Path.GetExtension(path);
            if (extension != ".dll")
            {
                return Path.ChangeExtension(path, extension + ".dll");
            }
            return path;
        }

    }
}
