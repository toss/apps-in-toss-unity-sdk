/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function groupBy(data, createKey) {
  return data.reduce(function (result, current) {
    var key = createKey(current);
    var value = result[key];

    if (value == null) {
      result[key] = [current];
    } else {
      value.push(current);
    }

    return result;
  }, {});
}

export { groupBy };
