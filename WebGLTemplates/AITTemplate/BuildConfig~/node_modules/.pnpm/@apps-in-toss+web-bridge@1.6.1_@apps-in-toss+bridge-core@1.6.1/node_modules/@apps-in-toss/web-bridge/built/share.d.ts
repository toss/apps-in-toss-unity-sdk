/**
 * @public
 * @category 공유
 * @kind function
 * @name share
 * @description
 * 사용자가 콘텐츠를 다른 사람과 공유할 수 있도록 네이티브 공유 시트를 표시해요.
 * `options.message` 속성에 공유할 메시지를 전달하면, 사용자가 선택할 수 있는 앱 목록이 표시돼요.
 * 예를 들어, 사용자가 텍스트 메시지를 공유하거나 메모 앱에 저장하려고 할 때 유용해요.
 * @param {object} options - 공유할 메시지를 담은 객체예요.
 * @param {string} options.message - 공유할 텍스트 문자열이에요. 예를 들어, "안녕하세요! 이 내용을 공유합니다."
 *
 * @example
 * ### 공유하기 기능 구현하기
 *
 * 아래는 버튼을 클릭하면 메시지를 공유하는 간단한 예제예요.
 *
 * ```tsx
 * import { share } from '@apps-in-toss/native-modules';
 * 
 *
 * function MyPage() {
 *   return (
 *     <input type="button"
 *       value="공유"
 *       onClick={() => share({ message: '공유할 메시지입니다.' })}
 *     />
 *   );
 * }
 * ```
 */
export declare function share(message: {
	message: string;
}): Promise<void>;

export {};
