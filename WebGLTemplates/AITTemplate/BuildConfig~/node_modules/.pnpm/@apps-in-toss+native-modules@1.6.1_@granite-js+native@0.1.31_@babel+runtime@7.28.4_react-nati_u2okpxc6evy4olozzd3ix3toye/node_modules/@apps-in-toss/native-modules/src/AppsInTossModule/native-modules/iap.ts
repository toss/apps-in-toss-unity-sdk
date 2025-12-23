import { noop } from 'es-toolkit';
import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';
import { INTERNAL__appBridgeHandler } from '../native-event-emitter/internal/appBridge';

type Sku =
  | {
      /**
       * @deprecated `productId`는 더 이상 사용하지 않아요. 대신 `sku`를 사용해요.
       */
      productId: string;
      sku?: string;
    }
  | {
      productId?: never;
      sku: string;
    };

/**
 * @public
 * @category 인앱결제
 * @name IapCreateOneTimePurchaseOrderResult
 * @description 인앱결제 1건이 완료되면 결제 세부 정보와 상품 정보를 담아 반환해요. 반환된 정보로 결제한 상품의 정보를 화면에 표시할 수 있어요.
 * @property {string} orderId - 결제 주문 ID이에요. 결제 완료 후 [결제 상태를 조회](https://developers-apps-in-toss.toss.im/api/getIapOrderStatus.html)할 때 사용해요.
 * @property {string} displayName - 화면에 표시할 상품 이름이에요.
 * @property {string} displayAmount - 통화 단위가 포함된 가격 정보예요. 예를 들어 `1,000원`으로 가격과 통화가 함께 표시돼요.
 * @property {number} amount - 상품 가격 숫자 값이에요. 화폐 단위와 쉼표를 제외한 순수 숫자예요. 예를 들어 `1000`으로 표시돼요.
 * @property {string} currency - [ISO 4217 표준](https://ko.wikipedia.org/wiki/ISO_4217)에 따른 상품 가격 통화 단위예요. 예를 들어 원화는 `KRW`, 달러는 `USD`로 표시돼요.
 * @property {number} fraction - 가격을 표시할 때 소수점 아래 몇 자리까지 보여줄지 정하는 값이에요. 예를 들어 달러는 소수점 둘째 자리까지 보여줘서 `2`, 원화는 소수점이 필요 없어서 `0`이에요
 * @property {string | null} miniAppIconUrl - 미니앱 아이콘 이미지의 URL이에요. 아이콘은 앱인토스 콘솔에서 설정한 이미지예요. 콘솔에서 아이콘을 등록하지 않았다면 `null`로 반환돼요.
 */
export interface IapCreateOneTimePurchaseOrderResult {
  orderId: string;
  displayName: string;
  displayAmount: string;
  amount: number;
  currency: string;
  fraction: number;
  miniAppIconUrl: string | null;
}

interface SuccessEvent {
  type: 'success';
  data: IapCreateOneTimePurchaseOrderResult;
}

interface PurchasedEvent {
  type: 'purchased';
  data: { orderId: string };
}

/**
 * @public
 * @category 인앱결제
 * @name IapCreateOneTimePurchaseOrderOptions
 * @property {Sku} options - 결제할 상품의 정보예요.
 * @property {string} options.sku - 주문할 상품의 고유 ID예요.
 * @property {(params: { orderId: string }) => boolean | Promise<boolean>} processProductGrant - 주문이 만들어진 뒤 실제로 상품을 지급할 때 호출해요. `orderId`를 받아서 지급 성공 여부를 `true` 또는 `Promise<true>`로 반환해요. 지급에 실패하면 `false`를 반환해요.
 * @property {(event: SuccessEvent) => void | Promise<void>} onEvent - 결제가 성공했을 때 호출해요. `event.type`이 `'success'`이고, `event.data`에 `IapCreateOneTimePurchaseOrderResult`가 들어 있어요.
 * @property {(error: unknown) => void | Promise<void>} onError - 결제 과정에서 에러가 발생했을 때 호출해요. 에러 객체를 받아서 로깅하거나 복구 절차를 실행할 수 있어요.
 */
export interface IapCreateOneTimePurchaseOrderOptions {
  options: Sku & { processProductGrant: (params: { orderId: string }) => boolean | Promise<boolean> };
  onEvent: (event: SuccessEvent) => void | Promise<void>;
  onError: (error: unknown) => void | Promise<void>;
}

interface IapRequestOneTimePurchaseOptions {
  options: Sku;
  onEvent: (event: PurchasedEvent | SuccessEvent) => void | Promise<void>;
  onError: (error: unknown) => void | Promise<void>;
}

export function iapCreateOneTimePurchaseOrder(params: Sku) {
  const sku = (params.sku ?? params.productId) as string;
  return AppsInTossModule.iapCreateOneTimePurchaseOrder({ productId: sku });
}

export function processProductGrant(params: { orderId: string; isProductGranted: boolean }) {
  return AppsInTossModule.processProductGrant({ orderId: params.orderId, isProductGranted: params.isProductGranted });
}

export function requestOneTimePurchase(params: IapRequestOneTimePurchaseOptions) {
  const { options, onEvent, onError } = params;
  const sku = (options.sku ?? options.productId) as string;

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    'requestOneTimePurchase',
    { sku },
    {
      onPurchased: (params: { orderId: string }) => {
        onEvent({ type: 'purchased', data: params });
      },
      onSuccess: (result: IapCreateOneTimePurchaseOrderResult) => {
        onEvent({ type: 'success', data: result });
      },
      onError: (error: any) => {
        onError(error);
      },
    }
  );

  return unregisterCallbacks;
}

/**
 * @public
 * @category 인앱결제
 * @name createOneTimePurchaseOrder
 * @description
 * 특정 인앱결제 주문서 페이지로 이동해요. 사용자가 상품 구매 버튼을 누르는 상황 등에 사용할 수 있어요. 사용자의 결제는 이동한 페이지에서 진행돼요. 만약 결제 중에 에러가 발생하면 에러 유형에 따라 에러 페이지로 이동해요.
 * @param {IapCreateOneTimePurchaseOrderOptions} params - 인앱결제를 생성할 때 필요한 정보예요.
 * @returns {() => void} 앱브릿지 cleanup 함수를 반환해요. 인앱결제 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.
 *
 * @throw {code: "INVALID_PRODUCT_ID"} - 유효하지 않은 상품 ID이거나, 해당 상품이 존재하지 않을 때 발생해요.
 * @throw {code: "PAYMENT_PENDING"} - 사용자가 요청한 결제가 아직 승인을 기다리고 있을 때 발생해요.
 * @throw {code: "NETWORK_ERROR"} - 서버 내부 문제로 요청을 처리할 수 없을 때 발생해요.
 * @throw {code: "INVALID_USER_ENVIRONMENT"} - 특정 기기, 계정 또는 설정 환경에서 구매할 수 없는 상품일 때 발생해요.
 * @throw {code: "ITEM_ALREADY_OWNED"} - 사용자가 이미 구매한 상품을 다시 구매하려고 할 때 발생해요.
 * @throw {code: "APP_MARKET_VERIFICATION_FAILED"} - 사용자가 결제를 완료했지만, 앱스토어에서 사용자 정보 검증에 실패했을 때 발생해요. 사용자가 앱스토어에 문의해서 환불을 요청해야해요.
 * @throw {code: "TOSS_SERVER_VERIFICATION_FAILED"} - 사용자가 결제를 완료했지만, 서버 전송에 실패해서 결제 정보를 저장할 수 없을 때 발생해요.
 * @throw {code: "INTERNAL_ERROR"} - 서버 내부 문제로 요청을 처리할 수 없을 때 발생해요.
 * @throw {code: "KOREAN_ACCOUNT_ONLY"} - iOS 환경에서 사용자의 계정이 한국 계정이 아닐 때 발생해요.
 * @throw {code: "USER_CANCELED"} - 사용자가 결제를 완료하지 않고 주문서 페이지를 이탈했을 때 발생해요.
 * @throw {code: "PRODUCT_NOT_GRANTED_BY_PARTNER"} - 파트너사의 상품 지급이 실패했을 때 발생해요.
 *
 * @example
 * ### 특정 인앱결제 주문서 페이지로 이동하기
 *
 * ```tsx
 * import { IAP } from "@apps-in-toss/web-framework";
 * import { Button } from "@toss/tds-react-native";
 * import { useCallback } from "react";
 *
 * interface Props {
 *   sku: string;
 * }
 *
 * function IapCreateOneTimePurchaseOrderButton({ sku }: Props) {
 *   const handleClick = useCallback(async () => {
 *
 *     const cleanup = await IAP.createOneTimePurchaseOrder({
 *       options: {
 *        sku,
 *        processProductGrant: ({ orderId }) => {
 *         // 상품 지급 로직 작성
 *          return true; // 상품 지급 여부
 *        }
 *        },
 *       onEvent: (event) => {
 *         console.log(event);
 *       },
 *       onError: (error) => {
 *         console.error(error);
 *       },
 *     });
 *
 *     return cleanup;
 *   }, []);
 *
 *   return <Button onClick={handleClick}>구매하기</Button>;
 * }
 * ```
 */
function createOneTimePurchaseOrder(params: IapCreateOneTimePurchaseOrderOptions) {
  const isIAPSupported = isMinVersionSupported({
    android: '5.219.0',
    ios: '5.219.0',
  });

  if (!isIAPSupported) {
    return noop;
  }

  const isProcessProductGrantSupported = isMinVersionSupported({
    android: '5.231.1',
    ios: '5.230.0',
  });

  const { options, onEvent, onError } = params;
  const sku = (options.sku ?? options.productId) as string;

  if (!isProcessProductGrantSupported) {
    const v1 = () => {
      AppsInTossModule.iapCreateOneTimePurchaseOrder({ productId: sku })
        .then((response: IapCreateOneTimePurchaseOrderResult) => {
          Promise.resolve(options.processProductGrant({ orderId: response.orderId }))
            .then(() => {
              onEvent({ type: 'success', data: response });
            })
            .catch((error: unknown) => {
              onError(error);
            });
        })
        .catch((error: unknown) => {
          onError(error);
        });

      return noop;
    };

    return v1();
  }

  const unregisterCallbacks = INTERNAL__appBridgeHandler.invokeAppBridgeMethod(
    'requestOneTimePurchase',
    { sku },
    {
      onPurchased: async (params: { orderId: string }) => {
        const isProductGranted = await options.processProductGrant(params);
        await AppsInTossModule.processProductGrant({ orderId: params.orderId, isProductGranted });
      },
      onSuccess: (result: IapCreateOneTimePurchaseOrderResult) => {
        onEvent({ type: 'success', data: result });
      },
      onError: (error: unknown) => {
        onError(error);
      },
    }
  );

  return unregisterCallbacks;
}

/**
 * @public
 * @category 인앱결제
 * @name IapProductListItem
 * @description 인앱결제로 구매할 수 있는 상품 하나의 정보를 담은 객체예요. 상품 목록을 화면에 표시할 때 사용해요.
 * @property {string} sku - 상품의 고유 ID예요.
 * @property {string} displayName - 화면에 표시할 상품 이름이에요. 상품 이름은 앱인토스 콘솔에서 설정한 값이에요.
 * @property {string} displayAmount - 통화 단위가 포함된 가격 정보예요. 예를 들어 `1,000원`으로 가격과 통화가 함께 표시돼요.
 * @property {string} iconUrl - 상품 아이콘 이미지의 URL이에요. 아이콘은 앱인토스 콘솔에서 설정한 이미지예요.
 * @property {string} description - 상품에 대한 설명이에요. 설명은 앱인토스 콘솔에서 설정한 값이에요.
 */
export interface IapProductListItem {
  sku: string;
  displayAmount: string;
  displayName: string;
  iconUrl: string;
  description: string;
}

/**
 * @public
 * @category 인앱결제
 * @name getProductItemList
 * @description 인앱결제로 구매할 수 있는 상품 목록을 가져와요. 상품 목록 화면에 진입할 때 호출해요.
 * @returns {Promise<{ products: IapProductListItem[] } | undefined>} 상품 목록을 포함한 객체를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.219.0, iOS 5.219.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 구매 가능한 인앱결제 상품목록 가져오기
 *
 * ```tsx
 * import { IAP, IapProductListItem } from "@apps-in-toss/framework";
 * import { Button, List, ListRow } from "@toss/tds-react-native";
 * import { useEffect, useState } from "react";
 *
 * function IapProductList() {
 *   const [products, setProducts] = useState<IapProductListItem[]>([]);
 *
 *   async function buyIapProduct(productId: string) {
 *     try {
 *       await IAP.createOneTimePurchaseOrder({
 *         productId,
 *       });
 *
 *       console.error("인앱결제에 성공했어요");
 *     } catch (error) {
 *       console.error("인앱결제에 실패했어요:", error);
 *     }
 *   }
 *
 *   useEffect(() => {
 *     async function fetchProducts() {
 *       try {
 *         const response = await IAP.getProductItemList();
 *         setProducts(response?.products ?? []);
 *       } catch (error) {
 *         console.error("상품 목록을 가져오는 데 실패했어요:", error);
 *       }
 *     }
 *
 *     fetchProducts();
 *   }, []);
 *
 *   return (
 *     <List>
 *       {products.map((product) => (
 *         <ListRow
 *           key={product.sku}
 *           left={
 *             <ListRow.Image type="square" source={{ uri: product.iconUrl }} />
 *           }
 *           right={
 *             <Button size="medium" onPress={() => buyIapProduct(product.sku)}>
 *               구매하기
 *             </Button>
 *           }
 *           contents={
 *             <ListRow.Texts
 *               type="3RowTypeA"
 *               top={product.displayName}
 *               middle={product.description}
 *               bottom={product.displayAmount}
 *             />
 *           }
 *         />
 *       ))}
 *     </List>
 *   );
 * }
 * ```
 */
async function getProductItemList() {
  const isSupported = isMinVersionSupported({
    android: '5.219.0',
    ios: '5.219.0',
  });

  if (!isSupported) {
    return;
  }

  return AppsInTossModule.iapGetProductItemList({});
}

/**
 * @public
 * @category 인앱결제
 * @name getPendingOrders
 * @description 대기 중인 주문 목록을 가져와요. 이 함수를 사용하면 결제가 아직 완료되지 않은 주문 정보를 확인할 수 있어요.
 * @returns {Promise<{ orders: { orderId: string; sku: string; paymentCompletedDate: string; }[]}>} 대기 중인 주문의 배열을 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.234.0, iOS 5.231.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 대기 중인 주문 목록 가져오기
 * ```typescript
 * import { IAP } from '@apps-in-toss/framework';
 *
 * async function fetchOrders() {
 *   try {
 *     const pendingOrders = await IAP.getPendingOrders();
 *     return pendingOrders;
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
async function getPendingOrders() {
  const isSupported = isMinVersionSupported({
    android: '5.234.0',
    ios: '5.231.0',
  });

  if (!isSupported) {
    return;
  }

  return AppsInTossModule.getPendingOrders({});
}

/**
 * @public
 * @category 인앱결제
 * @name CompletedOrRefundedOrdersResult
 * @description 인앱결제로 구매하거나 환불한 주문 목록을 나타내는 객체예요. 페이지네이션 정보를 포함해요.
 * @property {boolean} hasNext 다음 페이지가 있는지 여부예요. `true`면 더 많은 주문이 남아 있어요.
 * @property {string | null} [nextKey] 다음 주문 목록을 조회할 때 사용할 키예요. 마지막 페이지라면 `null`이거나 생략될 수 있어요.
 * @property {Array} orders 주문 정보를 담은 배열이에요. 각 요소는 하나의 주문을 나타내요.
 * @property {string} orders[].orderId 주문의 고유 ID예요.
 * @property {string} orders[].sku 주문 상품의 고유 ID예요.
 * @property {'COMPLETED' | 'REFUNDED'} orders[].status 주문의 상태예요. 'COMPLETED'는 주문이 완료된 상태, 'REFUNDED'는 환불된 상태를 의미해요.
 * @property {string} orders[].date 주문의 날짜 정보예요. ISO 8601 형식(YYYY-MM-DDTHH:mm:ss)을 사용해요. 예를 들어 "2025-09-22T00:00:00" 형식으로 제공돼요. 주문 상태가 `COMPLETED`라면 주문한 날짜를, `REFUNDED`라면 환불한 날짜를 나타내요.
 */
export interface CompletedOrRefundedOrdersResult {
  hasNext: boolean;
  nextKey?: string | null;
  orders: { orderId: string; sku: string; status: 'COMPLETED' | 'REFUNDED'; date: string }[];
}

/**
 * @public
 * @category 인앱결제
 * @name getCompletedOrRefundedOrders
 * @description 인앱결제로 구매하거나 환불한 주문 목록을 가져와요.
 * @returns {Promise<CompletedOrRefundedOrdersResult>} 페이지네이션을 포함한 주문 목록 객체를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.231.0, iOS 5.231.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ```typescript
 * import { IAP } from "@apps-in-toss/framework";
 *
 * async function fetchOrders() {
 *   try {
 *     const response =  await IAP.getCompletedOrRefundedOrders();
 *     return response;
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
async function getCompletedOrRefundedOrders(params?: { key?: string | null }) {
  const isSupported = isMinVersionSupported({
    android: '5.231.0',
    ios: '5.231.0',
  });

  if (!isSupported) {
    return;
  }

  return AppsInTossModule.getCompletedOrRefundedOrders(params ?? { key: null });
}

/**
 * @public
 * @category 인앱결제
 * @name completeProductGrant
 * @description 상품 지급 처리를 완료했다는 메시지를 앱에 전달해요. 이 함수를 사용하면 결제가 완료된 주문의 상품 지급이 정상적으로 완료되었음을 알릴 수 있어요.
 * @param {{ params: { orderId: string } }} params 결제가 완료된 주문 정보를 담은 객체예요.
 * @param {string} params.orderId 주문의 고유 ID예요. 상품 지급을 완료할 주문을 지정할 때 사용해요.
 * @returns {Promise<boolean>} 상품 지급이 완료됐는지 여부를 반환해요. 앱 버전이 최소 지원 버전(안드로이드 5.233.0, iOS 5.233.0)보다 낮으면 `undefined`를 반환해요.
 *
 * @example
 * ### 결제를 성공한 뒤 상품을 지급하는 예시
 * ```typescript
 * import { IAP } from '@apps-in-toss/framework';
 *
 * async function handleGrantProduct(orderId: string) {
 *   try {
 *     await IAP.completeProductGrant({ params: { orderId } });
 *   } catch (error) {
 *     console.error(error);
 *   }
 * }
 * ```
 */
async function completeProductGrant(params: { params: { orderId: string } }) {
  const isSupported = isMinVersionSupported({
    android: '5.233.0',
    ios: '5.233.0',
  });

  if (!isSupported) {
    return;
  }

  return AppsInTossModule.completeProductGrant(params);
}

/**
 * @public
 * @category 인앱결제
 * @name IAP
 * @description 인앱결제 관련 기능을 모은 객체예요. 단건 인앱결제 주문서 이동과 상품 목록 조회 기능을 제공해요.
 * @property {typeof createOneTimePurchaseOrder} [createOneTimePurchaseOrder] 특정 인앱결제 주문서 페이지로 이동해요. 자세한 내용은 [createOneTimePurchaseOrder](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/createOneTimePurchaseOrder.html) 문서를 참고하세요.
 * @property {typeof getProductItemList} [getProductItemList] 인앱결제로 구매할 수 있는 상품 목록을 가져와요. 자세한 내용은 [getProductItemList](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getProductItemList.html) 문서를 참고하세요.
 * @property {typeof getPendingOrders} [getPendingOrders] 대기 중인 주문 목록을 가져와요. 자세한 내용은 [getPendingOrders](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getPendingOrders.htm) 문서를 참고하세요.
 * @property {typeof getCompletedOrRefundedOrders} [getCompletedOrRefundedOrders] 인앱결제로 구매하거나 환불한 주문 목록을 가져와요. 자세한 내용은 [getCompletedOrRefundedOrders](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/getCompletedOrRefundedOrders.html) 문서를 참고하세요.
 * @property {typeof completeProductGrant} [completeProductGrant] 상품 지급 처리를 완료했다는 메시지를 앱에 전달해요. 자세한 내용은 [completeProductGrant](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/completeProductGrant.html) 문서를 참고하세요.
 */
export const IAP = {
  createOneTimePurchaseOrder,
  getProductItemList,
  getPendingOrders,
  getCompletedOrRefundedOrders,
  completeProductGrant,
};
