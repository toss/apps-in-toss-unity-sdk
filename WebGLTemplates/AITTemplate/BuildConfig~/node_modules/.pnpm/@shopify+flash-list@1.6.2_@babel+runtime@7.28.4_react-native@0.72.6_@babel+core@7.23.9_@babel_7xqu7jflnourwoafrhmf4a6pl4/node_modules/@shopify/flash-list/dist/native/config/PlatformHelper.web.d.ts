import React from "react";
import { BaseItemAnimator } from "recyclerlistview";
declare const PlatformConfig: {
    defaultDrawDistance: number;
    invertedTransformStyle: {
        transform: {
            scaleY: number;
        }[];
    };
    invertedTransformStyleHorizontal: {
        transform: {
            scaleX: number;
        }[];
    };
};
declare const getCellContainerPlatformStyles: (inverted: boolean, parentProps: {
    x: number;
    y: number;
    isHorizontal?: boolean;
}) => {
    transform: string;
    WebkitTransform: string;
} | undefined;
declare const getItemAnimator: () => BaseItemAnimator | undefined;
declare const getFooterContainer: () => React.ComponentClass | undefined;
export { PlatformConfig, getCellContainerPlatformStyles, getItemAnimator, getFooterContainer, };
//# sourceMappingURL=PlatformHelper.web.d.ts.map