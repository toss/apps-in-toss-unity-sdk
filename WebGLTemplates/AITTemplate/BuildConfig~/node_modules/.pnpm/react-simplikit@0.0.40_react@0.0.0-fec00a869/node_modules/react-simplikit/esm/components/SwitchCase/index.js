// src/components/SwitchCase/SwitchCase.tsx
function SwitchCase({ value, caseBy, defaultComponent = () => null }) {
  const stringifiedValue = String(value);
  return (caseBy[stringifiedValue] ?? defaultComponent)();
}
export {
  SwitchCase
};
