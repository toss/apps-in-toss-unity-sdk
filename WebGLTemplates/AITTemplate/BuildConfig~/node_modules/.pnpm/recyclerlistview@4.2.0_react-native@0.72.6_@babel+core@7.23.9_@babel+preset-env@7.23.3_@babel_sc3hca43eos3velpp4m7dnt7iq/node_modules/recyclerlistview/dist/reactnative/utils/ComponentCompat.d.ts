import * as React from "react";
export declare abstract class ComponentCompat<T1 = {}, T2 = {}, SS = any> extends React.Component<T1, T2, SS> {
    private _hasRenderedOnce;
    private _didPropsChange;
    constructor(props: T1, context?: any);
    shouldComponentUpdate(newProps: T1, newState: T2): boolean;
    componentWillReceivePropsCompat(newProps: T1): void;
    componentWillMountCompat(): void;
    componentWillUpdateCompat(): void;
    render(): React.ReactNode;
    abstract renderCompat(): React.ReactNode;
}
