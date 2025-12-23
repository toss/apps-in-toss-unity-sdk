/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function difference(xs, ys) {
  return differenceWith(xs, ys, function (x, y) {
    return x === y;
  });
}
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function differenceWith(xs, ys, areItemsEqual) {
  return xs.filter(function (x) {
    return !ys.some(function (y) {
      return areItemsEqual(x, y);
    });
  });
}

export { difference, differenceWith };
