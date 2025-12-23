/**
 * @public
 * @category 친구초대
 * @name RewardFromContactsViralEvent
 * @description 친구에게 공유하기를 완료했을 때 지급할 리워드 정보를 담는 타입이에요. 이 타입을 사용하면 공유가 완료됐을 때 지급할 리워드 정보를 확인할 수 있어요.
 * @property {'sendViral'} type - 이벤트의 타입이에요. `'sendViral'`은 사용자가 친구에게 공유를 완료했을 때 돌아와요.
 * @property {Object} data - 지급할 리워드 관련 정보를 담고 있어요.
 * @property {number} data.rewardAmount - 지급할 리워드 수량이에요. 앱인토스 콘솔에서 설정한 수량 및 금액 값이에요.
 * @property {string} data.rewardUnit - 리워드의 단위예요. 앱인토스 콘솔에 설정된 리워드 이름인 '하트', '보석' 등이 리워드 단위예요.
 */
export type RewardFromContactsViralEvent = {
	type: "sendViral";
	data: {
		rewardAmount: number;
		rewardUnit: string;
	};
};
/**
 * @public
 * @category 친구초대
 * @name ContactsViralSuccessEvent
 * @description 연락처 공유 모듈이 정상적으로 종료됐을 때 전달되는 이벤트 객체예요. 종료 이유와 함께 리워드 상태 및 남은 친구 수 등 관련 정보를 제공해요.
 * @property {'close'} type - 이벤트의 타입이에요. `'close'`는 공유 모듈이 종료됐을 때 돌아와요.
 * @property {Object} data - 모듈 종료와 관련된 세부 정보를 담고 있어요.
 * @property {'clickBackButton' | 'noReward'} data.closeReason - 모듈이 종료된 이유예요. `'clickBackButton'`은 사용자가 뒤로 가기 버튼을 눌러 종료한 경우이고, `'noReward'`는 받을 수 있는 리워드가 없어서 종료된 경우예요.
 * @property {number} data.sentRewardAmount - 사용자가 받은 전체 리워드 수량이에요.
 * @property {number} data.sendableRewardsCount - 아직 공유할 수 있는 친구 수예요.
 * @property {number} data.sentRewardsCount - 사용자가 공유를 완료한 친구 수예요.
 * @property {string} data.rewardUnit - 리워드의 단위예요. 앱인토스 콘솔에 설정된 리워드 이름인 '하트', '보석' 등이 리워드 단위예요.
 */
export type ContactsViralSuccessEvent = {
	type: "close";
	data: {
		closeReason: "clickBackButton" | "noReward";
		sentRewardAmount?: number;
		sendableRewardsCount?: number;
		sentRewardsCount: number;
		rewardUnit?: string;
	};
};
export type ContactsViralEvent = RewardFromContactsViralEvent | ContactsViralSuccessEvent;
/**
 * @public
 * @category 친구초대
 * @name ContactsViralOption
 * @description [연락처 공유 기능](/react-native/reference/native-modules/친구초대/contactsViral.html)을 사용할 때 필요한 옵션이에요.
 * @property {string} moduleId - 공유 리워드를 구분하는 UUID 형식의 고유 ID예요. 앱인토스 콘솔의 미니앱 > 공유 리워드 메뉴에서 확인할 수 있어요.
 */
export type ContactsViralOption = {
	moduleId: string;
};
/**
 * @public
 * @category 친구초대
 * @name ContactsViralParams
 * @description `ContactsViralParams`는 연락처 공유 기능을 사용할 때 전달해야 하는 파라미터 타입이에요. 옵션을 설정하고, 이벤트 및 에러 처리 콜백을 지정할 수 있어요.
 * @property {ContactsViralOption} options - 공유 기능에 사용할 옵션 객체예요.
 * @property {(event: ContactsViralEvent) => void} onEvent - 공유 이벤트가 발생했을 때 실행되는 함수예요. [`RewardFromContactsViralEvent`](/bedrock/reference/native-modules/친구초대/RewardFromContactsViralEvent.html) 또는 [`ContactsViralSuccessEvent`](/react-native/reference/native-modules/친구초대/ContactsViralSuccessEvent.html) 타입의 이벤트 객체가 전달돼요.
 * @property {(error: unknown) => void} onError - 예기치 않은 에러가 발생했을 때 실행되는 함수예요.
 */
export interface ContactsViralParams {
	options: ContactsViralOption;
	onEvent: (event: ContactsViralEvent) => void;
	onError: (error: unknown) => void;
}
/**
 * @public
 * @category 친구초대
 * @name contactsViral
 * @description 친구에게 공유하고 리워드를 받을 수 있는 기능을 제공해요. 사용자가 친구에게 공유를 완료하면 앱브릿지가 이벤트를 통해 리워드 정보를 전달해요.
 * @param {ContactsViralParams} params - 연락처 공유 기능을 실행할 때 사용하는 파라미터예요. 옵션 설정과 이벤트 핸들러를 포함해요. 자세한 내용은 [ContactsViralParams](/bedrock/reference/native-modules/친구초대/ContactsViralParams.html) 문서를 참고하세요.
 * @returns {() => void} 앱브릿지 cleanup 함수를 반환해요. 공유 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.
 *
 * @example
 * ### 친구에게 공유하고 리워드 받기
 *
 * ```tsx
 * import { useCallback } from 'react';
 * 
 * import { contactsViral } from '@apps-in-toss/web-framework';
 *
 * function ContactsViralButton({ moduleId }: { moduleId: string }) {
 *   const handleContactsViral = useCallback(() => {
 *     try {
 *       const cleanup = contactsViral({
 *         options: { moduleId: moduleId.trim() },
 *         onEvent: (event) => {
 *           if (event.type === 'sendViral') {
 *             console.log('리워드 지급:', event.data.rewardAmount, event.data.rewardUnit);
 *           } else if (event.type === 'close') {
 *             console.log('모듈 종료:', event.data.closeReason);
 *           }
 *         },
 *         onError: (error) => {
 *           console.error('에러 발생:', error);
 *         },
 *       });
 *
 *       return cleanup;
 *     } catch (error) {
 *       console.error('실행 중 에러:', error);
 *     }
 *   }, [moduleId]);
 *
 *   return <input type="button" value="친구에게 공유하고 리워드 받기" onClick={handleContactsViral} />;
 * }
 * ```
 */
export declare function contactsViral(params: ContactsViralParams): () => void;

export {};
