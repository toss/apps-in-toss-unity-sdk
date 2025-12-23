import type { ComponentProps, ReactNode } from 'react';
import { Text } from '../paragraph';
interface StepperRowTextsProps {
    /**
     * `StepperRow.Texts` 컴포넌트의 텍스트의 타입을 설정해요. 타이틀과 설명의 스타일이 타입에 따라 달라져요.
     * - `A`일 떄 타이틀은 `t5`, 설명은 `t6`에요.
     * - `B`일 떄 타이틀은 `t4`, 설명은 `t6`에요.
     * - `C`일 떄 타이틀은 `t5`, 설명은 `t7`에요.
     */
    type: 'A' | 'B' | 'C';
    /**
     * `StepperRow.Texts` 컴포넌트에 표시될 타이틀을 지정해요.
     */
    title: ReactNode;
    /**
     * `StepperRow.Texts` 컴포넌트에 표시될 설명을 지정해요.
     */
    description?: ReactNode;
    /**
     * 타이틀의 스타일링을 할 때 사용해요.
     */
    titleProps?: ParagraphTextProps;
    /**
     * 설명의 스타일링을 할 때 사용해요.
     */
    descriptionProps?: ParagraphTextProps;
}
type ParagraphTextProps = ComponentProps<typeof Text>;
export declare function StepperRowTexts({ type, title, description, titleProps, descriptionProps }: StepperRowTextsProps): import("react/jsx-runtime").JSX.Element;
export {};
