var fs = require('fs');
var path = require('path');
var backing; // breeze-client/adapter-model-library-backing-store
var breeze; // breeze-client
var handlebars = require('handlebars');
var _ = require('lodash');

module.exports = {
  generate: generate
};

/** Generate the TypeScript entity files from Breeze metadata
 * @param {Object}  config 
 * @param {string}  config.inputFileName:      Breeze metadata file
 * @param {string}  config.outputFolder:       Where to write TypeScript files (defaults to current folder)
 * @param {string}  config.sourceFilesFolder:  Location of existing TS entity files (defaults to outputFolder)
 * @param {string}  config.baseClassName:      Base class for TS entities
 * @param {string}  config.baseComplexName:    Base class for TS complex types
 * @param {boolean} config.camelCase:          Whether to use camelCase for TS property names
 * @param {boolean} config.kebabCaseFileNames: Whether to kebab-case-file-names.ts (otherwise PascalCaseFileNames.ts)
 * @param {boolean} config.useEnumTypes:       Whether to output Enums.ts (if the input metadata contains an "enumTypes" section)
 * @param {boolean} config.propertyComments:   Whether to add comments to each data property
 */
async function generate(config) {
  try {
    // try importing as modules; else use require
    backing = await import('breeze-client/adapter-model-library-backing-store');
    breeze = await import('breeze-client');  
  } catch {
    backing = require('breeze-client/adapter-model-library-backing-store');
    breeze = require('breeze-client');   
  }
  generateCore(config);
}

/** Generate the TypeScript entity files from Breeze metadata
 * @param {Object}  config 
 * @param {string}  config.inputFileName:      Breeze metadata file
 * @param {string}  config.outputFolder:       Where to write TypeScript files (defaults to current folder)
 * @param {string}  config.sourceFilesFolder:  Location of existing TS entity files (defaults to outputFolder)
 * @param {string}  config.baseClassName:      Base class for TS entities
 * @param {string}  config.baseComplexName:    Base class for TS complex types
 * @param {boolean} config.camelCase:          Whether to use camelCase for TS property names
 * @param {boolean} config.kebabCaseFileNames: Whether to kebab-case-file-names.ts (otherwise PascalCaseFileNames.ts)
 * @param {boolean} config.useEnumTypes:       Whether to output Enums.ts (if the input metadata contains an "enumTypes" section)
 * @param {boolean} config.propertyComments:   Whether to add comments to each data property
 */
function generateCore(config) {
  console.log(config);
  if (!config.inputFileName || !fs.existsSync(config.inputFileName)) {
    throw new Error("Must specify a valid input file name");
  }
  config.outputFolder = config.outputFolder || '.';
  config.sourceFilesFolder = config.sourceFilesFolder || config.outputFolder;
  if (config.camelCase) {
    breeze.NamingConvention.camelCase.setAsDefault();
  }
  backing.ModelLibraryBackingStoreAdapter.register();
  console.log('Generating typescript classes...');
  console.log('Input: ' + path.resolve(config.inputFileName));
  console.log('Source: ' + path.resolve(config.sourceFilesFolder));
  console.log('Output: ' + path.resolve(config.outputFolder));
  console.log('BaseClass: ' + config.baseClassName);
  console.log('camelCase: ' + !!config.camelCase);
  console.log('kebabCaseFileNames: ' + !!config.kebabCaseFileNames);
  console.log('useEnumTypes: ' + !!config.useEnumTypes);
  console.log('propertyComments: ' + !!config.propertyComments);

  handlebars.registerHelper('camelCase', function(str) {
    return _.camelCase(str);
  });

  // Load metadata.
  var metadata = fs.readFileSync(config.inputFileName, 'utf8');
  //console.log(metadata);

  // Import metadata
  var metadataStore = breeze.MetadataStore.importMetadata(metadata);

  if (config.useEnumTypes) {
    // until breeze adds the enumTypes to the metadataStore
    var metajson = JSON.parse(metadata);
    var enumTypes = metajson.enumTypes;
    metadataStore.enumTypes = enumTypes;
  }

  processRawMetadata(metadataStore, config);
  //console.log(metadataStore.getEntityTypes());

  // Load and compile typescript template
  var compiledTemplate = compileTemplate('entity.template.txt');

  // Generate typescript classes for each entity
  metadataStore.getEntityTypes().forEach(function (entityType) {
    var ts = compiledTemplate(entityType);

    // Don't overwrite file if nothing has changed.
    if (entityType.originalFileContent !== ts) {
      fs.writeFileSync(entityType.filename, ts, 'utf8');
    } else {
      console.log(entityType.sourceFilename + " hasn't changed. Skipping...");
    }
  });

  metadataStore.generatedAt = new Date();
  metadataStore.namespace = metadataStore.getEntityTypes()[0].namespace;
  metadataStore.codePrefix = config.codePrefix;

  // Generate registration helper
  compiledTemplate = compileTemplate('register.template.txt');
  var regModules = metadataStore.modules.filter(m => !m.entityType.isAbstract);
  var ts = compiledTemplate({ codePrefix: metadataStore.codePrefix, modules: regModules });
  var filename = fileNameCase('RegistrationHelper', config) + '.ts';
  writeIfChanged(filename, ts, config);


  // Generate entity model
  compiledTemplate = compileTemplate('entityModel.template.txt');
  var ts = compiledTemplate(metadataStore);
  var filename = fileNameCase('EntityModel', config) + '.ts';
  writeIfChanged(filename, ts, config);


  // Generate metadata.ts
  compiledTemplate = compileTemplate('metadata.template.txt');
  var ts = compiledTemplate({codePrefix: config.codePrefix, metadata: metadata});
  var filename = fileNameCase('Metadata', config) + '.ts';
  writeIfChanged(filename, ts, config);

  // Generate enums.ts
  if (config.useEnumTypes) {
    compiledTemplate = compileTemplate('enum.template.txt');
    var ts = compiledTemplate(metadataStore);
    var filename = fileNameCase('Enums', config) + '.ts';
    writeIfChanged(filename, ts, config);
  }


}

/**
 * Preprocess the metadata for each entity before file generation.
 * - Remove properties that are defined on base classes
 * - Set the root base class
 * - Set the imports using hasDependency
 * - Get the custom code blocks from existing file
 */
function processRawMetadata(metadataStore, config) {
  var entityTypes = metadataStore.getEntityTypes();
  metadataStore.modules = entityTypes.map(function (entityType) {
    var histName = 'hist' + entityType.shortName.toLowerCase();
    var hasHist = _.some(entityTypes, et => histName === et.shortName.toLowerCase());
    return { entityType: entityType, path: entityType.shortName, 
      moduleName: fileNameCase(entityType.shortName, config), hasHist: hasHist  };
  });


  if (config.useEnumTypes && metadataStore.enumTypes) {
    metadataStore.enumModules = metadataStore.enumTypes.map(function (enumType) {
      return { entityType: enumType, path: enumType.shortName, moduleName: fileNameCase("Enums", config) };
    });
  }

  var baseClass = config.baseClassName;
  if (baseClass) {
    console.log('Injected base entity class: ' + baseClass);
  }
  var baseComplex = config.baseComplexName;
  if (baseComplex) {
    console.log('Injected base complex type class: ' + baseComplex);
  }

  var allModules = metadataStore.modules.concat(metadataStore.enumModules || []);
  
  entityTypes.forEach(function (entityType) {
    if (!entityType.getProperties) {
      entityType.getProperties = function () {
        return entityType.dataProperties;
      }
    }
    var properties = entityType.getProperties().filter(function (property) {
      return !property.baseProperty;
    });
    entityType.properties = properties.map(function (property) {
      
      return { name: property.name, dataType: convertDataType(metadataStore, property, config.useEnumTypes), isNullable: property.isNullable,
        comment: config.propertyComments && getPropertyComment(property) };
    });

    if (entityType.baseEntityType) {
      // entityType.baseClass = entityType.baseEntityType.namespace + '.' + entityType.baseEntityType.shortName;
      entityType.baseClass = entityType.baseEntityType.shortName;
    } else if (baseClass && !entityType.isComplexType) {
      entityType.baseClass = baseClass;
      //entityType.references.push({
      //  entityType: null,
      //  path: path.relative(config.sourceFilesFolder, baseClassFileName.substr(0, baseClassFileName.length - 3))
      //});
    } else if (baseComplex && entityType.isComplexType) {
      entityType.baseClass = baseComplex;
    }
    var baseModulePrefix = (entityType.baseClass == baseClass || entityType.baseClass == baseComplex) ? '../' : './';
    entityType.baseClassModuleName = baseModulePrefix + fileNameCase(entityType.baseClass, config);
    entityType.imports = allModules.filter(function (module) {
      // baseClass is already imported in the template
      if (module.entityType === entityType || module.path === entityType.baseClass) {
        return false;
      }

      return hasDependency(entityType, module.entityType);
    });
    entityType.generatedAt = new Date();

    // Set output filename path
    entityType.filename = path.resolve(config.outputFolder, fileNameCase(entityType.shortName, config) + '.ts');

    entityType.abstract = entityType.isAbstract ? 'abstract ' : '';

    // Extract custom code from existing file
    entityType.sourceFilename = path.resolve(config.sourceFilesFolder, fileNameCase(entityType.shortName, config) + '.ts');
    if (fs.existsSync(entityType.sourceFilename)) {
      var ts = fs.readFileSync(entityType.sourceFilename, 'utf8');
      entityType.originalFileContent = ts;

      entityType.codeimport = extractSection(ts, 'code-import', entityType.shortName);
      entityType.modulecode = extractSection(ts, 'module-code', entityType.shortName);
      // entityType.codereference = extractSection(ts, 'code-reference');
      entityType.code = extractSection(ts, 'code', entityType.shortName);

      // Extract optional initializer function
      var matches = entityType.code.match('\/\/\/[ \t]*\[[ \t]*Initializer[ \t]*\][ \t\r\n]*(public)?[ \t\r\n]+static[ \t\r\n]+([0-9|A-Z|a-z]+)');
      if (matches && matches.length != 0) {
        entityType.initializerFn = matches[2];
      } else {
        entityType.initializerFn = 'initializer';
        entityType.generateInitializer = true;
      }

      if (entityType.initializerFn) {
        console.log('Initializer function "' + entityType.initializerFn + '" discovered for entity type: ' + entityType.shortName);
      } else {
        console.log('No initializer function discovered for entity type: ' + entityType.shortName);
      }
    }
  });
}

/** Get the contents of a custom code block */
function extractSection(content, tag, sourceFileName) {
  var matches = content.match('\/\/\/[ \t]*<' + tag + '>.*');
  if (matches && matches.length !== 0) {
    var startTag = matches[0];
    matches = content.match('\/\/\/[ \t]*<\/' + tag + '>.*');
    if (!matches || matches.length === 0) {
      throw new Error('Expected </' + tag + '> closing tag. ->: ' + sourceFileName);
    }
    var endTag = matches[0];

    return content.substring(content.indexOf(startTag) + startTag.length, content.indexOf(endTag)).trim();
  }

  return null;
}

/** Get the TypeScript data type of the given property */
function convertDataType(metadataStore, property, useEnumTypes) {
  if (property.isNavigationProperty) {
    // var navigationType = property.entityType.namespace + '.' + property.entityType.shortName;
    var navigationType = property.entityType.shortName;
    if (property.isScalar) {
      return navigationType;
    }
    return navigationType + '[]';
  }

  if (property.isComplexProperty) {
    var complexType = getEntityType(metadataStore, property.complexTypeName);
    // return complexType.namespace + '.' + complexType.shortName;
    if (!complexType) console.log("Cannot find complex type " + property.complexTypeName);
    return complexType.shortName;
  }

  if (useEnumTypes && property.enumType) {
    return getShortName(property.enumType);
  }

  var dataType = property.dataType;
  if (dataType.isNumeric) {
    return 'number';
  }

  if (dataType === breeze.DataType.Boolean) {
    return 'boolean';
  }

  if (dataType === breeze.DataType.DateTime || dataType === breeze.DataType.DateTimeOffset || dataType === breeze.DataType.Time) {
    return 'Date';
  }
  
  if (dataType === breeze.DataType.DateOnly) {
    return 'Date';
  }
  
  if (dataType === breeze.DataType.String || dataType === breeze.DataType.Guid) {
    return 'string';
  }
  return 'any';
}

/** Get a data type comment for the given property */
function getPropertyComment(property) {
  if (property.isDataProperty) {
    var com = property.enumType ? 'Enum' : property.isComplexProperty ? 'Complex' : property.dataType.name;
    if (property.maxLength) {
      com += '(' + property.maxLength + ')';
    } 
    if (property.isPartOfKey) {
      com += ' key';
    }
    if (property.relatedNavigationProperty) {
      com += ' FK ' + property.relatedNavigationProperty.name;
    }
    return com;
  } else if (property.isNavigationProperty) {
    if (property.foreignKeyNames.length && property.foreignKeyNames.length) {
      return 'FK ' + property.foreignKeyNames.join(',');
    } else {
      return 'Inv FK ' + property.invForeignKeyNames.join(',');
    }
  }
}


/** Get the EntityType of the given name */
function getEntityType(metadataStore, name) {
  var types = metadataStore.getEntityTypes().filter(function (entityType) {
    return entityType.name === name;
  });

  if (types.length === 1)
    return types[0];

  return null;
}

/** Determine if entityType has a property of type dependentEntityType */
function hasDependency(entityType, dependentEntityType) {
  var complexMatches = entityType.dataProperties.filter(function (property) {
    if (property.baseProperty) {
      return false;
    } 
    if (property.isComplexProperty && property.complexTypeName === dependentEntityType.name) return true;
    if (property.enumType && getShortName(property.enumType) === dependentEntityType.shortName) {
      return true;
    }
  });

  if (complexMatches.length !== 0) {
    return true;
  }

  return entityType.navigationProperties.filter(function (property) {
    return !property.isInherited && property.entityType === dependentEntityType;
  }).length !== 0
}

/** Set the correct format of the filename */
function fileNameCase(filename, config) {
  if (!filename) return filename;
  if (config.kebabCaseFileNames) {
    if (filename.startsWith("I")) {
      return "i" + _.kebabCase(filename.substring(1)).toLowerCase();
    }
    return _.kebabCase(filename).toLowerCase();
  }

  return filename;
}

function getShortName(longname) {
  return longname.split(':')[0]
}

/** Load and compile the template from the given file */
function compileTemplate(filename) {
  var templateFilename = path.resolve(__dirname, filename);
  var template = fs.readFileSync(templateFilename, 'utf8');
  return handlebars.compile(template);
}

/** Write to the output file if the content is different from the source file */
function writeIfChanged(filename, content, config) {
  var sourceFilename = path.resolve(config.sourceFilesFolder, filename);
  var outFilename = path.resolve(config.outputFolder, filename);
  var originalContent;
  if (fs.existsSync(sourceFilename)) {
    originalContent = fs.readFileSync(sourceFilename, 'utf8');
  }
  // Don't overwrite file if nothing has changed.
  if (originalContent !== content) {
    fs.writeFileSync(outFilename, content, 'utf8');
  } else {
    console.log(sourceFilename + " hasn't changed. Skipping...");
  }
  
}
