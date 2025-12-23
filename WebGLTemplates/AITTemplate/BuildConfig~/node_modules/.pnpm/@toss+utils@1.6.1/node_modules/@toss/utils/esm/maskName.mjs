/** @tossdocs-ignore */
function maskName(name) {
  var _ref = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {},
      _ref$maskChar = _ref.maskChar,
      maskChar = _ref$maskChar === void 0 ? '*' : _ref$maskChar;

  var firstLetter = name.slice(0, 1); // 두 글자 이름인 경우, 두 번째 글자를 마스킹해야 하기 때문에
  // 마스킹을 하지 않는 마지막 글자의 최소 시작 인덱스는 2가 된다.

  var lastLetterIndex = Math.max(2, name.length - 1);
  var lastLetter = name.slice(lastLetterIndex);
  var middleLength = name.length - 1 - lastLetter.length;
  var middle = Array.from({
    length: middleLength
  }).fill(maskChar).join('');
  return "".concat(firstLetter).concat(middle).concat(lastLetter);
}

export { maskName };
