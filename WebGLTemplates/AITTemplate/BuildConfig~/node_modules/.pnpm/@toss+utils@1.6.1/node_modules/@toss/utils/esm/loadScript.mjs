import { isServer } from './device/isServer.mjs';

/** @tossdocs-ignore */

function loadScript(source) {
  if (isServer()) {
    return Promise.resolve();
  }

  var element = document.querySelector("script[src=\"".concat(source, "\"]")); // 이미 로드되어 있거나 로드 중

  if (element) {
    return Promise.resolve();
  }

  return new Promise(function (resolve) {
    var script = document.createElement('script');
    script.async = true;
    script.defer = true;
    script.src = source;
    document.body.append(script);
    script.addEventListener('load', resolve);
  });
}

export { loadScript as default, loadScript };
