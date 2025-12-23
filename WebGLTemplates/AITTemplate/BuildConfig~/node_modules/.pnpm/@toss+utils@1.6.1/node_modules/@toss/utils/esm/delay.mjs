/** @tossdocs-ignore */

/**
 * @deprecated This feature is now available in the es-toolkit package.
 */
function delay(milliseconds) {
  return new Promise(function (resolve) {
    setTimeout(resolve, milliseconds);
  });
}

export { delay };
