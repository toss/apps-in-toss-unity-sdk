/**
 * @public
 * @category Screen Control
 * @kind function
 * @name closeView
 * @description Function that closes the current screen. It can be used when you want to exit a service by pressing a "Close" button.
 * @returns {Promise<void>}
 *
 * @example
 * ### Close screen with close button
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { closeView } from '@granite-js/react-native';
 *
 * function CloseButton() {
 *  return <Button title="Close" onPress={closeView} />;
 * }
 * ```
 */
declare function closeView(): Promise<void>;

/**
 * @public
 * @kind function
 * @category Screen Navigation
 *
 * @name openURL
 * @signature
 * ```typescript
 * function openURL(url: string): Promise<any>;
 * ```
 *
 * @description
 * Opens the specified URL in the device's default browser or related app.
 * This function uses the [`Linking.openURL`](https://reactnative.dev/docs/0.72/linking#openurl) method from `react-native` to open the URL.
 *
 * @param {string} url URL address to open
 * @returns {Promise<any>} Promise that resolves when the URL is successfully opened
 *
 * @example
 *
 * ### Open external URL
 *
 * ```tsx
 * import { openURL } from '@granite-js/react-native';
 * import { Button } from 'react-native';
 *
 * function Page() {
 *   const handlePress = () => {
 *     openURL('https://google.com');
 *   };
 *
 *   return <Button title="Open Google Website" onPress={handlePress} />;
 * }
 * ```
 */
declare function openURL(url: string): Promise<any>;

export { closeView, openURL };
