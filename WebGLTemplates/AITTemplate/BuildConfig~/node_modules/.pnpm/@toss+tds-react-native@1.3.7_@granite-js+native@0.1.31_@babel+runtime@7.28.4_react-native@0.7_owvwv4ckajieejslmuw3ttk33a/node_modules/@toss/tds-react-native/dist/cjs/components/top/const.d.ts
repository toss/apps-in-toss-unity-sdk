import type { A11yCondition } from '../../core';
import type { FontWeightKeys, TypographyKeys } from '../txt';
import type { SubTitleSize, TitleSize } from './types';
export declare const titleTypographyMap: Record<TitleSize, TypographyKeys>;
export declare const subTitleTypographyMap: Record<SubTitleSize, TypographyKeys>;
export declare const subTitleFontWeightMap: Record<SubTitleSize, FontWeightKeys>;
export declare const a11yCondition: Record<'subTitleParagraph' | 'subTitleSelector' | 'subTitleTextButton' | 'titleParagraph' | 'titleSelector' | 'titleTextButton' | 'rightButton', A11yCondition>;
