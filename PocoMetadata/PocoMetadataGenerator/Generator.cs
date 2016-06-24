using System;
using System.Linq;
using System.Reflection;

namespace Breeze.PocoMetadata
{
    /// <summary>
    /// Class that loads the assembly and calls the PocoMetadataBuilder
    /// </summary>
    public class Generator
    {
        /// <summary>
        /// Load the assembly from the given file, then call <see cref="Generate(Assembly, EntityDescriptor)"/>
        /// </summary>
        /// <param name="assemblyFileName"></param>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public static Metadata Generate(string assemblyFileName, EntityDescriptor descriptor)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);
            Assembly assembly = Assembly.LoadFrom(assemblyFileName);
            return Generate(assembly, descriptor);
        }

        /// <summary>
        /// Load the assembly from the given AssemblyName, then call <see cref="Generate(Assembly, EntityDescriptor)"/>
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public static Metadata Generate(AssemblyName assemblyName, EntityDescriptor descriptor)
        {
            //Assembly assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
            Assembly assembly = Assembly.Load(assemblyName.FullName);
            return Generate(assembly, descriptor);
        }

        /// <summary>
        /// Generate metadata from the given Assembly.
        /// </summary>
        /// <param name="assembly">Assembly containing entity types</param>
        /// <param name="descriptor">Descriptor used to evaluate types and resolve metadata</param>
        /// <returns></returns>
        public static Metadata Generate(Assembly assembly, EntityDescriptor descriptor)
        {
            var builder = new PocoMetadataBuilder(descriptor);
            var types = GetTypesFromAssembly(assembly);
            var metadata = builder.BuildMetadata(types);
            return metadata;
        }

        private static Type[] GetTypesFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes();

            return types.ToArray();
        }

    }
}
