# Breeze Entity Generator

This tool generates TypeScript classes from Breeze metadata.  The generated classes match the server-side classes and the 
query responses, so you can develop your application with typed entities.

Breeze metadata can be generated from [EntityFramework](https://github.com/Breeze/northwind-demo/blob/master/server/STEPS-Server-NET5.md#configure-the-program-class-to-generate-metadata), [NHibernate](https://github.com/Breeze/breeze.server.net/blob/master/AspNet/Breeze.ContextProvider.NH/NHMetadataBuilder.cs), [POCO
classes](https://github.com/Breeze/breeze.tooling/tree/master/PocoMetadata), or [handwritten](http://breeze.github.io/doc-js/metadata-by-hand-details.html).

**Usage**:	Write your JavaScript file, `generate-entities.js`:
```
const  tsGen = require('node_modules/breeze-entity-generator/tsgen-core');

tsGen.generate({
  inputFileName: 'MyProject.metadata.json',
  outputFolder: './src/app/model/entities/',
  camelCase: true,
  kebabCaseFileNames: true,
  baseClassName: 'BaseEntity',
  baseComplexName: 'BaseComplex',
  codePrefix: 'MyProject',
  useEnumTypes: true
});
```
Then run `node[.exe] generate-entities.js`

**Input(s)**:	A file containing CSDL or Breeze native metadata

**Output(s)**:

- One TypeScript module file per entity and complex type, with the corresponding class export.  
- A helper class (RegistrationHelper.ts) to register all constructors with Breeze.
- A TypeScript barrel (EntityModel.ts) exporting all of the entity classes, for easy import when several classes are required.  
- A helper class (Metadata.ts) that exports the Breeze metadata as a static value, so it can be used by Breeze at runtime. 

## Config Parameters

`inputFileName`: Specifies the file containing the metadata

`outputFolder`: Optionally specifies the location for the generated TypeScript files. If not specified, the current directory is used as the location

`sourceFilesFolder`: Optionally specifies the location to find existing TypeScript files.  The `<code>` blocks from these files will be preserved in the corresponding output files.  If not specified, the outputFolder will be used.

`baseClassName`: Optionally specifies a TypeScript base class for all the generated entity classes. The generated entity classes will directly or indirectly inherit from this class. The file must contain a single module and exported class.

`baseComplexName`: Optionally specifies a TypeScript base class for all the generated complex type (value type) classes. The generated classes will directly or indirectly inherit from this class. The file must contain a single module and exported class.

`camelCase`: Optionally generates the property names using camel case. This parameter has no effect if the input file contains Breeze native metadata. (See [NamingConvention](http://breeze.github.io/doc-js/api-docs/classes/namingconvention.html#camelcase))

`codePrefix`: Name to put in front of the generated Metadata class and the RegistrationHelper class

`kebabCaseFileNames`: Optionally generate kebab-case-file-names instead of PascalCaseFileNames.

`useEnumTypes`: Optionally generate an Enums.ts file containing enums defined in the metadata.  Only effective if input metadata contains an "enumTypes" section.

## Description
At the core of the typescript generator sits [handlebars](http://handlebarsjs.com/) which is responsible for generating the actual TypeScript source code. The output that handlebars generate can be customized by modifying the templates.

Note: [node.js](http://nodejs.org/) must be installed and node must be part of the PATH.

### Custom code and imports

The typescript generator preserves three special sections for each class when regenerating the code. Those sections are `<code-import>`, `<code>` and `<module-code>`. The `<code-import>` section is for custom imports, the `<code>` section is for custom methods, etc and `<module-code>` section is for module level code eg functions.  Following is an example of a class after it got generated showing the three sections. Everything between the opening and closing tags is preserved.

```
import { EntityBase } from './EntityBase';
import { Order } from './Order';

/// <code-import> Place custom imports between <code-import> tags

/// </code-import>

/// <module-code> Place module level code between <module-code> tags

/// </module-code>

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


## Files	

`node_modules` (Directory containing the third-party node libraries including Breeze)

`entity.template.txt` (Handlebars template for an entity class)

`entityModel.template.txt` (Handlebars template for the barrel exporting all entities)

`register.template.txt` (Handlebars template for the ctor registration helper class)

`enum.template.txt` (Handlebars template for the file containing the enum classes)

`metadata.template.txt` (Handlebars template for the static metadata)

`tsgen-core.js` (The typescript generator node script)


## Release Notes

1.0.0 - Initial release as an npm package
1.1.0 - Work with breeze-client .mjs modules (as well as older CJS bundles)
1.2.0 - Add support for Complex Type base class and not-null (!) assertions.  Breaking change: entity.template.txt now imports baseClassName from the parent directory.
1.2.1 - Fix enum type references
1.3.0 - Use TypeScript enum types instead of string unions for enums.
1.3.1 - Use ordinal values instead of strings for enum definitions, e.g. `Green = 2`.  Template change only.
1.3.2 - Include './enums' in entity-model.ts.  Template change only.