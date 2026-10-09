// Test fixture: a CommonJS package that loads Node built-ins with require,
// both with and without the "node:" prefix (review 0004, finding 2).
'use strict';

const nodePath = require('node:path');
const path = require('path');

module.exports = {
  joined: nodePath.posix.join('a', 'b'),
  sameModule: nodePath === path,
};
