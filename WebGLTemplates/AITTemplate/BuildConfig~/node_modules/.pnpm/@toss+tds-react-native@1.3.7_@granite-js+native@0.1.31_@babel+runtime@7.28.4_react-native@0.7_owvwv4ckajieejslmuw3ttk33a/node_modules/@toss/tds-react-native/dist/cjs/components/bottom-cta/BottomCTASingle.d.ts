import type { ComponentProps } from 'react';
import { Button } from '../button';
import type { CommonProps } from './types';
type ButtonProps = ComponentProps<typeof Button>;
export type BottomCTASingleProps = CommonProps & ButtonProps;
/**
 *
 * @example
 * <BottomCTA.Single
 *    type="dark"
 *    style="weak"
 *    onPress={() => {
 *      // ...
 *    }}
 *  확인
 * </BottomCTA.Single>
 */
declare function BottomCTASingle({ gradient: _gradient, topAccessory, bottomAccessory, containerStyle, keyboardStyleEnabled, display: displayFromProps, ...props }: BottomCTASingleProps): import("react/jsx-runtime").JSX.Element;
export default BottomCTASingle;
