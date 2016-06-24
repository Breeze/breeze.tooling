var argv = require('yargs').argv;
var tsGenCore = require('./tsgen-core');

try {
  var config = argvToConfig;
  tsGenCore.generate(config);
} catch(e) {
  console.log('Unexpected error occurred: ' + e.message);
} 

function argvToConfig() {
  var config = {
    inputFileName: argv.i || argv.input,
    outputFolder: argv.o || argv.outputFolder,
    camelCase: !!(argv.c || argv.camelCase),
    baseClassFileName: argv.b || argv.baseClass,
    sourceFilesFolder: argv.s || argv.sourceFiles,
  }

  if (!inputFileName || !fs.existsSync(inputFileName)) {
    throw new Error("Must specify a valid input file name.");
  }
  return config;
}
