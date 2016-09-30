var fs = require('fs');
var path = require('path');
var breeze = require('breeze-client/breeze.debug');
var handlebars = require('handlebars');

try {
    var inputFileName;
    var sourceFilesFolder;
    var outputFolder;
    var baseClassPath;
    var currentParameter;
    process.argv.forEach(function (val, index) {
        // First parameter is always node itself
        if (val.match('node$|node.exe$') && index === 0) return;

        // Second parameter should be the script
        if (val.match('tsgen.js$') && index === 1) return;

        if (!currentParameter) {
            if (val === '-i' || val === '-input') {
                currentParameter = 'input';
                return;
            } else if (val === '-o' || val === '-output') {
                currentParameter = 'output';
                return;
            } else if (val === '-c' || val === '-camelCase') {
                breeze.NamingConvention.camelCase.setAsDefault();
            } else if (val === '-b' || val === '-baseClass') {
                currentParameter = 'baseClass';
                return;
            } else if (val === '-s' || val === '-sourceFiles') {
                currentParameter = 'sourceFiles';
                return;
            } else {
                throw new Error('Invalid parameter: ' + val);
            }
        }

        if (currentParameter === 'input') {
            inputFileName = val;
        }

        if (currentParameter === 'sourceFiles') {
            sourceFilesFolder = val;
        }

        if (currentParameter === 'output') {
            outputFolder = val;
        }

        if (currentParameter === 'baseClass') {
            baseClassPath = path.resolve(val);
        }

        currentParameter = null;
    });
    if (currentParameter) {
        throw new Error('Invalid usuage of paramter: -' + currentParamter);
    }

    if (!inputFileName || !fs.existsSync(inputFileName)) {
        throw new Error("Must specify a valid input file name.");
    }
    outputFolder = outputFolder || '.';
    sourceFilesFolder = sourceFilesFolder || outputFolder;

    console.log('Generating typescript classes...');
    console.log('Input: ' + path.resolve(inputFileName));
    console.log('Source: ' + path.resolve(sourceFilesFolder));
    console.log('Output: ' + path.resolve(outputFolder));

    // Load metadata.
    var metadata = fs.readFileSync(inputFileName, 'utf8');
    //console.log(metadata);

    // Import metadata
    var metadataStore = breeze.MetadataStore.importMetadata(metadata);
    processRawMetadata(metadataStore);
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

    var regHelperSourceFilename = path.resolve(sourceFilesFolder, '_RegistrationHelper.ts');
    var regHelperFilename = path.resolve(outputFolder, '_RegistrationHelper.ts');
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

    function processRawMetadata(metadataStore) {
        var entityTypes = metadataStore.getEntityTypes();
        metadataStore.modules = entityTypes.map(function (entityType) {
            return { entityType: entityType, path: entityType.shortName };
        });

        var baseClass;
        if (baseClassPath) {
            if (!fs.existsSync(baseClassPath)) {
                throw new Error('Base class file ' + baseClassPath + ' does not exist. Must specify a valid path.');
            }

            var baseClassText = fs.readFileSync(baseClassPath, 'utf8');
            var match = baseClassText.match('module[ \t\r\n]*(.*)[ \t\r\n]*{');
            if (!match || match.length !== 2) {
                throw new Error('Base class file must contain a single module definition.');
            }
            var namespace = match[1].trim();
            
            match = baseClassText.match('module[ \t\r\n]*.*[ \t\r\n]*{[ \t\r\n]*export[ \t\r\n]*class[ \t\r\n]*(.*)[ \t\r\n]*{');
            if (!match || match.length !== 2) {
                throw new Error('Base class file must contain a single module with an exported class.');
            }
            baseClass = namespace + '.' + match[1].trim();
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
                return { name: property.name, dataType: convertDataType(property) };
            });
            entityType.references = metadataStore.modules.filter(function (module) {
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
                entityType.baseClass = entityType.baseEntityType.namespace + '.' + entityType.baseEntityType.shortName;
            } else if (baseClass) {
                entityType.baseClass = baseClass;
                entityType.references.push({ entityType: null, path: path.relative(sourceFilesFolder, baseClassPath.substr(0, baseClassPath.length - 3)) });
            }

            // Set output filename path
            entityType.filename = path.resolve(outputFolder, entityType.shortName + '.ts');

            // Extract custom code from existing file
            entityType.sourceFilename = path.resolve(sourceFilesFolder, entityType.shortName + '.ts');
            if (fs.existsSync(entityType.sourceFilename)) {
                var ts = fs.readFileSync(entityType.sourceFilename, 'utf8');
                entityType.originalFileContent = ts;

                entityType.codereference = extractSection(ts, 'code-reference');
                entityType.code = extractSection(ts, 'code');

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

    function extractSection(content, tag) {
        var matches = content.match('\/\/\/[ \t]*<' + tag + '>.*');
        if (matches && matches.length !== 0) {
            var startTag = matches[0];
            matches = content.match('\/\/\/[ \t]*<\/' + tag + '>.*');
            if (!matches || matches.length === 0) {
                throw new Error('Expected </' + tag + '> closing tag.');
            }
            var endTag = matches[0];

            return content.substring(content.indexOf(startTag) + startTag.length, content.indexOf(endTag)).trim();
        }

        return null;
    }

    function convertDataType(property) {
        if (property.isNavigationProperty) {
            var navigationType = property.entityType.namespace + '.' + property.entityType.shortName;
            if (property.isScalar) {
                return navigationType;
            }
            return navigationType + '[]';
        }

        if (property.isComplexProperty) {
            var complexType = getEntityType(property.complexTypeName);
            return complexType.namespace + '.' + complexType.shortName;
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

    function getEntityType(name) {
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
} catch(e) {
    console.log('Unexpected error occurred: ' + e.message);
} 

