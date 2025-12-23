

export interface EventEmitterSchema<K extends string, P extends unknown[]> {
	name: K;
	params: P;
}
export interface OnVisibilityChangedByTransparentServiceWebSubscription extends EmitterSubscription {
	remove: () => void;
}
export type OnVisibilityChangedByTransparentServiceWebEventEmitter = EventEmitterSchema<"visibilityChangedByTransparentServiceWeb", [
	boolean
]>;
export declare function onVisibilityChangedByTransparentServiceWeb(eventParams: {
	options: {
		callbackId: string;
	};
	onEvent: (isVisible: boolean) => void;
	onError: (error: unknown) => void;
}): () => void;

export {};
