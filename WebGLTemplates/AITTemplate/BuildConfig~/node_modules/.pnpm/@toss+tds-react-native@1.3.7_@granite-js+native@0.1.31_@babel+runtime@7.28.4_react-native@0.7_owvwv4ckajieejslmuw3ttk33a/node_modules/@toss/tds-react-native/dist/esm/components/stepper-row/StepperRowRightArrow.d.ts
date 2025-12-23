import type { FrameShapeType } from '../asset/types';
export interface StepperRowRightArrowProps {
    /**
     * 아이콘의 이름을 지정해요.
     * @default 'icon-arrow-right-mono'
     */
    name?: string;
    /**
     * 아이콘의 색상을 지정해요.
     * @default adaptive.grey400
     */
    color?: string;
    /**
     * 프레임의 모양을 설정해요.
     * 더 큰 텍스트 스케일이 160보다 크거나 같으면, `Asset.frameShape.CleanW32` 로 자동 변경돼요.
     * @default Asset.frameShape.CleanW24
     */
    frameShape?: FrameShapeType;
}
export declare function StepperRowRightArrow({ name, color, frameShape }: StepperRowRightArrowProps): import("react/jsx-runtime").JSX.Element;
