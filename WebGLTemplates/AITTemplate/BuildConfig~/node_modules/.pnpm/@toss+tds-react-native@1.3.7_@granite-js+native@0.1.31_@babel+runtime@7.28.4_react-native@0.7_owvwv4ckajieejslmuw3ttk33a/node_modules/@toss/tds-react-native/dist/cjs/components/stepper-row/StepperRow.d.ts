import type { ReactNode } from 'react';
import { StepperRowAssetFrame } from './StepperRowAssetFrame';
import { StepperRowNumberIcon } from './StepperRowNumberIcon';
import { StepperRowRightArrow } from './StepperRowRightArrow';
import { StepperRowRightButton } from './StepperRowRightButton';
import { StepperRowTexts } from './StepperRowTexts';
interface StepperRowProps {
    /**
     * `StepperRow` 컴포넌트의 왼쪽 영역에 표시될 컴포넌트를 지정해요. 주로 `StepperRow.NumberIcon`나 `StepperRow.AssetFrame` 컴포넌트가 사용돼요.
     */
    left: ReactNode;
    /**
     * `StepperRow` 컴포넌트의 중앙 영역에 표시될 타이틀과 설명을 지정해요. 주로 `StepperRow.Texts` 컴포넌트가 사용돼요.
     */
    center: ReactNode;
    /**
     * `StepperRow` 컴포넌트의 오륵쪽 영역에 표시될 컴포넌트를 지정해요. 주로 `StepperRow.RightArrow`나 `StepperRow.RightButton` 컴포넌트가 사용돼요.
     */
    right?: ReactNode;
    /**
     * `StepperRow` 컴포넌트의 연결선을 숨길지 여부를 설정해요. 주로 마지막 스텝에서 `true`를 사용하여 연결선을 제거해요.
     * @default false
     */
    hideLine?: boolean;
}
export declare function StepperRow(props: StepperRowProps): import("react/jsx-runtime").JSX.Element;
export declare namespace StepperRow {
    var Texts: typeof StepperRowTexts;
    var NumberIcon: typeof StepperRowNumberIcon;
    var AssetFrame: typeof StepperRowAssetFrame;
    var RightArrow: typeof StepperRowRightArrow;
    var RightButton: typeof StepperRowRightButton;
}
export {};
