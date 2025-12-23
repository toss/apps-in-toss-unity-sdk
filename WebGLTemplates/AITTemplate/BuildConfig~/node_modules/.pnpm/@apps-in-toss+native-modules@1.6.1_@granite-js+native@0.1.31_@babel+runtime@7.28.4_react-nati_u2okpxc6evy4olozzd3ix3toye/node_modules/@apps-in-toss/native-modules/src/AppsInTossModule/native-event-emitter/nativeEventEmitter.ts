import { NativeEventEmitter, type EmitterSubscription } from 'react-native';
import type { OnVisibilityChangedByTransparentServiceWebEventEmitter } from './internal/onVisibilityChangedByTransparentServiceWeb';
import type { UpdateLocationEventEmitter } from './startUpdateLocation';
import type { EventEmitterSchema } from './types';
import { AppsInTossModuleInstance } from '../native-modules/AppsInTossModule';

type EventEmitters = UpdateLocationEventEmitter | OnVisibilityChangedByTransparentServiceWebEventEmitter;

type MapOf<T> = T extends EventEmitterSchema<infer K, any> ? { [key in K]: T } : never;
type UnionToIntersection<U> = (U extends any ? (k: U) => void : never) extends (k: infer I) => void ? I : never;
type EventEmittersMap = UnionToIntersection<MapOf<EventEmitters>>;
type EventKeys = keyof EventEmittersMap;
type ParamOf<K extends EventKeys> = EventEmittersMap[K]['params'];
/**
 * @interface AppsInTossEventEmitter
 * @description
 * 네이티브 플랫폼에서 발생하는 이벤트들을 처리하는 NativeEventEmitter를 App In Toss 프레임워크에서 사용하는 형태에 맞게 정의한 인터페이스에요.
 * @property {(event: EventKeys, callback: (...params: ParamOf<E>) => void) => EmitterSubscription} addListener - 이벤트 리스너를 추가하는 함수
 * @property {(subscription: EmitterSubscription) => void} removeSubscription - 이벤트 구독을 제거하는 함수
 */
interface AppsInTossEventEmitter {
  addListener<Event extends EventKeys>(
    event: Event,
    callback: (...params: ParamOf<Event>) => void
  ): EmitterSubscription;
}

/**
 * @kind constant
 * @name nativeEventEmitter
 * @description
 * App In Toss 프레임워크에서 제공하는 react-native의 NativeEventEmitter instance에요.
 * @type {AppsInTossEventEmitter}
 */
export const nativeEventEmitter = new NativeEventEmitter(AppsInTossModuleInstance) as unknown as AppsInTossEventEmitter;
