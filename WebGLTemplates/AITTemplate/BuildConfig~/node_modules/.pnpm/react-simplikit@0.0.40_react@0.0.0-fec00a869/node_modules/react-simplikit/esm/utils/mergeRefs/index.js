// src/utils/mergeRefs/mergeRefs.ts
function mergeRefs(...refs) {
  return (value) => {
    for (const ref of refs) {
      if (ref == null) {
        continue;
      }
      if (typeof ref === "function") {
        ref(value);
        continue;
      }
      ref.current = value;
    }
  };
}
export {
  mergeRefs
};
