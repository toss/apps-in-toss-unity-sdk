type OneOrMore<T> = T | T[];
/**
 * @description
 * `useOutsideClickEffect` is a React hook that triggers a callback when a click event occurs outside the specified container(s).
 * It is useful for closing modals, dropdowns, tooltips, and other UI components when clicking outside.
 *
 * @param {HTMLElement | HTMLElement[] | null} container - A single HTML element, an array of HTML elements, or `null`.
 *   If `null`, no event listener is attached.
 * @param {() => void} callback - A function that is executed when clicking outside the specified container(s).
 *
 * @example
 * import { useOutsideClickEffect } from 'react-simplikit';
 * import { useState } from 'react';
 *
 * function Example() {
 *   const [wrapperEl, setWrapperEl] = useState<HTMLDivElement | null>(null);
 *
 *   useOutsideClickEffect(wrapperEl, () => {
 *     console.log('Outside clicked!');
 *   });
 *
 *   return <div ref={setWrapperEl}>Content</div>;
 * }
 */
declare function useOutsideClickEffect(container: OneOrMore<HTMLElement | null>, callback: () => void): void;

export { useOutsideClickEffect };
