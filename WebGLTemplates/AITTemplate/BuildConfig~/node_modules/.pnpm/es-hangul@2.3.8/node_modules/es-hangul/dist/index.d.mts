/**
 * @name assemble
 * @description
 * 인자로 받은 배열에 담긴 한글 문장과 문자를 한글 규칙에 맞게 합성합니다.
 * ```typescript
 * assemble(
 *   // 한글 문자와 문장을 담고 있는 배열
 *   fragments: string[]
 * ): string
 * ```
 * @example
 * assemble(['아버지가', ' ', '방ㅇ', 'ㅔ ', '들ㅇ', 'ㅓ갑니다']) // 아버지가 방에 들어갑니다
 * assemble(['아버지가', ' ', '방에 ', '들어갑니다']) // 아버지가 방에 들어갑니다
 * assemble(['ㅇ', 'ㅏ', 'ㅂ', 'ㅓ', 'ㅈ', 'ㅣ']) // 아버지
 */
declare function assemble(fragments: string[]): string;

declare function disassemble(str: string): string;

/**
 * 초성으로 올 수 있는 한글 글자
 */
declare const CHOSEONGS: readonly ["ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"];
/**
 * 중성으로 올 수 있는 한글 글자
 */
declare const JUNGSEONGS: ("ㅏ" | "ㅐ" | "ㅑ" | "ㅒ" | "ㅓ" | "ㅔ" | "ㅕ" | "ㅖ" | "ㅗ" | "ㅗㅏ" | "ㅗㅐ" | "ㅗㅣ" | "ㅛ" | "ㅜ" | "ㅜㅓ" | "ㅜㅔ" | "ㅜㅣ" | "ㅠ" | "ㅡ" | "ㅡㅣ" | "ㅣ")[];
/**
 * 종성으로 올 수 있는 한글 글자
 */
declare const JONGSEONGS: ("" | "ㄱ" | "ㄲ" | "ㄱㅅ" | "ㄴ" | "ㄴㅈ" | "ㄴㅎ" | "ㄷ" | "ㄹ" | "ㄹㄱ" | "ㄹㅁ" | "ㄹㅂ" | "ㄹㅅ" | "ㄹㅌ" | "ㄹㅍ" | "ㄹㅎ" | "ㅁ" | "ㅂ" | "ㅂㅅ" | "ㅅ" | "ㅆ" | "ㅇ" | "ㅈ" | "ㅊ" | "ㅋ" | "ㅌ" | "ㅍ" | "ㅎ")[];

interface ReturnTypeDisassembleCompleteCharacter {
    choseong: (typeof CHOSEONGS)[number];
    jungseong: (typeof JUNGSEONGS)[number];
    jongseong: (typeof JONGSEONGS)[number];
}
/**
 * @name disassembleCompleteCharacter
 * @description
 * 완전한 한글 문자열을 초성, 중성, 종성으로 분리합니다.
 *
 * @param {string} letter 분리하고자 하는 완전한 한글 문자열
 *
 * @example
 * disassembleCompleteCharacter('값') // { choseong: 'ㄱ', jungseong: 'ㅏ', jongseong: 'ㅂㅅ' }
 * disassembleCompleteCharacter('리') // { choseong: 'ㄹ', jungseong: 'ㅣ', jongseong: '' }
 * disassembleCompleteCharacter('빚') // { choseong: 'ㅂ', jungseong: 'ㅣ', jongseong: 'ㅈ' }
 * disassembleCompleteCharacter('박') // { choseong: 'ㅂ', jungseong: 'ㅏ', jongseong: 'ㄱ' }
 */
declare function disassembleCompleteCharacter(letter: string): ReturnTypeDisassembleCompleteCharacter | undefined;

declare function disassembleToGroups(str: string): string[][];

/**
 * @name combineCharacter
 * @description
 * 인자로 초성, 중성, 종성을 받아 하나의 한글 문자를 반환합니다.
 * ```typescript
 * combineCharacter(
 *   // 초성
 *   choseong: string
 *   // 중성
 *   jungseong: string
 *   // 종성
 *   jongseong: string
 * ): string
 * ```
 * @example
 * combineCharacter('ㄱ', 'ㅏ', 'ㅂㅅ') // '값'
 * combineCharacter('ㅌ', 'ㅗ') // '토'
 */
declare function combineCharacter(choseong: string, jungseong: string, jongseong?: string): string;

/**
 * @name combineVowels
 * @description
 * 인자로 두 개의 모음을 받아 합성하여 겹모음을 생성합니다. 만약 올바른 한글 규칙으로 합성할 수 없는 모음들이라면 단순 Join합니다.
 * ```typescript
 * combineVowels(
 *   // 첫 번째 모음
 *   vowel1: string
 *   // 두 번째 모음
 *   vowel2: string
 * ): string
 * ```
 * @example
 * combineVowels('ㅗ', 'ㅏ') // 'ㅘ'
 * combineVowels('ㅗ', 'ㅐ') // 'ㅙ'
 * combineVowels('ㅗ', 'ㅛ') // 'ㅗㅛ'
 */
type Invert<T extends Record<string, string>> = {
    [K in keyof T as T[K]]: K;
};
declare function combineVowels<V1 extends string, V2 extends string>(vowel1: V1, vowel2: V2): `${V1}${V2}` extends "ㅏ" | "ㅐ" | "ㅑ" | "ㅒ" | "ㅓ" | "ㅔ" | "ㅕ" | "ㅖ" | "ㅗ" | "ㅗㅏ" | "ㅗㅐ" | "ㅗㅣ" | "ㅛ" | "ㅜ" | "ㅜㅓ" | "ㅜㅔ" | "ㅜㅣ" | "ㅠ" | "ㅡ" | "ㅡㅣ" | "ㅣ" ? Invert<{
    readonly ㅏ: "ㅏ";
    readonly ㅐ: "ㅐ";
    readonly ㅑ: "ㅑ";
    readonly ㅒ: "ㅒ";
    readonly ㅓ: "ㅓ";
    readonly ㅔ: "ㅔ";
    readonly ㅕ: "ㅕ";
    readonly ㅖ: "ㅖ";
    readonly ㅗ: "ㅗ";
    readonly ㅘ: "ㅗㅏ";
    readonly ㅙ: "ㅗㅐ";
    readonly ㅚ: "ㅗㅣ";
    readonly ㅛ: "ㅛ";
    readonly ㅜ: "ㅜ";
    readonly ㅝ: "ㅜㅓ";
    readonly ㅞ: "ㅜㅔ";
    readonly ㅟ: "ㅜㅣ";
    readonly ㅠ: "ㅠ";
    readonly ㅡ: "ㅡ";
    readonly ㅢ: "ㅡㅣ";
    readonly ㅣ: "ㅣ";
}>[`${V1}${V2}`] : `${V1}${V2}`;

/**
 * @name getChoseong
 * @description
 * 단어에서 초성을 추출합니다. (예: `사과` -> `'ㅅㄱ'`)
 * ```typescript
 * getChoseong(
 *   // 초성을 추출할 단어
 *   word: string
 * ): string
 * ```
 * @example
 * getChoseong('사과') // 'ㅅㄱ'
 * getChoseong('띄어 쓰기') // 'ㄸㅇ ㅆㄱ'
 */
declare function getChoseong(word: string): string;

/**
 * @name canBeChoseong
 * @description
 * 인자로 받은 문자가 초성으로 위치할 수 있는 문자인지 검사합니다.
 * ```typescript
 * canBeChoseong(
 *   // 대상 문자
 *   character: string
 * ): boolean
 * ```
 * @example
 * canBeChoseong('ㄱ') // true
 * canBeChoseong('ㅃ') // true
 * canBeChoseong('ㄱㅅ') // false
 * canBeChoseong('ㅏ') // false
 * canBeChoseong('가') // false
 */
declare function canBeChoseong(character: string): character is (typeof CHOSEONGS)[number];

/**
 * @name canBeJungseong
 * @description
 * 인자로 받은 문자가 중성으로 위치할 수 있는 문자인지 검사합니다.
 * ```typescript
 * canBeJungseong(
 *   // 대상 문자
 *   character: string
 * ): boolean
 * ```
 * @example
 * canBeJungseong('ㅏ') // true
 * canBeJungseong('ㅗㅏ') // true
 * canBeJungseong('ㅘ') // true
 * canBeJungseong('ㅏㅗ') // false
 * canBeJungseong('ㄱ') // false
 * canBeJungseong('ㄱㅅ') // false
 * canBeJungseong('가') // false
 */
declare function canBeJungseong(character: string): character is (typeof JUNGSEONGS)[number];

/**
 * @name canBeJongseong
 * @description
 * 인자로 받은 문자가 종성으로 위치할 수 있는 문자인지 검사합니다.
 * ```typescript
 * canBeJongseong(
 *   // 대상 문자
 *   character: string
 * ): boolean
 * ```
 * @example
 * canBeJongseong('ㄱ') // true
 * canBeJongseong('ㄱㅅ') // true
 * canBeJongseong('ㅎㄹ') // false
 * canBeJongseong('가') // false
 * canBeJongseong('ㅏ') // false
 * canBeJongseong('ㅗㅏ') // false
 */
declare function canBeJongseong(character: string): character is (typeof JONGSEONGS)[number];

/**
 * @name removeLastCharacter
 * @description
 * 인자로 주어진 한글 문자열에서 가장 마지막 문자 하나를 제거하여 반환합니다.
 * ```typescript
 * removeLastCharacter(
 *   // 한글 문자열
 *   words: string
 * ): string
 * ```
 * @example
 * removeLastCharacter('안녕하세요 값') // 안녕하세요 갑
 * removeLastCharacter('프론트엔드') // 프론트엔ㄷ
 * removeLastCharacter('일요일') // 일요이
 * removeLastCharacter('전화') // 전호
 * removeLastCharacter('신세계') // 신세ㄱ
 */
declare function removeLastCharacter(words: string): string;

type JosaOption = '이/가' | '을/를' | '은/는' | '으로/로' | '와/과' | '이나/나' | '이란/란' | '아/야' | '이랑/랑' | '이에요/예요' | '으로서/로서' | '으로써/로써' | '으로부터/로부터' | '이라/라';
type ExtractJosaOption<T> = T extends `${infer A}/${infer B}` ? A | B : never;
declare function josa<T extends string, U extends JosaOption>(word: T, josa: U): `${T}${ExtractJosaOption<U>}`;
declare namespace josa {
    var pick: typeof josaPicker;
}
declare function josaPicker<T extends JosaOption>(word: string, josa: T): ExtractJosaOption<T>;

/**
 * @name hasBatchim
 * @description
 * 한글 문자열의 마지막 글자가 받침이 있는지 확인합니다.
 * ```typescript
 * hasBatchim(
 *   // 글자에 받침이 있는지 확인하고 싶은 문자열
 *   str: string,
 *   // 옵션 객체로 only 속성을 받을 수 있습니다.
 *   options?: { only?: "single" | "double" }
 * ): boolean
 * ```
 * @example
 * hasBatchim('값') // true
 * hasBatchim('토') // false
 * hasBatchim('갑', { only: "single" }) // true
 * hasBatchim('값', { only: "single" }) // false
 * hasBatchim('값', { only: "double" }) // true
 * hasBatchim('토', { only: "double" }) // false
 */
declare function hasBatchim(str: string, options?: {
    /**
     * 체크할 받침의 종류
     * 사용하지 않으면 둘다 체크합니다.
     */
    only?: 'single' | 'double';
}): boolean;

type Options = {
    hardConversion: boolean;
};
/**
 * 주어진 한글 문자열을 표준 발음으로 변환합니다.
 * @param hangul 한글 문자열을 입력합니다.
 * @param options 변환 옵션을 설정합니다.
 * @param options.hardConversion 경음화 등의 된소리를 적용할지 여부를 설정합니다. 기본값은 true입니다.
 * @returns 변환된 표준 발음 문자열을 반환합니다.
 */
declare function standardizePronunciation(hangul: string, options?: Options): string;

/**
 * 주어진 한글 문자열을 로마자로 변환합니다.
 * @param hangul 한글 문자열을 입력합니다.
 * @returns 변환된 로마자를 반환합니다.
 */
declare function romanize(hangul: string): string;

declare function numberToHangul(input: number, options?: {
    spacing?: boolean;
}): string;

declare function numberToHangulMixed(input: number, options?: {
    spacing?: boolean;
}): string;

/**
 * @deprecated 더 유연하게 사용 가능한 `numberToHangul`을 이용해 주세요
 */
declare function amountToHangul(amount: string | number): string;

/**
 * 숫자를 순 우리말 수사로 변환합니다. 주어진 숫자가 0보다 크고 100 이하일 때 유효합니다.
 *
 * @remarks
 * - **수사**란 숫자를 나타내는 우리말 단어입니다. [자세히 알아보기](https://ko.dict.naver.com/#/entry/koko/d0ce2b674cae4b44b9028f648dd458b0)
 * - **수관형사**는 사물의 수나 양을 나타내는 관형사입니다. '두 사람'의 '두', '세 근'의 '세' 따위를 뜻 합니다. [자세히 알아보기](https://ko.dict.naver.com/#/entry/koko/c513782b82554ff499c80ec616c5b611)
 *
 * @param num 숫자를 입력합니다.
 * @param classifier 수관형사를 사용할지 여부를 입력합니다. 기본값은 false입니다.
 * @returns 변환된 수사를 반환합니다.
 *
 * @example
 * susa(1); // '하나'
 * susa(2); // '둘'
 * susa(11); // '열하나'
 * susa(21); // '스물하나'
 * susa(99); // '아흔아홉'
 * susa(100); // '백'
 * susa(1, true); // '한'
 * susa(2, true); // '두'
 * susa(11, true); // '열한'
 * susa(20, true); // '스무'
 * susa(21, true); // '스물한'
 *
 * @see https://es-hangul.slash.page/docs/api/susa
 */
declare function susa(num: number, classifier?: boolean): string;

/**
 * 숫자를 한글 서수사로 변환합니다.
 *
 * @remarks
 * - **서수사**는 순서를 나타내는 단어입니다.
 * - 1부터 99까지의 정수는 순우리말 서수사 문자열로 변환합니다.
 * - 100 이상의 정수는 한자어 서수사 문자열로 변환합니다.
 *
 * @param num - 변환할 숫자
 * @return 변환된 서수사 문자열
 * @throws {Error} 지원하지 않는 숫자인 경우
 *
 * @example
 * seosusa(1);  // '첫째'
 * seosusa(2);  // '둘째'
 * seosusa(3);  // '셋째'
 * seosusa(10); // '열째'
 * seosusa(11); // '열한째'
 * seosusa(12); // '열두째'
 * seosusa(13); // '열셋째'
 * seosusa(20); // '스무째'
 * seosusa(21); // '스물한째'
 * seosusa(30); // '서른째'
 * seosusa(40); // '마흔째'
 * seosusa(99); // '아흔아홉째'
 * seosusa(100); // '백째'
 *
 * @see https://es-hangul.slash.page/docs/api/seosusa
 */
declare function seosusa(num: number): string;

declare function days(num: number): string;

/**
 * @name convertQwertyToHangul
 * @description
 * 영어 알파벳을 qwerty 자판과 매칭과는 한글 문자와 문장으로 변환합니다.
 * @param word 한글 문장으로 변환하고자 하는 영문
 * @returns qwerty 영어 알파벳을 변환하여 한글 규칙에 맞게 합성한 문자열
 */
declare function convertQwertyToHangul(word: string): string;

/**
 * @name convertQwertyToAlphabet
 * @description
 * 영어 알파벳을 qwerty 자판과 매칭되는 한글 음소로 변환합니다.
 * @param word 한글 음소로 변환하고자 하는 영문
 * @returns 영어 알파벳이 포함되지 않은 한글 음소, 음절, 기타 문자로 이루어진 문자열
 */
declare function convertQwertyToAlphabet(word: string): string;

/**
 * @name convertHangulToQwerty
 * @description
 * 한글을 qwerty 자판과 매칭되는 영어 알파벳으로 변환합니다.
 * @param word 영문으로 변환하고자 하는 한글
 * @returns 한글을 규칙에 맞게 qwerty 자판의 영어 알파벳으로 변환한 문자열
 * @example
 * convertHangulToQwerty('겨노'); // 'rush'
 * convertHangulToQwerty('쨰ㅉ'); // 'WOW'
 * convertHangulToQwerty('FE개발!'); // 'FEroqkf!'
 * convertHangulToQwerty('ㅇPdml'); // 'dPdml'
 * convertHangulToQwerty(''); // ''
 */
declare function convertHangulToQwerty(word: string): string;

export { amountToHangul, assemble, canBeChoseong, canBeJongseong, canBeJungseong, combineCharacter, combineVowels, convertHangulToQwerty, convertQwertyToAlphabet, convertQwertyToHangul, days, disassemble, disassembleCompleteCharacter, disassembleToGroups, getChoseong, hasBatchim, josa, numberToHangul, numberToHangulMixed, removeLastCharacter, romanize, seosusa, standardizePronunciation, susa };
