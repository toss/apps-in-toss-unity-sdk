/** @tossdocs-ignore */
function maskAll(str) {
  return str.replace(/./g, '*');
}

function isKoreanName(name) {
  return /[가-힣]{2,}/.test(name);
}

function maskName(name) {
  if (isKoreanName(name)) {
    switch (name.length) {
      case 2:
        return name.replace(/([가-힣])([가-힣]+)/, '$1*');

      default:
        return maskExceptForEdge(name, 1);
    }
  } else {
    if (name.length < 3) {
      return name;
    }

    var unmaskedSideSize = name.length < 6 ? 1 : 2;
    return maskExceptForEdge(name, unmaskedSideSize);
  }
}

function maskExceptForEdge(text, edgeSize) {
  return text.slice(0, edgeSize) + text.slice(edgeSize, text.length - edgeSize).replace(/[a-zA-Z가-힣]/g, '*') + text.slice(text.length - edgeSize, text.length);
}

function isHyphenSeparated(phoneNumber) {
  return /^\d{2,3}-\d{3,4}-\d{4}$/.test(phoneNumber);
}

function isSeoulPhoneNumber(phoneNumber) {
  return /^02\d+$/.test(phoneNumber);
}

function maskPhoneNumber(phoneNumber) {
  if (isHyphenSeparated(phoneNumber)) {
    return phoneNumber.replace(/^(\d{2,3})-(\d{3,4})-(\d{4})$/, function (_, p1, p2, p3) {
      return "".concat(p1, "-").concat(maskAll(p2), "-").concat(p3);
    });
  }

  if (isSeoulPhoneNumber(phoneNumber)) {
    return phoneNumber.replace(/^02(\d{3,4})(\d{4})/, function (_, p1, p2) {
      return "02".concat(maskAll(p1)).concat(p2);
    });
  }

  return phoneNumber.replace(/^(\d{3})(\d{3,4})(\d{4})/, function (_, p1, p2, p3) {
    return "".concat(p1).concat(maskAll(p2)).concat(p3);
  });
}

var Masker = {
  maskName: maskName,
  maskPhoneNumber: maskPhoneNumber
};

export { Masker };
