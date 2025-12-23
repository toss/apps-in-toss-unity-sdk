/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function range(start, end) {
  var step = arguments.length > 2 && arguments[2] !== undefined ? arguments[2] : 1;

  var _start = end === undefined ? 0 : start;

  var _end = end === undefined ? start : end;

  var output = [];
  var current = _start;

  while (current < _end) {
    output.push(current);
    current += step;
  }

  return output;
}

export { range };
