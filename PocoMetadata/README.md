# PocoMetadata

PocoMetadata is a tool that generates [Breeze Metadata](http://www.breezejs.com/documentation/breeze-metadata-format) from a C# domain model, optionally containing [Data Annotations](https://msdn.microsoft.com/en-us/library/dd901590.aspx).  No ORM is assumed.

It works by inspecting the C# classes in an assembly, and using an 
[EntityDescriptor](https://github.com/Breeze/breeze.tooling/blob/master/PocoMetadata/PocoMetadataGenerator/EntityDescriptor.cs)
to establish rules for foreign keys, complex types, etc.

The command-line tool, PocoMetadataCLI.exe, uses the default implementation of `EntityDescriptor`, which is *not* useful for most models.  You should subclass EntityDescriptor to provide the rules for your model, and create a version of PocoMetadataCLI that uses your subclass.

Alternatively, you can reference the PocoMetadata.dll from your project to generate the metadata for your application.

**Usage**:  `PocoMetadataCLI[.exe] -i  <file> [-n] [-o <output file>] [-d  <directory>]`

**Parameters**:

`-input-file <file>`: Specifies the assembly containing the class definitions

`-output-file <file>`: The name of the output file.  If omitted, output is written to stdout. 

`-output-directory <directory>`: The name of the directory in which to save the output file(s).  If not specified, the current directory is used as the location.

