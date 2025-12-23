import * as react_jsx_runtime from 'react/jsx-runtime';
import { ReactNode } from 'react';

type ProviderProps<ContextValuesType> = (ContextValuesType & {
    children: ReactNode;
}) | {
    children: ReactNode;
};
/**
 * @description
 * `buildContext` is a helper function that reduces repetitive code when defining React Context.
 *
 * @param {string} contextName - The name of the context.
 * @param {ContextValuesType} [defaultContextValues] - The default values to be passed to the context.
 *
 * @returns {[Provider: (props: ProviderProps<ContextValuesType>) => JSX.Element, useContext: () => ContextValuesType]} A tuple of the form :
 * - Provider `(props: ProviderProps<ContextValuesType>) => JSX.Element` - The component that provides the context;
 * - useContext `() => ContextValuesType` - The hook that uses the context;
 *
 * @example
 * const [Provider, useContext] = buildContext<{ title: string }>('TestContext', null);
 *
 * function Inner() {
 *   const { title } = useContext();
 *   return <div>{title}</div>;
 * }
 *
 * function Page() {
 *   return (
 *     <Provider title="Hello">
 *       <Inner />
 *     </Provider>
 *   );
 * }
 */
declare function buildContext<ContextValuesType extends object>(contextName: string, defaultContextValues?: ContextValuesType): readonly [({ children, ...contextValues }: ProviderProps<ContextValuesType>) => react_jsx_runtime.JSX.Element, () => ContextValuesType];

export { buildContext };
