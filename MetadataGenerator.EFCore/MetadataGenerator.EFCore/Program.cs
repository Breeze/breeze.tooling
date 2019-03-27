using Breeze.Core;
using Breeze.Persistence;
using Breeze.Persistence.EFCore;
using CommandLine;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MetadataGenerator {

  class Program {

    static void Main(string[] args) {
      Console.WriteLine("MetadataGenerator " + args.ToAggregateString(" "));
      var parser = Parser.Default.ParseArguments<CommandLineOptions>(args);
      parser.WithParsed<CommandLineOptions>(o => ProcessArgs(o))
        .WithNotParsed<CommandLineOptions>(errs => HandleParseErrors(errs)); 

    }

    static void ProcessArgs(CommandLineOptions options) {
      
      var assemblyName = GetAssemblyName(options.InputFile);
      
      if (!File.Exists(assemblyName)) {
        Console.WriteLine("The specified file {0} cannot be found", assemblyName);
        return;
      }

      var dbContextTypes = GetDbContextTypesFromAssembly(assemblyName);

      if (dbContextTypes.Length == 1 || options.TypeName != null) {
        if (options.TypeName != null) {
          try {
            var dbContextType = dbContextTypes.SingleOrDefault(t => t.Name == options.TypeName || t.FullName == options.TypeName);
            if (dbContextType != null) {
              ProcessType(dbContextType, options);
            } else {
              Console.WriteLine("The type specified {0} can not be found in the assembly {1}",
                                options.TypeName, assemblyName);
            }
          } catch (InvalidOperationException) {
            Console.WriteLine("There are several types named {0} in the assembly {1}",
                              options.TypeName, assemblyName);
          } catch (Exception ex) {
            Console.WriteLine(ex);
          }
        } else {
          var type = dbContextTypes.First();
          ProcessType(type, options);
        }
      } else {
        throw new Exception("Multiple Context types not yet supported");
      }
    }

    private static void HandleParseErrors(IEnumerable<Error> errs) {
      errs.ToList().ForEach(err => {
        Console.WriteLine(err.ToString());
      });
    }

    private static void ProcessType(Type dbContextType, CommandLineOptions options) {

      var metadata = GetMetadataFromType(dbContextType);
      
      if (!string.IsNullOrEmpty(metadata)) {
        string outputFileNameFullPath;
        if (string.IsNullOrEmpty(options.OutputFile) && string.IsNullOrEmpty(options.OutputFolder)) {
          var outputFolder = Path.GetDirectoryName(options.InputFile);
          var outputFileName = Path.GetFileNameWithoutExtension(options.InputFile) + ".metadata.json";
          outputFileNameFullPath = Path.Combine(outputFolder, outputFileName);
        } else {
          outputFileNameFullPath = string.IsNullOrEmpty(options.OutputFolder) ?
                   Path.GetFullPath(options.OutputFile) : Path.Combine(options.OutputFolder, options.OutputFile);
        }
        WriteMetadataToFile(metadata, outputFileNameFullPath);
        Console.WriteLine("Metadata written to: " + outputFileNameFullPath);
      }
    }



    /// <summary>
    /// This method will invoke the Breeze.EFPersistenceManager to extract the metadata for the 
    /// </summary>
    /// <param name="dbContextType">The type to extract the csdl for</param>
    /// <returns>The metadata, or an empty string</returns>
    /// <remarks>

    /// </remarks>
    private static string GetMetadataFromType(Type dbContextType) {
      try {
        var providerType = typeof(EFPersistenceManager<>).MakeGenericType(dbContextType);
        var dbContextOptionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);

        var dbContextOptionsBuilder = (DbContextOptionsBuilder) Activator.CreateInstance(dbContextOptionsBuilderType);
        // by telling EF Core that we are using an in memory database we don't need to provide a connection string. 'foo' can be any name; it's not used.
        InMemoryDbContextOptionsExtensions.UseInMemoryDatabase(dbContextOptionsBuilder, "foo", null);
        var dbContext = Activator.CreateInstance(dbContextType, new Object[] { dbContextOptionsBuilder.Options } );
        
        var provider = (PersistenceManager)Activator.CreateInstance(providerType, new Object[] { dbContext });
        
        var metadata = provider.Metadata();

        return metadata;
      } catch (Exception ex) {
        Console.WriteLine("An exception was thrown while processing {0}. {1}", dbContextType.FullName, ex);
      }
      return string.Empty;
    }

    private static void WriteMetadataToFile(string metadata, string fileName) {
      File.WriteAllText(fileName, metadata);
    }


    private static string GetAssemblyName(string fileName) {
      var path = Path.GetFullPath(fileName);
      var extension = Path.GetExtension(path);
      if (extension != ".dll") {
        return Path.ChangeExtension(path, extension + ".dll");
      }
      return path;
    }

    private static Type[] GetDbContextTypesFromAssembly(string assemblyName) {
      var assembly = Assembly.LoadFrom(assemblyName);

      var types = assembly.GetExportedTypes().Where(
        t => (t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DbContext))));
      return types.ToArray();
    }
  }
}
