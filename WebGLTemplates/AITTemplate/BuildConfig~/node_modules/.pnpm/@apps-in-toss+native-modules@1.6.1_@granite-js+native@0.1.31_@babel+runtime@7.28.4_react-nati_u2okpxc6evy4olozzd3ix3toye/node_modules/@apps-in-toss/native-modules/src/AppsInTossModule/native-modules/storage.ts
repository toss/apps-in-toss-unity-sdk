import { AppsInTossModule } from './AppsInTossModule';

/**
 * @public
 * @category 저장소
 * @name getItem
 * @description 모바일 앱의 로컬 저장소에서 문자열 데이터를 가져와요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
 * @param {string} key - 가져올 아이템의 키를 입력해요.
 * @returns {Promise<string | null>} 지정한 키에 저장된 문자열 값을 반환해요. 값이 없으면 `null`을 반환해요.
 * @example
 *
 * ### `my-key`에 저장된 아이템 가져오기
 * ```ts
 * const value = await Storage.getItem('my-key');
 * console.log(value); // 'value'
 * ```
 */
function getItem(key: string): Promise<string | null> {
  return AppsInTossModule.getStorageItem({ key });
}

/**
 * @public
 * @category 저장소
 * @name setItem
 * @description 모바일 앱의 로컬 저장소에 문자열 데이터를 저장해요. 주로 앱이 종료되었다가 다시 시작해도 데이터가 유지되어야 하는 경우에 사용해요.
 * @param {string} key - 저장할 아이템의 키를 입력해요.
 * @param {string} value - 저장할 아이템의 값을 입력해요.
 * @returns {Promise<void>} 아이템을 성공적으로 저장하면 아무 값도 반환하지 않아요.
 * @example
 *
 * ### `my-key`에 아이템 저장하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.setItem('my-key', 'value');
 * ```
 */
function setItem(key: string, value: string): Promise<void> {
  return AppsInTossModule.setStorageItem({
    key,
    value,
  });
}

/**
 * @public
 * @category 저장소
 * @name removeItem
 * @description 모바일 앱의 로컬 저장소에서 특정 키에 해당하는 아이템을 삭제해요.
 * @param {string} key - 삭제할 아이템의 키를 입력해요.
 * @returns {Promise<void>} 아이템을 삭제하면 아무 값도 반환하지 않아요.
 * @example
 *
 * ### `my-key`에 저장된 아이템 삭제하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.removeItem('my-key');
 * ```
 */
function removeItem(key: string): Promise<void> {
  return AppsInTossModule.removeStorageItem({ key });
}

/**
 * @public
 * @category 저장소
 * @name clearItems
 * @description 모바일 앱의 로컬 저장소의 모든 아이템을 삭제해요.
 * @returns {Promise<void>} 아이템을 삭제하면 아무 값도 반환하지 않고 저장소가 초기화돼요.
 * @example
 *
 * ### 저장소 초기화하기
 * ```ts
 * import { Storage } from '@apps-in-toss/framework';
 *
 * await Storage.clearItems();
 * ```
 */
function clearItems(): Promise<void> {
  return AppsInTossModule.clearStorage({});
}

/**
 * @public
 * @category 저장소
 * @name Storage
 * @description 네이티브의 저장소를 사용해요.
 * @property {typeof getItem} [getItem] 모바일 앱의 로컬 저장소에서 아이템을 가져오는 함수예요. 자세한 내용은 [getItem](/react-native/reference/native-modules/저장소/getItem.html)을 참고하세요.
 * @property {typeof setItem} [setItem] 모바일 앱의 로컬 저장소에 아이템을 저장하는 함수예요. 자세한 내용은 [setItem](/react-native/reference/native-modules/저장소/setItem.html)을 참고하셰요.
 * @property {typeof removeItem} [removeItem] 모바일 앱의 로컬 저장소에서 아이템을 삭제하는 함수예요. 자세한 내용은 [removeItem](/react-native/reference/native-modules/저장소/removeItem.html)을 참고하세요.
 * @property {typeof clearItems} [clearItems] 모바일 앱의 로컬 저장소를 초기화하는 함수예요. 자세한 내용은 [clearItems](/react-native/reference/native-modules/저장소/clearItems.html)을 참고하세요.
 */
export const Storage = {
  getItem,
  setItem,
  removeItem,
  clearItems,
};
