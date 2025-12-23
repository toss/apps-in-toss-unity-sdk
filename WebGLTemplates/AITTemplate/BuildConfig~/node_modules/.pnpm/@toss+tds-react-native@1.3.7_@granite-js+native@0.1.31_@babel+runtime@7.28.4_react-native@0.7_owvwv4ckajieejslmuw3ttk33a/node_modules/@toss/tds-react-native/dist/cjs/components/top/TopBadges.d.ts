import type { ParagraphBadgeProps } from '../paragraph';
type BadgeItem = {
    label: string;
    type: ParagraphBadgeProps['type'];
    style: ParagraphBadgeProps['badgeStyle'];
};
export interface BadgesProps {
    items: BadgeItem[];
}
declare function Badges({ items }: BadgesProps): import("react/jsx-runtime").JSX.Element;
declare const UpperBadges: typeof Badges;
declare const SubtitleBadges: typeof Badges;
export { UpperBadges, SubtitleBadges };
