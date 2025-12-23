import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';
import { TableRowLeftText, TableRowRightText } from './TableRowText';
import type { TableRowAlign } from './types';
interface TableRowProps {
    align: TableRowAlign;
    /**
     * @description 0 - 100
     */
    leftRatio?: number;
    /**
     * @description "string" | <TableRow.LeftText .../>
     */
    left: ReactNode;
    /**
     * @description "string" | <TableRow.RightText .../>
     */
    right: ReactNode;
    horizontalPadding?: 0 | 24;
    style?: StyleProp<ViewStyle>;
}
/**
 *
 * @example
 *
 * // default
 * <TableRow align="left" left="받는분" right="김토스" leftRatio={40} />
 *
 * // custom text
 * <TableRow
 *   align="right"
 *   left={<TableRow.LeftText color={colors.grey900}>받는분</TableRow.LeftText>}
 *   right={
 *     <TableRow.RightText fontWeight="bold">
 *       김토스
 *     </TableRow.RightText>
 *   }
 *   leftRatio={40}
 * />
 */
declare function TableRow({ align, leftRatio, left, right, horizontalPadding, style }: TableRowProps): import("react/jsx-runtime").JSX.Element;
declare namespace TableRow {
    var LeftText: typeof TableRowLeftText;
    var RightText: typeof TableRowRightText;
}
export default TableRow;
