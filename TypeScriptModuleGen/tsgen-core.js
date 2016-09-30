var fs = require('fs');
var path = require('path');
var breeze = require('breeze-client/breeze.debug');
var handlebars = require('handlebars');

module.exports = {
  generate: generate
};

// config structure
//   inputFileName: 
//   outputFolder: 
//   camelCase: 
//   baseClassFileName:
//   sourceFilesFolder: 
function generate(config) {
  config.outputFolder = config.outputFolder || '.';
  config.sourceFilesFolder = config.sourceFilesFolder || config.outputFolder;
  if (config.camelCase) {
    breeze.NamingConvention.camelCase.setAsDefault();
  }
  console.log('Generating typescript classes...');
  console.log('Input: ' + path.resolve(config.inputFileName));
  console.log('Source: ' + path.resolve(config.sourceFilesFolder));
  console.log('Output: ' + path.resolve(config.outputFolder));
  console.log('CamelCase: ' + !!config.camelCase);

  // Load metadata.
  var metadata = fs.readFileSync(config.inputFileName, 'utf8');
  //console.log(metadata);

  // Import metadata
  var metadataStore = breeze.MetadataStore.importMetadata(metadata);
  processRawMetadata(metadataStore, config);
  //console.log(metadataStore.getEntityTypes());

  // Load and compile typescript template
  var templateFilename = path.resolve(__dirname, 'entity.template.txt');
  var template = fs.readFileSync(templateFilename, 'utf8');
  var compiledTemplate = handlebars.compile(template);

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

  // Generate registration helper
  metadataStore.generatedAt = new Date();
  metadataStore.namespace = metadataStore.getEntityTypes()[0].namespace;
  templateFilename = path.resolve(__dirname, 'register.template.txt');
  template = fs.readFileSync(templateFilename, 'utf8');
  compiledTemplate = handlebars.compile(template);
  var ts = compiledTemplate(metadataStore);

  var regHelperSourceFilename = path.resolve(config.sourceFilesFolder, '_RegistrationHelper.ts');
  var regHelperFilename = path.resolve(config.outputFolder, '_RegistrationHelper.ts');
  var regHelperOriginalContent;
  if (fs.existsSync(regHelperSourceFilename)) {
    regHelperOriginalContent = fs.readFileSync(regHelperSourceFilename, 'utf8');
  }
  // Don't overwrite file if nothing has changed.
  if (regHelperOriginalContent !== ts) {
    fs.writeFileSync(regHelperFilename, ts, 'utf8');
  } else {
    console.log(regHelperSourceFilename + " hasn't changed. Skipping...");
  }
}

function processRawMetadata(metadataStore, config) {
  var entityTypes = metadataStore.getEntityTypes();
  metadataStore.modules = entityTypes.map(function (entityType) {
    return { entityType: entityType, path: entityType.shortName };
  });

  var baseClass = config.baseClassName;
  if (baseClass) {
    console.log('Injected base class: ' + baseClass);
  }

  entityTypes.forEach(function (entityType) {
    if (!entityType.getProperties) {
      entityType.getProperties = function () {
        return entityType.dataProperties;
      }
    }
    var properties = entityType.getProperties().filter(function (property) {
      return !property.isInherited;
    });
    entityType.properties = properties.map(function (property) {
      return { name: property.name, dataType: convertDataType(metadataStore, property) };
    });
    entityType.imports = metadataStore.modules.filter(function (module) {
      if (module.entityType === entityType) {
        return false;
      }

      if (module.entityType === entityType.baseEntityType) {
        return true;
      }

      return hasDependency(entityType, module.entityType);
    });
    entityType.generatedAt = new Date();
    if (entityType.baseEntityType) {
      // entityType.baseClass = entityType.baseEntityType.namespace + '.' + entityType.baseEntityType.shortName;
      entityType.baseClass = entityType.baseEntityType.shortName;
    } else if (baseClass) {
      entityType.baseClass = baseClass;
      //entityType.references.push({
      //  entityType: null,
      //  path: path.relative(config.sourceFilesFolder, baseClassFileName.substr(0, baseClassFileName.length - 3))
      //});
    }

    // Set output filename path
    entityType.filename = path.resolve(config.outputFolder, entityType.shortName + '.ts');

    // Extract custom code from existing file
    entityType.sourceFilename = path.resolve(config.sourceFilesFolder, entityType.shortName + '.ts');
    if (fs.existsSync(entityType.sourceFilename)) {
      var ts = fs.readFileSync(entityType.sourceFilename, 'utf8');
      entityType.originalFileContent = ts;

      entityType.codeimport = extractSection(ts, 'code-import', entityType.shortName);
      // entityType.codereference = extractSection(ts, 'code-reference');
      entityType.code = extractSection(ts, 'code', entityType.shortName);

      // Extract optional initializer function
      var matches = entityType.code.match('\/\/\/[ \t]*\[[ \t]*Initializer[ \t]*\][ \t\r\n]*(public)?[ \t\r\n]+static[ \t\r\n]+([0-9|A-Z|a-z]+)');
      if (matches && matches.length != 0) {
        entityType.initializerFn = matches[2];
      }

      if (entityType.initializerFn) {
        console.log('Initializer function "' + entityType.initializerFn + '" discovered for entity type: ' + entityType.shortName);
      } else {
        console.log('No initializer function discovered for entity type: ' + entityType.shortName);
      }
    }
  });
}

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

function convertDataType(metadataStore, property) {
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
    return complexType.shortName;
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
  if (dataType === breeze.DataType.String || dataType === breeze.DataType.Guid) {
    return 'string';
  }
  return 'any';
}

function getEntityType(metadataStore, name) {
  var types = metadataStore.getEntityTypes().filter(function (entityType) {
    return entityType.name === name;
  });

  if (types.length === 1)
    return types[0];

  return null;
}

function hasDependency(entityType, dependentEntityType) {
  var complexMatches = entityType.dataProperties.filter(function (property) {
    return !property.isInherited && property.isComplexProperty && property.complexTypeName === dependentEntityType.name;
  });

  if (complexMatches.length !== 0) {
    return true;
  }

  return entityType.navigationProperties.filter(function (property) {
    return !property.isInherited && property.entityType === dependentEntityType;
  }).length !== 0
}


