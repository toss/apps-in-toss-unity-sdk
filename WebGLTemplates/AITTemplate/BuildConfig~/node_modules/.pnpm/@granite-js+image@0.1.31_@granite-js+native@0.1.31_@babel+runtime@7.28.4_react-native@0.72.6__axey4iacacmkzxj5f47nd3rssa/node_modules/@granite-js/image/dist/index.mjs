import FastImage from "@granite-js/native/react-native-fast-image";
import { StyleSheet, View } from "react-native";
import { SvgUri, SvgXml } from "@granite-js/native/react-native-svg";
import { createElement, useCallback, useEffect, useState } from "react";
import { usePreservedCallback } from "react-simplikit";
import { jsx } from "react/jsx-runtime";

//#region src/SvgImage.tsx
/**
* @name SvgImage
* @category Components
* @description The `SvgImage` component loads and renders SVG images from a given external URL.
* @link https://github.com/software-mansion/react-native-svg/tree/v13.14.0/README.md
*
* @param {object} props - The `props` object passed to the component.
* @param {string} props.url - The URI address of the SVG image to load.
* @param {number | string} [props.width = '100%'] - Sets the horizontal size of the SVG image. Default value is '`100%`'.
* @param {number | string} [props.height = '100%'] - Sets the vertical size of the SVG image. Default value is '`100%`'.
* @param {object} props.style - Sets the style of the image component.
* @param {() => void} props.onLoadStart - A callback function called when the SVG image resource starts loading.
* @param {() => void} props.onLoadEnd - A callback function called after the SVG image resource is loaded.
* @param {() => void} props.onError - A callback function called when an error occurs during SVG image loading.
*
* @example
* ```tsx
* import { SvgImage } from './SvgImage';
* import { View } from 'react-native';
*
* function MyComponent() {
*   return (
*     <View>
*       <SvgImage
*         url="https://example.com/icon.svg"
*         width={100}
*         height={100}
*         onError={() => console.log('An error occurred while loading the SVG')}
*       />
*     </View>
*   );
* }
* ```
*/
function SvgImage({ url, width = "100%", height = "100%", style, testID, onLoadStart: _onLoadStart, onLoadEnd: _onLoadEnd, onError: _onError }) {
	const svgStyle = {
		width,
		height
	};
	const [data, setData] = useState(void 0);
	const [isError, setIsError] = useState(false);
	const onLoadStart = usePreservedCallback(() => _onLoadStart?.());
	const onLoadEnd = usePreservedCallback(() => _onLoadEnd?.());
	const onError = usePreservedCallback(() => _onError?.());
	const Fallback = useCallback(() => createElement(View, { style: {
		width,
		height
	} }, null), [width, height]);
	useEffect(() => {
		let isMounted = true;
		/**
		* First attempts to fetch the XML resource, and if that fails, tries to load directly by passing the URI to the Svg component
		*/
		async function fetchSvg() {
			onLoadStart();
			try {
				const response = await fetch(url);
				const svg = await response.text();
				if (isMounted) {
					onLoadEnd();
					setData(svg);
				}
			} catch {
				setIsError(true);
			}
		}
		fetchSvg();
		return () => {
			isMounted = false;
		};
	}, [
		onLoadStart,
		onLoadEnd,
		url
	]);
	if (data == null) return /* @__PURE__ */ jsx(Fallback, {});
	if (isError) return /* @__PURE__ */ jsx(SvgUri, {
		testID,
		uri: url,
		style,
		...svgStyle,
		onError,
		onLoad: onLoadEnd,
		fallback: /* @__PURE__ */ jsx(Fallback, {})
	});
	return /* @__PURE__ */ jsx(SvgXml, {
		testID,
		xml: data,
		style,
		...svgStyle,
		fallback: /* @__PURE__ */ jsx(Fallback, {})
	});
}

//#endregion
//#region src/Image.tsx
/**
* @public
* @category UI
* @name Image
* @description You can use the `Image` component to load and render bitmap images (such as PNG, JPG) or vector images (SVG). It automatically renders with the appropriate method depending on the image format.
* @link https://github.com/DylanVann/react-native-fast-image/tree/v8.6.3/README.md
*
* @param {object} [props] - The `props` object passed to the component.
* @param {object} [props.style] - An object that defines the style for the image component. It can include layout-related properties like `width` and `height`.
* @param {object} [props.source] - An object containing information about the image resource to load.
* @param {string} [props.source.uri] - The URI address representing the image resource to load.
* @param {'immutable' | 'web' | 'cacheOnly'} [props.source.cache = 'immutable'] - An option to set the image caching strategy. This applies only to bitmap images. The default value is `immutable`.
* @param {() => void} [props.onLoadStart] - A callback function that is called when image loading starts.
* @param {() => void} [props.onLoadEnd] - A callback function that is called when image loading finishes.
* @param {() => void} [props.onError] - A callback function that is called when an error occurs during image loading.
*
* @example
* ### Example: Loading and rendering an image
*
* The following example shows how to load bitmap and vector image resources, and how to print an error message to `console.log` if an error occurs.
*
* ```tsx
* import { Image } from '@granite-js/react-native';
* import { View } from 'react-native';
*
* export function ImageExample() {
*   return (
*     <View>
*       <Image
*         source={{ uri: 'my-image-link' }}
*         style={{
*           width: 300,
*           height: 300,
*           borderWidth: 1,
*         }}
*         onError={() => {
*           console.log('Failed to load image');
*         }}
*       />
*
*       <Image
*         source={{ uri: 'my-svg-link' }}
*         style={{
*           width: 300,
*           height: 300,
*           borderWidth: 1,
*         }}
*         onError={() => {
*           console.log('Failed to load image');
*         }}
*       />
*     </View>
*   );
* }
* ```
*/
function Image(props) {
	if (typeof props.source === "object" && props.source.uri?.endsWith(".svg")) {
		const style = StyleSheet.flatten(props.style);
		const width = style?.width;
		const height = style?.height;
		return /* @__PURE__ */ jsx(SvgImage, {
			testID: props.testID,
			url: props.source.uri,
			width,
			height,
			style: props.style,
			onLoadStart: props.onLoadStart,
			onLoadEnd: props.onLoadEnd,
			onError: props.onError
		});
	}
	return /* @__PURE__ */ jsx(FastImage, { ...props });
}

//#endregion
export { Image };