using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using System;
using System.CodeDom;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Breeze.Persistence.EFCore;

namespace MetadataGenerator
{

    class Program
    {
        static readonly CommandLineOptions Options = new CommandLineOptions();

        static void Main(string[] args)
        {
            Console.WriteLine("MetadataGenerator " + args.ToAggregateString(" "));
            var parser = new CommandLine.Parser(ps => { ps.MutuallyExclusive = true; ps.HelpWriter = Console.Out; });

            if (!parser.ParseArguments(args, Options))
            {
                return;
            }
            var assemblyName = GetAssemblyName(Options.InputFile);

            if (!File.Exists(assemblyName))
            {
                Console.WriteLine("The specified file {0} cannot be found", assemblyName);
                return;
            }

            var dbContextTypes = GetDbContextTypesFromAssembly(assemblyName);

            if (dbContextTypes.Length == 1 || Options.TypeName != null)
            {
                if (Options.TypeName != null)
                {
                    try
                    {
                        var type = dbContextTypes.SingleOrDefault(t => t.Name == Options.TypeName || t.FullName == Options.TypeName);
                        if (type != null)
                        {
                            ProcessType(type, Options.OutputFile ?? Options.TypeName + ".json");
                        }
                        else
                        {
                            Console.WriteLine("The type specified {0} can not be found in the assembly {1}",
                                              Options.TypeName, assemblyName);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Console.WriteLine("There are several types named {0} in the assembly {1}",
                                          Options.TypeName, assemblyName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                else
                {
                    var type = dbContextTypes.First();
                    ProcessType(type, Options.OutputFile ?? type.FullName + ".json");
                }
            }
            else
            {
                // Hadle multitype scenario.
                //foreach (var type in dbContextTypes) {
                //}
            }
        }

        private static void ProcessType(Type type, string fileName)
        {
            var metadata = GetMetadataFromType(type);
            if (!string.IsNullOrEmpty(metadata))
            {
                var fileNameFullPath = string.IsNullOrEmpty(Options.OutputFolder) ? 
                    Path.GetFullPath(fileName) : Path.Combine(Options.OutputFolder, fileName);

                if (Options.Native)
                {
                    var tempFileName = Path.GetTempFileName();
                    WriteMetadataToFile(metadata, tempFileName);
                    ConvertCsdlToNative(tempFileName, fileNameFullPath);
                    File.Delete(tempFileName);
                }
                else
                {
                    WriteMetadataToFile(metadata, fileNameFullPath);
                }
            }
        }

        private static void ConvertCsdlToNative(string csdlFileName, string nativeFileName)
        {
            var processStartInfo = new ProcessStartInfo(@"node")
                {
                    Arguments = String.Format("convertJson.js {0} {1}", csdlFileName, nativeFileName),
                    WorkingDirectory = @".\NodeFiles"
                };

            if (nativeFileName == string.Empty)
            {
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = false;
                processStartInfo.RedirectStandardInput = false;
                // We don't need the StdErr as we don't want to polute the output.
                processStartInfo.RedirectStandardError = true;
            }

            try
            {
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception was thrown while starting node.js. {0}", ex);
            }
        }

        private static void WriteMetadataToFile(string metadata, string fileName)
        {
            File.WriteAllText(fileName, metadata);
        }

        /// <summary>
        /// This method will invoke the Breezer.WebApi.EFContextProvider extract the csdl fot the type
        /// </summary>
        /// <param name="type">The type to extract the csdl for</param>
        /// <returns>The metadata, or an empty string</returns>
        /// <remarks>
        /// When using a DbFirst approach EF thwros two kind of exceptions:
        /// 1. InvalidOperationException is there is no connection string for the DbContext we are working on.
        /// 2. UnintentionalCodeFirstException if it finds the connection string for the DbContext. 
        ///     UnintentionalCodeFirstException inherits from InvalidOperationException
        /// </remarks>
        private static string GetMetadataFromType(Type type)
        {
            try
            {
                if (type.BaseType == typeof(System.Data.Entity.DbContext) || type == typeof(ObjectContext)) //project is using EF6
                {
                    var providerType = typeof(EFContextProvider<>).MakeGenericType(type);
                    var provider = (ContextProvider)Activator.CreateInstance(providerType);
                    var metadata = provider.Metadata();

                    return metadata;
                }
                else if (type.BaseType == typeof(Microsoft.EntityFrameworkCore.DbContext)) //project is using EF Core
                {
                    var provider = (Microsoft.EntityFrameworkCore.DbContext)Activator.CreateInstance(type);
                    var pm = new EFPersistenceManager<Microsoft.EntityFrameworkCore.DbContext>(provider);
                    var metadata = pm.Metadata();

                    return metadata;
                }
                else
                {
                    Console.WriteLine("Could not interpret database context");
                    return null;
                }
            }
            //catch (InvalidOperationException iex) // This is might be because we have a DbFirst DbContext, so let's try it out.
            //{
            //    try {
            //        return EFContextProvider<object>.GetMetadataFromDbFirstAssembly(type.Assembly, Options.ResourcePrefix);
            //    }
            //    catch (Exception ex) {
            //        Console.WriteLine("An exception was thrown while processing the dbfirst assembly {0}. {1}", type.Assembly.FullName, ex);
            //    }
            //}
            catch (Exception ex)
            {
                Console.WriteLine("An exception was thrown while processing {0}. {1}", type.FullName, ex);
            }
            return string.Empty;
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

        private static Type[] GetDbContextTypesFromAssembly(string assemblyName)
        {
            var assembly = Assembly.LoadFrom(assemblyName);

            var types = assembly.GetExportedTypes().Where(
                        t =>
                        (t.IsClass && !t.IsAbstract &&
                         (t.IsSubclassOf(typeof(DbContext))
                          || t.IsSubclassOf(typeof(ObjectContext)) 
                          || t.IsSubclassOf(typeof(Microsoft.EntityFrameworkCore.DbContext))
                         )));

            return types.ToArray();
        }
    }
}
