/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
var get = function get(obj, path, defaultValue) {
  var travel = function travel(regexp) {
    return String.prototype.split.call(path, regexp).filter(Boolean).reduce(function (acc, key) {
      return acc !== null && acc !== undefined ? acc[key] : acc;
    }, obj);
  };

  var result = travel(/[,[\]]+?/) || travel(/[,[\].]+?/);
  return result === undefined || result === obj ? defaultValue : result;
};
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

var set = function set(obj, path, value) {
  // Regex explained: https://regexr.com/58j0k
  var pathArray = path.match(/([^[.\]])+/g);
  pathArray === null || pathArray === void 0 ? void 0 : pathArray.reduce(function (acc, key, i) {
    if (acc[key] == null) {
      acc[key] = {};
    }

    if (i === pathArray.length - 1) {
      acc[key] = value;
    }

    return acc[key];
  }, obj);
  return obj;
};

export { get, set };
