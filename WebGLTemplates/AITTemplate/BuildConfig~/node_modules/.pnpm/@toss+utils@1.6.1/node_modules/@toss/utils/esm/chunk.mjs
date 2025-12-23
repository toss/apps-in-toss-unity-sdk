/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function chunk(arr, size) {
  if (size < 1) {
    return [];
  }

  return arr.reduce(function (result, item, index) {
    if (index % size > 0) {
      result[result.length - 1].push(item);
    } else {
      result.push([item]);
    }

    return result;
  }, []);
}

export { chunk };
