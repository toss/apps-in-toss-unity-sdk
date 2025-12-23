/** @tossdocs-ignore */
var nextUniqueId = 0;
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function generateID() {
  var prefix = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : '';
  nextUniqueId = nextUniqueId + 1;
  return "".concat(prefix).concat(nextUniqueId);
}

export { generateID };
