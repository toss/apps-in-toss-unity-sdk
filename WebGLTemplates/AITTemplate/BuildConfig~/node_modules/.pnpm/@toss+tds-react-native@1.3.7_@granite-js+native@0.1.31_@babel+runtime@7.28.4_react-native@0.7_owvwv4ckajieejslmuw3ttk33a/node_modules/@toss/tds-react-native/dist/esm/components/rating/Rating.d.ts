import type { View } from 'react-native';
import { type EditableRatingProps } from './EditableRating';
import { type ReadOnlyRatingProps } from './ReadOnlyRating';
export type EditableRating = {
    readonly: false;
} & EditableRatingProps;
export type ReadOnlyRating = {
    readonly: true;
} & ReadOnlyRatingProps;
export type RatingProps = EditableRating | ReadOnlyRating;
export declare const Rating: import("react").ForwardRefExoticComponent<RatingProps & import("react").RefAttributes<View>>;
