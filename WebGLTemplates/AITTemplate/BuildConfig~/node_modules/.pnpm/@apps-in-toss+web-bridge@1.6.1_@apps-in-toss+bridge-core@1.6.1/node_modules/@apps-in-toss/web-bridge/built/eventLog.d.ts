export type Primitive = string | number | boolean | null | undefined | symbol;
export interface EventLogParams {
	log_name: string;
	log_type: "debug" | "info" | "warn" | "error" | "event" | "screen" | "impression" | "click";
	params: Record<string, Primitive>;
}
/**
 * @category 로깅
 * @kind function
 * @name eventLog
 * @description
 * 이벤트 로그를 기록하는 함수예요.
 *
 * 이 함수는 앱 내에서 발생하는 다양한 이벤트를 로깅하는 데 사용돼요. 디버깅, 정보 제공, 경고, 오류 등 다양한 유형의 로그를 기록할 수 있어요. 샌드박스 환경에서는 콘솔에 로그가 출력되고, 실제 환경에서는 로그 시스템에 기록돼요.
 *
 * @param {Object} params 로그 기록에 필요한 매개변수 객체예요.
 * @param {string} params.log_name 로그의 이름이에요.
 * @param {'debug' | 'info' | 'warn' | 'error' | 'event' | 'screen' | 'impression' | 'click'} params.log_type 로그의 유형이에요.
 * @param {Record<string, Primitive>} params.params 로그에 포함할 추가 매개변수 객체예요.
 *
 * @returns {Promise<void>} 로그 기록이 완료되면 해결되는 Promise예요.
 *
 * @example
 * ### 이벤트 로그 기록하기
 *
 * ```tsx
 * import { eventLog } from '@apps-in-toss/web-framework';
 *
 * function logUserAction() {
 *   eventLog({
 *     log_name: 'user_action',
 *     log_type: 'info',
 *     params: {
 *       action: 'button_click',
 *       screen: 'main',
 *       userId: 12345
 *     }
 *   });
 * }
 * ```
 */
export declare function eventLog(params: EventLogParams): Promise<void>;

export {};
