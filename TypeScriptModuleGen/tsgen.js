var argv = require('yargs').argv;
var tsGenCore = require('./tsgen-core');

try {
  var config = argvToConfig();
  tsGenCore.generate(config);
} catch(e) {
  console.log('Unexpected error occurred: ' + e.stack);
} 

function argvToConfig() {
  var config = {
    inputFileName: argv.i || argv.input,
    outputFolder: argv.o || argv.outputFolder,
    sourceFilesFolder: argv.s || argv.sourceFiles,
    baseClassFileName: argv.b || argv.baseClass,
    camelCase: !!(argv.c || argv.camelCase),
    kebabCaseFileNames: !!(argv.k || argv.kebabCaseFileNames),
    useEnumTypes: !!(argv.e || argv.useEnumTypes)
  }
  return config;
}
