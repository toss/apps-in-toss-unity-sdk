import { FetchContactsPermissionError, type FetchContacts } from '@apps-in-toss/types';
import { AppsInTossModule } from '../../AppsInTossModule';
import { createPermissionFunction } from '../createPermissionFunction';

/**
 * @public
 * @category 연락처
 * @name fetchContacts
 * @description 사용자의 연락처 목록을 페이지 단위로 가져오는 함수예요.
 * @param options - 연락처를 가져올 때 지정하는 옵션 객체예요.
 * @param options.size - 한 번에 가져올 연락처 개수예요. 예를 들어, 10을 전달하면 최대 10개의 연락처를 가져와요.
 * @param options.offset - 가져올 연락처의 시작 지점이에요. 처음 호출할 때는 `0`을 전달해야 해요. 이후에는 이전 호출에서 반환된 `nextOffset` 값을 사용해요.
 * @param options.query - 추가적인 필터링 옵션이에요.
 * @param options.query.contains - 이름에 특정 문자열이 포함된 연락처만 가져오고 싶을 때 사용해요. 이 값을 전달하지 않으면 모든 연락처를 가져와요.
 * @property [openPermissionDialog] - 연락처 읽기 권한을 다시 요청하는 다이얼로그를 표시해요. 사용자는 "허용", "한 번만 허용", "안하기" 중 하나를 선택할 수 있어요. "허용"이나 "한 번만 허용"을 선택하면 `allowed`를 반환하고, "안하기"를 선택하면 `denied`를 반환해요.
 * @property [getPermission] - 연락처 읽기 권한의 현재 상태를 반환해요. `allowed`는 사용자가 연락처 읽기 권한을 허용한 상태예요. `denied`는 사용자가 연락처 읽기 권한을 거부한 상태예요. `notDetermined`는 연락처 읽기 권한 요청을 한 번도 하지 않은 상태예요.
 *
 * @returns {Promise<ContactResult>}
 * 연락처 목록과 페이지네이션 정보를 포함한 객체를 반환해요.
 * - `result`: 가져온 연락처 목록이에요.
 * - `nextOffset`: 다음 호출에 사용할 오프셋 값이에요. 더 가져올 연락처가 없으면 `null`이에요.
 * - `done`: 모든 연락처를 다 가져왔는지 여부를 나타내요. 모두 가져왔다면 `true`예요.
 *
 * @signature
 * ```typescript
 * function fetchContacts(options: {
 *   size: number;
 *   offset: number;
 *   query?: {
 *     contains?: string;
 *   };
 * }): Promise<ContactResult>;
 * ```
 *
 * @example
 * ### 특정 문자열이 포함된 연락처 목록 가져오기
 *
 * 연락처 목록을 가져오는 예제예요.
 * "권한 확인하기"버튼을 눌러서 현재 연락처 읽기 권한을 확인해요.
 * 사용자가 권한을 거부했거나 시스템에서 권한이 제한된 경우에는 [`FetchContactsPermissionError`](/react-native/reference/types/권한/FetchContactsPermissionError)를 반환해요.
 * "권한 요청하기"버튼을 눌러서 연락처 읽기 권한을 요청할 수 있어요.
 *
 * ```tsx
 * import { ContactEntity, fetchContacts, FetchContactsPermissionError } from '@apps-in-toss/framework';
 * import { useState } from 'react';
 * import { Alert, Button, Text, View } from 'react-native';
 *
 * // 특정 문자열을 포함한 연락처 목록을 가져와 화면에 표시하는 컴포넌트
 * function ContactsList() {
 *   const [contacts, setContacts] = useState<{
 *     result: ContactEntity[];
 *     nextOffset: number | null;
 *     done: boolean;
 *   }>({
 *     result: [],
 *     nextOffset: null,
 *     done: false,
 *   });
 *
 *   const handlePress = async () => {
 *     try {
 *       if (contacts.done) {
 *         console.log('모든 연락처를 가져왔어요.');
 *         return;
 *       }
 *
 *       const response = await fetchContacts({
 *         size: 10,
 *         offset: contacts.nextOffset ?? 0,
 *         query: { contains: '김' },
 *       });
 *       setContacts((prev) => ({
 *         result: [...prev.result, ...response.result],
 *         nextOffset: response.nextOffset,
 *         done: response.done,
 *       }));
 *     } catch (error) {
 *       if (error instanceof FetchContactsPermissionError) {
 *         // 연락처 읽기 권한 없음
 *       }
 *       console.error('연락처를 가져오는 데 실패했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       {contacts.result.map((contact, index) => (
 *         <Text key={index}>
 *           {contact.name}: {contact.phoneNumber}
 *         </Text>
 *       ))}
 *       <Button
 *         title={contacts.done ? '모든 연락처를 가져왔어요.' : '다음 연락처 가져오기'}
 *         disabled={contacts.done}
 *         onPress={handlePress}
 *       />
 *       <Button
 *         title="권한 확인하기"
 *         onPress={async () => {
 *           const permission = await fetchContacts.getPermission();
 *           Alert.alert(permission);
 *         }}
 *       />
 *       <Button
 *         title="권한 요청하기"
 *         onPress={async () => {
 *           const permission = await fetchContacts.openPermissionDialog();
 *           Alert.alert(permission);
 *         }}
 *       />
 *     </View>
 *   );
 * }
 * ```
 */

export const fetchContacts = createPermissionFunction<FetchContacts>({
  handler: async (options) => {
    const contacts = await AppsInTossModule.fetchContacts(options);

    return {
      result: contacts.result,
      nextOffset: contacts.nextOffset ?? null,
      done: contacts.done,
    };
  },
  permission: {
    name: 'contacts',
    access: 'read',
  },
  error: FetchContactsPermissionError,
});
