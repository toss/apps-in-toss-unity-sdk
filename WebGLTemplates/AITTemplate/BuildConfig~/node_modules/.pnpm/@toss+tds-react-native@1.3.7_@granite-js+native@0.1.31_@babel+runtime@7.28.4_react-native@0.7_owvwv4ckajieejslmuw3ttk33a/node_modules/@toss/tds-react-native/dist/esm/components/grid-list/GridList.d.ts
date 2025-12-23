import { GridListItem } from './GridListItem';
import type { GridListProps } from './types';
/**
 *
 * @example
 * <GridList column={3}>
 *   <GridList.Item
 *     image={<Icon name="icn-bank-toss" />}
 *     title="토스"
 *     onPress={() => null}
 *   />
 * </GridList>
 */
declare function GridList({ children, column, style }: GridListProps): import("react/jsx-runtime").JSX.Element;
declare namespace GridList {
    var Item: typeof GridListItem;
}
export default GridList;
