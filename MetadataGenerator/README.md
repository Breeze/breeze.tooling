# Metadata Generator

Metadata Generator generates Breeze metadata from Entity Framework.  It accepts the DLL containing the DBContext and generates metadata.

**Usage**:	`metagenerator[.exe] –i  <file> [-n] [-o <output file>] [-d  <directory>] [-m] –t <DBContext Type>`

**Input(s)**:	Assembly containing the `DbContext`, or the `ObjectContext` class definition

**Output(s)**:	CSDL or Breeze native metadata stored in one or more files using the naming convention `<Fully Qualified DBContext Type Names>.json`

**Description**:	This tool has a dependency on the nodeFiles directory. It loads the assembly containing the entities.  For each DbContext, this tool generates a CSDL file.  If there are multiple DbContext, the tool should be run once for each DbContext.  Following this, optionally to obtain Breeze metadata, invoke Node.js that converts the CSDL format to Breeze native format and stores in specified file.

**Parameters**:

`-input-file <file>`: Specifies the assembly containing the DBContext or ObjectContext class definition

`-native`: Optionally, specify this flag if output format is to be native, otherwise output format is CSDL

`-output-file <file>`: The name of the output file. Default value is the fully qualified DbContext  type name 

`-output-directory <directory>`: The name of the directory in which to save the output file(s).  If not specified, the current directory is used as the location.

`-multiple-type`: Optionally, specify this flag to generate one file per DbContext type found in the assembly.

`-type-name`:  The DBContext Type to extract from the input assembly

**Files**:	

`metagenerator.exe`

`<Fully Qualified DBContext Type Names>.json`

## Notes for EF Core

If using Metadata Generator with EF Core, ensure that your Entity Framework Core project targets both .Net Core and .Net Standard Frameworks.  You'll point Metadata Generator to the .Net Standard DLL.

There are also some required configuration additions to the .csproj file. See below for a working sample:

```XML
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netcoreapp2.1;netstandard2.0</TargetFrameworks>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="2.1.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="2.1.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="2.1.1" />
  </ItemGroup>

</Project>
```