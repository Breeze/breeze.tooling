const  tsGen = require('../tsgen-core');
const fs = require('fs');
const dir = './entities';

if (!fs.existsSync(dir)){
    fs.mkdirSync(dir);
}

tsGen.generate({
  inputFileName: 'metadata.json',
  outputFolder: dir,
  camelCase: true,
  kebabCaseFileNames: true,
  baseClassName: 'BaseEntity',
  codePrefix: 'Test'
});
