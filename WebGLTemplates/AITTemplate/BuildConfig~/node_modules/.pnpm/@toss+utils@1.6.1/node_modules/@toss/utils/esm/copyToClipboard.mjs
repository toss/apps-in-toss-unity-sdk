import { isIOS } from './device/isIOS.mjs';

/**
 * @name copyToClipboard
 * @deprecated clipboard.writeText를 사용하세요.
 **/

function copyToClipboard(text) {
  if (!clipboardCopySupported()) {
    return false;
  }

  copy(text);
  return true;
}

function clipboardCopySupported() {
  var _document$queryComman, _document$queryComman2, _document;

  return (_document$queryComman = (_document$queryComman2 = (_document = document).queryCommandSupported) === null || _document$queryComman2 === void 0 ? void 0 : _document$queryComman2.call(_document, 'copy')) !== null && _document$queryComman !== void 0 ? _document$queryComman : false;
}

function copy(text) {
  var focusingContainer = document.body;
  var textArea = document.createElement('textArea');
  textArea.value = text;
  textArea.contentEditable = 'true';
  textArea.readOnly = false;
  textArea.style.userSelect = 'text';
  textArea.style.webkitUserSelect = 'text';
  focusingContainer.insertBefore(textArea, focusingContainer.firstChild);

  if (isIOS()) {
    var range = document.createRange();
    range.selectNodeContents(textArea);
    var selection = window.getSelection();

    if (selection !== null) {
      selection.removeAllRanges();
      selection.addRange(range);
    }

    textArea.setSelectionRange(0, 999999);
  } else {
    textArea.select();
  }

  document.execCommand('copy');
  focusingContainer.removeChild(textArea);
}

export { copyToClipboard };
