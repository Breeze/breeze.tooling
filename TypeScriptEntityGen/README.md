# TypeScript Generator

TypeScript Generator, a platform independent tool, is the second tool in the tool chain set.  It accepts CSDL or Breeze native metadata from the Metadata Generator and generates TypeScript classes relevant to a DbContext and entities.

**Usage**:	`node[.exe] .\tsgen.js -input <file> [-output <directory>] [-camelCase] [-baseClass <file>]`

**Input(s)**:	A file containing CSDL or Breeze native metadata

**Output(s)**:	One typescript file with the corresponding class definition per entity and complex type plus a helper class to register all constructors with Breeze

**Parameters**:

`-input <file>`: Specifies the file containing the metadata

`-output <directory>`: Optionally specifies the location for the generate typescript files. If not specified, the current directory is used as the location

`-camelCase`: Optionally generates the property names using camel case. This parameter has no effect if the input file contains Breeze native metadata. (See [NamingConvention](http://www.breezejs.com/sites/all/apidocs/classes/NamingConvention.html#property_camelCase))

`-baseClass <file>`: Optionally specifies a typescript base class for all the generated entity classes. The generated entity classes will directly or indirectly inherit from this class. The file must contain a single module and exported class

**Description**:
At the core of the typescript generator sits [handlebars](http://handlebarsjs.com/) which is responsible for generating the actual TypeScript source code. The output that handlebars generate can be customized by modifying the templates.

Note: [node.js](http://nodejs.org/) must be installed and node must be part of the PATH.

### Custom code and custom references

The typescript generator preserves two special sections for each class when regenerating the code. Those sections are `<code-reference>` and `<code>`. The `<code-reference>` section is for custom references and the `<code>` section is for custom methods etc.  Following is an example of a class after it got generated showing the two sections. Everything between the opening and closing tags is preserved.

`/// <reference path="Order.ts" />`

`/// <code-reference> Place custom references between code-reference tags`

`/// </code-reference>`

```
module DomainModel.NorthwindIB {

   export class InternationalOrder extends DomainModel.NorthwindIB.Order {
       /// <code> Place custom code between code tags
       
       /// </code>

       // Generated code. Do not place code below this line.
       customsDescription: string;
       exciseTax: number;
       
   }
}
```

**Files**: 

`node_modules` (Directory containing the third-party node libraries including Breeze)

`entity.template.txt` (Handlebars template for an entity class)

`register.template.txt` (Handlebars template for the ctor registration helper class.

`tsgen.js` (The node script)

## Complete Tool Chain

A PowerShell script is used to execute the Metadata generator and Typescript generator in the proper sequence with one command line.

**Usage**:	`PowerShell[.exe] .\tsg.ps1 -assembly <file> [-outputDir <directory>] [-baseClass <file>]`

**Input(s)**:	An assembly containing the DbContext and Entity classes.

**Output(s)**:	One TypeScript file with the corresponding class definition per entity and complex type plus a helper class to register all constructors with Breeze.

**Parameters**:

`-assembly <file>`: Specifies the assembly containing the DbContext and Entities.

`-outputDir <directory>`: Optionally specifies the location for the generate typescript files. If not specified, the current directory is used as the location.

`-baseClass <file>`: Optionally specifies a typescript base class for all the generated entity classes. The generated entity classes will directly or indirectly inherit from this class. The file must contain a single module and exported class.

Note: [node.js](http://nodejs.org/) must be installed and node must be part of the PATH.

Note: The PowerShell script execution policy must be configured to allow for the script to run. See [this link](http://technet.microsoft.com/en-us/library/ee176949.aspx#EEAA) for detailed information.

**Files**:	

`.bin` (The metadata generator binaries)

`node_modules` (Directory containing the third-party node libraries including Breeze)

`entity.template.txt` (Handlebars template for an entity class)

`register.template.txt` (Handlebars template for the ctor registration helper class.

`tsgen.js` (The typescript generator node script)

`tsg.ps1` (The powershell script)
