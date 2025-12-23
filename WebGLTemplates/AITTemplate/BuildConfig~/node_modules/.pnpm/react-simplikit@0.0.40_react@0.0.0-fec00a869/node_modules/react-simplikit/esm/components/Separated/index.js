// src/components/Separated/Separated.tsx
import { Children, Fragment, isValidElement } from "react";
import { Fragment as Fragment2, jsx, jsxs } from "react/jsx-runtime";
function Separated({ children, by: separator }) {
  const childrenArray = Children.toArray(children).filter(isValidElement);
  return /* @__PURE__ */ jsx(Fragment2, { children: childrenArray.map((child, i, { length }) => /* @__PURE__ */ jsxs(Fragment, { children: [
    child,
    i + 1 !== length && separator
  ] }, i)) });
}
export {
  Separated
};
