/** @tossdocs-ignore */
function isDifferentArray() {
  var a = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : [];
  var b = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : [];
  return a.length !== b.length || a.some(function (item, index) {
    return !Object.is(item, b[index]);
  });
}

export { isDifferentArray };
