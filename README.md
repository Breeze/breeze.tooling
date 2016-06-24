# Breeze Tooling

This repo is for developer tools that help make it easy to use your domain model with Breeze.

## Overview

The tools described herein form the tool chain set needed to generate TypeScript classes.  They can be invoked from the console as per their respective usage.  Initial input to this set of tools consists of the compiled DbContext and entities.   The final output of the TypeScript Generator is a TypeScript class for each entity and optionally complex types found in the DbContext.

The environment of execution should have the following setup prior to invoking the tools:

- Node.js should be installed.  This is available at nodejs.org.
- Node.exe should be in the PATH environment variable

The following prerequisites are recommended for better understanding:

- Node.js
- Breeze.js
- Handlebars.js

## Metadata Generator

[Metadata Generator](./MetadataGenerator) is the first tool in the tool chain for Entity Framework.  It accepts the DLL containing the DBContext and generates metadata.

## PocoMetadata

[PocoMetadata](./PocoMetadata) is the first tool in the tool chain if you are *not* using Entity Framework or other ORM.  It is a tool that generates Breeze metadata from a C# domain model, optionally 
containing [Data Annotations](https://msdn.microsoft.com/en-us/library/dd901590.aspx).

## TypeScript Entity Generator

[TypeScript Entity Generator](./TypeScriptEntityGen) is the second tool in the tool chain if you are generating non-module entities.  It accepts CSDL or Breeze native metadata from the Metadata Generator and generates TypeScript classes relevant to a DbContext and entities.

## TypeScript Module Generator

[TypeScript Module Generator](./TypeScriptModuleGen) is the second tool in the tool chain if you are generating ES6 *module* entities.  It accepts CSDL or Breeze native metadata from the Metadata Generator and generates TypeScript modules relevant to a DbContext and entities.

## Complete Tool Chain (PowerShell)

A PowerShell script is used to execute the Metadata generator and TypeScript generator in the proper sequence with one command line.  See the [TypeScriptEntityGen](./TypeScriptEntityGen) README for usage.

## Complete Tool Chain (Gulp)

A Gulp script is used to execute the Metadata generator and TypeScript generator in the proper sequence with one command line.  See the [TypeScriptModuleGen](./TypeScriptModuleGen) README for usage.
