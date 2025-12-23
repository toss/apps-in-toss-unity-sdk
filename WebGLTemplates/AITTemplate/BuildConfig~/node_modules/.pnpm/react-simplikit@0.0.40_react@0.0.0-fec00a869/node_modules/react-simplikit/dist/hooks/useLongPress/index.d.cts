import { MouseEvent, TouchEvent } from 'react';

type Handler<E extends HTMLElement> = (event: MouseEvent<E> | TouchEvent<E>) => void;
type UseLongPressOptions<E extends HTMLElement> = {
    delay?: number;
    moveThreshold?: {
        x?: number;
        y?: number;
    };
    onClick?: Handler<E>;
    onLongPressEnd?: Handler<E>;
};
/**
 * @description
 * `useLongPress` is a React hook that detects when an element is pressed and held for a specified duration.
 * It handles both mouse and touch events, making it work consistently across desktop and mobile devices.
 *
 * @template {HTMLElement} E - The HTML element type that will use the long press handlers.
 * @param {(event: React.MouseEvent<E> | React.TouchEvent<E>) => void} onLongPress - The callback function to be executed when a long press is detected.
 * @param {UseLongPressOptions} [options] - Configuration options for the long press behavior.
 * @param {number} [options.delay=500] - The time in milliseconds before triggering the long press. Defaults to 500ms.
 * @param {Object} [options.moveThreshold] - Maximum movement allowed before canceling a long press.
 * @param {number} [options.moveThreshold.x] - Maximum horizontal movement in pixels.
 * @param {number} [options.moveThreshold.y] - Maximum vertical movement in pixels.
 * @param {(event) => void} [options.onClick] - Optional function to execute on a normal click (press and release before delay).
 * @param {(event) => void} [options.onLongPressEnd] - Optional function to execute when a long press ends.
 *
 * @returns {Object} Event handlers to attach to an element.
 * - onMouseDown `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for mouse down events.
 * - onMouseUp `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for mouse up events.
 * - onMouseLeave `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for mouse leave events.
 * - onTouchStart `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for touch start events.
 * - onTouchEnd `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for touch end events.
 * - onMouseMove `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for mouse move events. Included if `moveThreshold` is provided.
 * - onTouchMove `(event: MouseEvent<E> | TouchEvent<E>) => void` - Event handler for touch move events. Included if `moveThreshold` is provided.
 *
 * @example
 * import { useLongPress } from 'react-simplikit';
 *
 * function ContextMenu() {
 *   const [menuVisible, setMenuVisible] = useState(false);
 *
 *   const longPressHandlers = useLongPress(
 *     () => setMenuVisible(true),
 *     {
 *       delay: 400,
 *       onClick: () => console.log('Normal click'),
 *       onLongPressEnd: () => console.log('Long press completed')
 *     }
 *   );
 *
 *   return (
 *     <div>
 *       <button {...longPressHandlers}>Press and hold</button>
 *       {menuVisible && <div className="context-menu">Context Menu</div>}
 *     </div>
 *   );
 * }
 */
declare function useLongPress<E extends HTMLElement = HTMLElement>(onLongPress: Handler<E>, { delay, moveThreshold, onClick, onLongPressEnd }?: UseLongPressOptions<E>): {
    onTouchMove?: ((event: MouseEvent<E> | TouchEvent<E>) => void) | undefined;
    onMouseMove?: ((event: MouseEvent<E> | TouchEvent<E>) => void) | undefined;
    onMouseDown: (event: MouseEvent<E> | TouchEvent<E>) => void;
    onMouseUp: (event: MouseEvent<E> | TouchEvent<E>) => void;
    onMouseLeave: () => void;
    onTouchStart: (event: MouseEvent<E> | TouchEvent<E>) => void;
    onTouchEnd: (event: MouseEvent<E> | TouchEvent<E>) => void;
};

export { useLongPress };
