import type { ComponentType, ReactElement, ReactNode } from 'react';
export declare function FixedBottomCTAProvider<WrapperProps = unknown>({ children, wrapper, wrapperProps, }: {
    children: ReactNode;
    wrapper?: ComponentType<WrapperProps>;
    wrapperProps?: WrapperProps;
}): import("react/jsx-runtime").JSX.Element;
export declare function FixedBottomCTAConsumer({ children }: {
    children: ReactElement;
}): null;
