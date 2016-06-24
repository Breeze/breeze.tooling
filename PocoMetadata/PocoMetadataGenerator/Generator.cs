using System;
using System.Linq;
using System.Reflection;

namespace Breeze.PocoMetadata
{
    public class Generator
    {
        public static Metadata Generate(string assemblyFileName, EntityDescriptor descriptor)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);
            Assembly assembly = Assembly.LoadFrom(assemblyFileName);
            return Generate(assembly, descriptor);
        }

        public static Metadata Generate(AssemblyName assemblyName, EntityDescriptor descriptor)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
            Assembly assembly = Assembly.Load(assemblyName.FullName);
            return Generate(assembly, descriptor);
        }

        public static Metadata Generate(Assembly assembly, EntityDescriptor descriptor)
        {
            var builder = new PocoMetadataBuilder(descriptor);
            var types = GetTypesFromAssembly(assembly);
            var metadata = builder.BuildMetadata(types);
            return metadata;
            //var names = types.Select(t => t.Name).ToArray();
            //var namejoin = string.Join("\n", names);
            //return assembly.FullName + "\n" + names.Length + " types:\n" + namejoin;
        }

        private static Type[] GetTypesFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes();

            return types.ToArray();
        }

    }
}
