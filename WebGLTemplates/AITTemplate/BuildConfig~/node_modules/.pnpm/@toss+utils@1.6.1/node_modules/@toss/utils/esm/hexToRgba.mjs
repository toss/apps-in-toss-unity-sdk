/** @tossdocs-ignore */
function parseHexValueStr(str) {
  return parseInt(str, 16);
}

function isValidHexValue(hex) {
  var regex = /^#?[0-9A-Fa-f]{6}$/;
  return regex.test(hex);
}
function isRGBDecimalValue(rgbDecimal) {
  return 0 <= rgbDecimal && rgbDecimal <= 255;
}
function isAlphaValue(alpha) {
  return 0 <= alpha && alpha <= 1;
}
function hexToRgba(hex) {
  var alpha = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : 1;

  if (!isValidHexValue(hex)) {
    throw new Error("Invalid hex value: ".concat(hex));
  }

  if (!isAlphaValue(alpha)) {
    throw new Error("Invalid alpha value(0~1): ".concat(alpha));
  }

  var normalizedHex = hex.startsWith('#') ? hex.slice(1) : hex;
  var r = parseHexValueStr(normalizedHex.slice(0, 2));
  var g = parseHexValueStr(normalizedHex.slice(2, 4));
  var b = parseHexValueStr(normalizedHex.slice(4, 6));
  return "rgba(".concat(r, ",").concat(g, ",").concat(b, ",").concat(alpha, ")");
}

export { hexToRgba, isAlphaValue, isRGBDecimalValue, isValidHexValue };
