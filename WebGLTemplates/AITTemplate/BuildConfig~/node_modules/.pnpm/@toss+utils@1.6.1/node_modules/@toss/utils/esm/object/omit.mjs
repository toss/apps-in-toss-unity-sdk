import { objectKeys } from './object-keys.mjs';

/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package.
 */

function omit(obj, keys) {
  return objectKeys(obj).filter(function (k) {
    return !keys.includes(k);
  }).reduce(function (acc, key) {
    return acc[key] = obj[key], acc;
  }, {});
}

export { omit };
