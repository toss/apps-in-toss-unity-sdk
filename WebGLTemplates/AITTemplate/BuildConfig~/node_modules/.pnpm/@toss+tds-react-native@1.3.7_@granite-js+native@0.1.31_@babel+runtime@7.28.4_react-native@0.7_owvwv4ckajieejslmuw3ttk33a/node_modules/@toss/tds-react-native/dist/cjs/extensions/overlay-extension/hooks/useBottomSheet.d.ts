import { BottomSheet } from '../../../components/bottom-sheet';
import type { ComponentProps } from 'react';
type BottomSheetPrivateOptions = Omit<ComponentProps<typeof BottomSheet.Root>, 'open'>;
type BottomSheetPublicOptions = Partial<BottomSheetPrivateOptions>;
interface UseBottomSheetOptions {
    /** @default true */
    closeOnDestroy?: boolean;
}
/**
 * @public
 * @category Hooks
 * @kind function
 * @name useBottomSheet
 * @description
 * `useBottomSheet`은 TDS의 `BottomSheet` 컴포넌트를 간편하게 열고 닫을 수 있게 해주는 Hook이에요.
 *
 * @param {UseBottomSheetOptions} [options] - `BottomSheet`을 제어하기 위한 옵션 객체에요.
 * @param {boolean} [options.closeOnDestroy=true] - 컴포넌트가 사라질 때 `BottomSheet`를 자동으로 닫을지 여부를 결정해요.
 *
 * @returns {{
 *   close: () => void;
 *   open: (options: BottomSheetPublicOptions) => void;
 * }}
 * - `close`함수는 현재 열려있는 `BottomSheet`를 닫아요.
 * - `open`함수는 기본 `BottomSheet`를 열어요.
 *
 * @example
 * ## `open`으로 `BottomSheet`를 여는 예제
 * ```tsx
 * import { useBottomSheet } from '../../../overlay-extension';
 *
 * const bottomSheet = useBottomSheet();
 *
 * const showBasicBottomSheet = () => {
 *    bottomSheet.open({
 *     header: <BottomSheet.Header>제목</BottomSheet.Header>,
 *     children: <Text>내용</Text>,
 *   });
 * };
 * ```
 */
export declare function useBottomSheet(options?: UseBottomSheetOptions): {
    close: () => void;
    open: ({ header, cta, onExited, onClose, ...options }: BottomSheetPublicOptions) => void;
};
export {};
