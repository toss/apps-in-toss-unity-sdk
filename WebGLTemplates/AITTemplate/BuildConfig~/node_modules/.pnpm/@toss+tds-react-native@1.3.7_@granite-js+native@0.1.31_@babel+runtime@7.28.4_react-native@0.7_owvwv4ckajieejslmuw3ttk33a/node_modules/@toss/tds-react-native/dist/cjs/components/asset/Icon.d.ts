import type { ComponentProps } from 'react';
import { ContentIcon } from './blocks/ContentIcon';
import type { AssetCommonType } from './types';
type Props = AssetCommonType & ComponentProps<typeof ContentIcon>;
export declare function Icon({ frameShape, backgroundColor, style, overlap, acc, accPosition, ...iconProps }: Props): import("react/jsx-runtime").JSX.Element;
export {};
