// src/utils/mergeProps/mergeProps.ts
function mergeProps(...props) {
  return props.reduce(pushProp, {});
}
function pushProp(prev, curr) {
  for (const key in curr) {
    if (curr[key] === void 0) continue;
    switch (key) {
      case "className": {
        prev[key] = [prev[key], curr[key]].join(" ").trim();
        break;
      }
      case "style": {
        prev[key] = mergeStyle(prev[key], curr[key]);
        break;
      }
      default: {
        const mergedFunction = mergeFunction(prev[key], curr[key]);
        if (mergedFunction) {
          prev[key] = mergedFunction;
        } else if (curr[key] !== void 0) {
          prev[key] = curr[key];
        }
      }
    }
  }
  return prev;
}
function mergeStyle(a, b) {
  if (a == null) return b;
  return { ...a, ...b };
}
function mergeFunction(a, b) {
  if (typeof a === "function" && typeof b === "function") {
    return (...args) => {
      a(...args);
      b(...args);
    };
  }
}
export {
  mergeProps
};
