using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.PocoMetadata
{
    public class Generator
    {
        public static Metadata Generate(string assemblyFileName)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);
            Assembly assembly = Assembly.LoadFrom(assemblyFileName);
            return Generate(assembly);
        }

        public static Metadata Generate(AssemblyName assemblyName)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
            Assembly assembly = Assembly.Load(assemblyName.FullName);
            return Generate(assembly);
        }

        public static Metadata Generate(Assembly assembly)
        {
            var builder = new PocoMetadataBuilder(new EntityDescription());
            var types = GetTypesFromAssembly(assembly);
            var metadata = builder.BuildMetadata(types);
            return metadata;
            //var names = types.Select(t => t.Name).ToArray();
            //var namejoin = string.Join("\n", names);
            //return assembly.FullName + "\n" + names.Length + " types:\n" + namejoin;
        }

        private static Type[] GetTypesFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes().Where(
                        t =>
                        (t.IsClass && !t.IsAbstract));

            return types.ToArray();
        }

    }
}
