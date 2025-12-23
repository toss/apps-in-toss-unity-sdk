/** @tossdocs-ignore */
function isServer() {
  return typeof window === 'undefined' || 'Deno' in globalThis;
}

export { isServer };
