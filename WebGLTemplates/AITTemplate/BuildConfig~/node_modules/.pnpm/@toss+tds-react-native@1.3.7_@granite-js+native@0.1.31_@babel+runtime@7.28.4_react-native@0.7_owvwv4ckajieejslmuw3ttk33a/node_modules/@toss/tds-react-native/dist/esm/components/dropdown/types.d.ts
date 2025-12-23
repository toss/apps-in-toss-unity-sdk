import type { ReactElement, RefCallback, RefObject } from 'react';
export type ReactElementWithRef = ReactElement & {
    ref?: RefObject<any> | RefCallback<any>;
};
export type Placement = 'bottom-start' | 'bottom-end';
