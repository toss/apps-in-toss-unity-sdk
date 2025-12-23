import type { ReactNode } from 'react';
export default function useOverlayCache(): {
    mount: (id: string, element: ReactNode) => void;
    unmount: (id: string) => void;
    overlayById: Map<string, ReactNode>;
};
