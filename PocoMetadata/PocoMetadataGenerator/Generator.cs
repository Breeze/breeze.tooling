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
          Assembly assembly = Assembly.LoadFrom(assemblyFileName);
          return Generate(new[] { assembly }, descriptor);
       }

       /// <summary>
       /// Load the assembly from the given AssemblyName, then call <see cref="Generate(Assembly, EntityDescriptor)"/>
       /// </summary>
       /// <param name="assemblyName"></param>
       /// <param name="descriptor"></param>
       /// <returns></returns>
       public static Metadata Generate(AssemblyName assemblyName, EntityDescriptor descriptor)
       {
          Assembly assembly = Assembly.Load(assemblyName.FullName);
          return Generate(new [] { assembly }, descriptor);
       }

      /// <summary>
      /// Load the assembly from the given files, then call <see cref="Generate(Assembly, EntityDescriptor)"/>
      /// </summary>
      /// <param name="assemblyFileNames"></param>
      /// <param name="descriptor"></param>
      /// <returns></returns>
      public static Metadata Generate(string[] assemblyFileNames, EntityDescriptor descriptor)
        {
            Assembly[] assembly = assemblyFileNames.Select(Assembly.LoadFrom).ToArray();
            return Generate(assembly, descriptor);
        }

        /// <summary>
        /// Load the assembly from the given AssemblyNamse, then call <see cref="Generate(Assembly, EntityDescriptor)"/>
        /// </summary>
        /// <param name="assemblyNames"></param>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public static Metadata Generate(AssemblyName[] assemblyNames, EntityDescriptor descriptor)
        {
            Assembly[] assembly = assemblyNames.Select(n => Assembly.Load(n.FullName)).ToArray();
            return Generate(assembly, descriptor);
        }

        /// <summary>
        /// Generate metadata from the given Assembly.
        /// </summary>
        /// <param name="assembly">Assembly containing entity types</param>
        /// <param name="descriptor">Descriptor used to evaluate types and resolve metadata</param>
        /// <returns></returns>
        public static Metadata Generate(Assembly[] assembly, EntityDescriptor descriptor)
        {
            var builder = new PocoMetadataBuilder(descriptor);
            var types = GetTypesFromAssemblies(assembly);
            var metadata = builder.BuildMetadata(types);
            return metadata;
        }

        private static Type[] GetTypesFromAssemblies(Assembly[] assembly)
        {
            var types = assembly.SelectMany(s => s.GetExportedTypes());

            return types.ToArray();
        }

    }
}
