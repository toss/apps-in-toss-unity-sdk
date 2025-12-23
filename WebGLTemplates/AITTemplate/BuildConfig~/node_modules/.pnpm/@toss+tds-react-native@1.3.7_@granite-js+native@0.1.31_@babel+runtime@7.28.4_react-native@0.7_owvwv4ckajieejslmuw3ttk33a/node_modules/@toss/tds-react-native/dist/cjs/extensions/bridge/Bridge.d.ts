import type { ViewProps } from 'react-native';
import type { IconProps } from './components';
interface BridgeBaseProps extends ViewProps {
    /**
     * `Bridge` 컴포넌트 중앙에 표시될 아이콘이에요.
     */
    icon: IconProps;
    /**
     * `Bridge` 컴포넌트의 열림 상태를 제어해요.
     */
    open?: boolean;
    /**
     * `Bridge` 컴포넌트의 색상 정책을 결정해요.
     * - `basic`: 하얀 배경을 갖고있어요. 보통 하얀 배경의 화면에서 사용돼요.
     * - `inverted`: 검은 배경을 갖고있어요. 보통 검은 배경이나 색이 짙은 배경의 화면에서 사용돼요.
     * @default 'basic'
     */
    colorMode?: 'basic' | 'inverted';
    onClose?: () => void;
    onExited?: () => void;
}
interface BridgeBasicProps extends BridgeBaseProps {
    variant?: 'basic';
    serviceName: string;
    amount?: never;
}
interface BridgePointProps extends BridgeBaseProps {
    variant: 'point';
    serviceName: string;
    amount: number;
}
export type BridgeProps = BridgeBasicProps | BridgePointProps;
export declare function Bridge({ style, icon, open, colorMode, onClose: _onClose, onExited: _onExited, variant, serviceName, amount, ...restProps }: BridgeProps): import("react/jsx-runtime").JSX.Element;
export {};
