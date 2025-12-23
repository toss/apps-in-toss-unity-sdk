export interface SaveBase64DataParams {
	data: string;
	fileName: string;
	mimeType: string;
}
/**
 * @public
 * @category 데이터
 * @name saveBase64Data
 * @description 문자열로 인코딩된 Base64 데이터를 지정한 파일 이름과 MIME 타입으로 사용자 기기에 저장해요. 이미지, 텍스트, PDF 등 다양한 형식의 데이터를 저장할 수 있어요.
 * @param {SaveBase64DataParams} params - 저장할 데이터와 파일 정보를 담은 객체예요.
 * @param {string} params.data - Base64 형식으로 인코딩된 데이터 문자열이에요.
 * @param {string} params.fileName - 저장할 파일 이름이에요. 확장자도 같이 붙여줘야해요. 예를 들어, 'example.png'로 저장할 수 있어요.
 * @param {string} params.mimeType - 저장할 파일의 MIME 타입이에요. 예를 들어 'image/png' 로 지정하면 이미지, 'application/pdf'는 PDF 파일이에요. 자세한 내용은 [MIME 문서](https://developer.mozilla.org/ko/docs/Web/HTTP/Guides/MIME_types)를 참고해주세요.
 *
 * @example
 * ### Base64 이미지 데이터를 사용자 기기에 저장하기
 *
 * ```tsx
 * 
 * import { saveBase64Data } from '@apps-in-toss/web-framework';
 *
 * // '저장' 버튼을 누르면 이미지가 사용자 기기에 저장돼요.
 * function SaveButton() {
 *   const handleSave = async () => {
 *     try {
 *       await saveBase64Data({
 *         data: 'iVBORw0KGgo...',
 *         fileName: 'some-photo.png',
 *         mimeType: 'image/png',
 *       });
 *     } catch (error) {
 *       console.error('데이터 저장에 실패했어요:', error);
 *     }
 *   };
 *
 *   return <input type="button" value="저장" onClick={handleSave} />;
 * }
 * ```
 */
export declare function saveBase64Data(params: SaveBase64DataParams): Promise<void>;

export {};
