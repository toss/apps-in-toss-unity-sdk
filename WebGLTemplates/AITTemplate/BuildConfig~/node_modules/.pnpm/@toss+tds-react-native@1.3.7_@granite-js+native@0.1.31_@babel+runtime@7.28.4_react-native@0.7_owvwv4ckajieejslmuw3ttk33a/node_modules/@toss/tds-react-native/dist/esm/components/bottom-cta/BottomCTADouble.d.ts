import type { ReactElement } from 'react';
import type { CommonProps } from './types';
type DoubleProps = {
    leftButton: ReactElement;
    rightButton: ReactElement;
};
export type BottomCTADoubleProps = CommonProps & DoubleProps;
/**
 *
 * @example
 * <BottomCTA.Double
 *   leftButton={<Button>버튼</Button>}
 *   rightButton={<Button>버튼</Button>}
 * />
 */
declare function BottomCTADouble({ gradient: _gradient, topAccessory, bottomAccessory, containerStyle, keyboardStyleEnabled, ...props }: BottomCTADoubleProps): import("react/jsx-runtime").JSX.Element;
export default BottomCTADouble;
