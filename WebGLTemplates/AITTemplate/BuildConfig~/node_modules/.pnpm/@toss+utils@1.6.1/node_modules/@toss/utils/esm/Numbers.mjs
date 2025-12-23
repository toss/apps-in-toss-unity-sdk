/** @tossdocs-ignore */
var units = ['', '십', '백', '천', '만', '십', '백', '천', '억', '십', '백', '천', '조', '십', '백', '천', '경'];

function chunk(value, byDigits) {
  var result = [];
  var source = String(value);

  for (var end = source.length; end >= 1; end = end - byDigits) {
    var start = Math.max(end - byDigits, 0);
    var slice = source.slice(start, end);
    result.push(Number(slice));
  }

  return result;
}

function createNumberFormatterBy(formatter) {
  return function formatNumber(value, unit) {
    if (unit < 1) {
      // 부동소수점 오류 때문에 unit < 1인 경우 특별 처리
      var reciprocal = 1 / unit;
      return formatter(value * reciprocal) / reciprocal;
    }

    return formatter(value / unit) * unit;
  };
}

var ceilToUnit = createNumberFormatterBy(Math.ceil);
var floorToUnit = createNumberFormatterBy(Math.floor);
var roundToUnit = createNumberFormatterBy(Math.round);
function formatToKoreanNumber(value) {
  var options = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {};
  var unit = options.floorUnit !== undefined ? floorToUnit(value, options.floorUnit || 1) : ceilToUnit(value, options.ceilUnit || 1);

  if (unit === 0) {
    return '0';
  }

  return chunk(unit, 4).reduce(function (prevFormatted, currChunkNum, index) {
    if (currChunkNum === 0) {
      return prevFormatted;
    }

    var val = options.formatAllDigits ? formatThousands(currChunkNum) : commaizeNumber(currChunkNum);
    var unit = units[index * 4];
    return "".concat(val).concat(unit, " ").concat(prevFormatted);
  }, '').trim();
}
function formatToKRW(value) {
  var options = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : {};
  var formattedVal = formatToKoreanNumber(value, options);

  if (options.shouldHaveSpaceBeforeWon === true) {
    return "".concat(formattedVal, " \uC6D0");
  }

  return "".concat(formattedVal, "\uC6D0");
}
function commaizeNumber(value) {
  var numStr = String(value);
  var decimalPointIndex = numStr.indexOf('.');
  var commaizeRegExp = /(\d)(?=(\d\d\d)+(?!\d))/g;
  return decimalPointIndex > -1 ? numStr.slice(0, decimalPointIndex).replace(commaizeRegExp, '$1,') + numStr.slice(decimalPointIndex) : numStr.replace(commaizeRegExp, '$1,');
}
function floorAndFormatNumber(value) {
  return commaizeNumber(Math.floor(value));
}
function decommaizeNumber(numStr) {
  return Number(numStr.replace(/,/g, ''));
}
function formatPhoneNumber(phoneNumber) {
  // 서울 국번(02)인 경우에만 지역번호가 2자리입니다.
  var isSeoulNumber = phoneNumber.startsWith('02'); // 12자리 전화번호는 앞자리가 4개입니다.

  var is12Number = phoneNumber.length === 12;
  var areaCodeEndIndex = isSeoulNumber ? 2 : is12Number ? 4 : 3; // 9 ~ 12자리 전화번호에 대응하기 위해서
  // [0:areaCodeEndIndex], [areaCodeEndIndex:length-4], [length-4:length] 형식으로 나누고 join합니다.

  return [phoneNumber.slice(0, areaCodeEndIndex), phoneNumber.slice(areaCodeEndIndex, phoneNumber.length - 4), phoneNumber.slice(phoneNumber.length - 4)].join('-');
}

function formatThousands(num) {
  var numString = String(num).split('').reverse().map(function (digit, index) {
    return digit !== '0' ? "".concat(digit !== '1' ? digit : '').concat(units[index]) : '';
  }).reverse().join('');
  return numString;
}

function formatBusinessRegistrationNumber(businessRegistrationNumber) {
  if (businessRegistrationNumber.length !== 10) {
    throw new Error('사업자등록번호는 반드시 길이가 10 이어야 합니다.');
  }

  if (/^\d+$/.test(businessRegistrationNumber) === false) {
    throw new Error('사업자등록번호는 [0-9] 이어야 합니다.');
  }

  return businessRegistrationNumber.replace(/(\d{3})(\d{2})(\d{5})/, '$1-$2-$3');
}

export { ceilToUnit, commaizeNumber, decommaizeNumber, floorAndFormatNumber, floorToUnit, formatBusinessRegistrationNumber, formatPhoneNumber, formatToKRW, formatToKoreanNumber, roundToUnit };
