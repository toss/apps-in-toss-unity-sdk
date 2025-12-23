import { Dispatch, SetStateAction } from 'react';

type ControlledState<T> = {
    value: T;
    defaultValue?: never;
} | {
    defaultValue: T;
    value?: T;
};
type UseControlledStateProps<T> = ControlledState<T> & {
    onChange?: (value: T) => void;
    equalityFn?: (prev: T, next: T) => boolean;
};
/**
 * @description
 * `useControlledState` is a React hook that allows you to control both controlled and uncontrolled states.
 * If you pass the state to `value`, it will be a controlled state, and if you pass the state to `defaultValue`, it will be an uncontrolled state.
 * If both `value` and `defaultValue` are passed, `value` will take precedence.
 *
 * @param {Object} props
 * @param {T} [props.value] - The value of the state.
 * @param {T} [props.defaultValue] - The default value of the state.
 * @param {(value: T) => void} [props.onChange] - The callback function that is called when the state changes.
 * @param {(prev: T, next: T) => boolean} [props.equalityFn] - The function that is used to compare the previous and next values.
 *
 * @returns {[T, Dispatch<SetStateAction<T>>]} - The state and the setter function.
 *
 * @example
 * type ToggleProps = {
 *   value?: boolean;
 *   defaultValue?: boolean;
 *   onChange?: (value: boolean) => void;
 * }
 *
 * function Toggle({ value, defaultValue, onChange }: ToggleProps) {
 *  const [on, setOn] = useControlledState({
 *    value,
 *    defaultValue: defaultValue ?? false,
 *    onChange,
 *  });
 *
 *  return (
 *    <button onClick={() => setOn((prev) => !prev)}>
 *      {on ? 'ON' : 'OFF'}
 *    </button>
 *  )
 * }
 */
declare function useControlledState<T>({ value: valueProp, defaultValue, onChange, equalityFn, }: UseControlledStateProps<T>): [T, Dispatch<SetStateAction<T>>];

export { useControlledState };
