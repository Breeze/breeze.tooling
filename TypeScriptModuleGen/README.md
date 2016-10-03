# TypeScript Module Generator

TypeScript Generator, a platform independent tool, is the second tool in the tool chain set.  It accepts CSDL or Breeze native metadata from the Metadata Generator and generates TypeScript classes relevant to a DbContext and entities.

**Usage**:	`node[.exe] .\tsgen.js --input <file> [--output <directory>] [--camelCase] [--baseClass <file>]`

**Input(s)**:	A file containing CSDL or Breeze native metadata

**Output(s)**:

- One TypeScript module file per entity and complex type, with the corresponding class export.  
- A helper class (RegistrationHelper.ts) to register all constructors with Breeze.
- A TypeScript barrel (EntityModel.ts) exporting all of the entity classes, for easy import when several classes are required.  
- A helper class (Metadata.ts) that exports the Breeze metadata as a static value, so it can be used by Breeze at runtime. 

**Parameters**:

`--input <file>`: Specifies the file containing the metadata

`--output <directory>`: Optionally specifies the location for the generated TypeScript files. If not specified, the current directory is used as the location

`--sourceFiles <directory>`: Optionally specifies the location to find existing TypeScript files. The `<code>` blocks from these files will be preserved in the corresponding output files. 

`--baseClass <file>`: Optionally specifies a TypeScript base class for all the generated entity classes. The generated entity classes will directly or indirectly inherit from this class. The file must contain a single module and exported class

`--camelCase`: Optionally generates the property names using camel case. This parameter has no effect if the input file contains Breeze native metadata. (See [NamingConvention](http://www.breezejs.com/sites/all/apidocs/classes/NamingConvention.html#property_camelCase))

`--kebabCaseFileNames`: Optionally generate kebab-case-file-names instead of PascalCaseFileNames.

`--useEnumTypes`: Optionally generate an Enums.ts file containing enums defined in the metadata.  Only effective if input file contains an "enumTypes" section.

**Description**:
At the core of the typescript generator sits [handlebars](http://handlebarsjs.com/) which is responsible for generating the actual TypeScript source code. The output that handlebars generate can be customized by modifying the templates.

Note: [node.js](http://nodejs.org/) must be installed and node must be part of the PATH.

### Custom code and custom references

The typescript generator preserves two special sections for each class when regenerating the code. Those sections are `<code-reference>` and `<code>`. The `<code-reference>` section is for custom references and the `<code>` section is for custom methods etc.  Following is an example of a class after it got generated showing the two sections. Everything between the opening and closing tags is preserved.

`/// <reference path="Order.ts" />`

`/// <code-reference> Place custom references between code-reference tags`

`/// </code-reference>`

```
import { EntityBase } from './EntityBase';
import { Order } from './Order';

/// <code-import> Place custom imports between <code-import> tags

/// </code-import>

export class InternationalOrder extends EntityBase {

   /// <code> Place custom code between <code> tags
   
   /// </code>

   // Generated code. Do not place code below this line.
   orderID: number;
   customsDescription: string;
   exciseTax: number;
   rowVersion: number;
   order: Order;
}
```

**Files**: 

`node_modules` (Directory containing the third-party node libraries including Breeze)

`entity.template.txt` (Handlebars template for an entity class)

`register.template.txt` (Handlebars template for the ctor registration helper class.

`tsgen.js` (The node script)

## Complete Tool Chain

A [Gulp](http://gulpjs.com/) script is used to execute the Metadata generator and Typescript generator in the proper sequence with one command line.

Before using, edit the `gulpfile.js` to change the paths to match your environment.  The existing paths in the file are for demonstration, and use the Northwind model from [breeze.server.net](https://github.com/Breeze/breeze.server.net).  

You'll need to put the path to the correct DLL in the `generateMetadata` function, and set the output directory and TypeScript base class in the `generateEntities` function.

You will also need to run `npm install` prior to running `gulp`, to make sure all the dependencies are installed.

**Usage**:	`gulp generate`

**Input(s)**:	An assembly containing the DbContext and Entity classes.

**Output(s)**:	One TypeScript module file with the corresponding class export per entity and complex type plus a helper class to register all constructors with Breeze.

Note: [node.js](http://nodejs.org/) and npm must be installed, and node must be part of the PATH.


**Files**:	

`node_modules` (Directory containing the third-party node libraries including Breeze)

`entity.template.txt` (Handlebars template for an entity class)

`entityModel.template.txt` (Handlebars template for the barrel exporting all entities)

`register.template.txt` (Handlebars template for the ctor registration helper class)

`enum.template.txt` (Handlebars template for the file containing the enum classes)

`enum.template.txt` (Handlebars template for the static metadata)

`tsgen-core.js` (The typescript generator node script)

`tsgen.js` (The command line arguments reader that calls tsgen-core)

`gulpfile.js` (The gulp script)
