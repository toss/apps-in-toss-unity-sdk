import type { ReactNode } from 'react';
export type CommonToastProps = {
    open: boolean;
    /**
     * @example
     * icon={<Toast.Icon name="icn-attention-color" style={{opacity: 0.4}} />}
     * icon={<Toast.LottieIcon preset type="complete" />}
     * icon={<Toast.LottieIcon src="https://static.toss.im/lotties-common/check-green-spot.json" />}
     */
    icon?: ReactNode;
    text: string;
    /**
     * @default button 있으면 5초, 없으면 3초
     */
    duration?: number;
    onClose: () => void;
    onExited?: () => void;
    onEntered?: () => void;
};
