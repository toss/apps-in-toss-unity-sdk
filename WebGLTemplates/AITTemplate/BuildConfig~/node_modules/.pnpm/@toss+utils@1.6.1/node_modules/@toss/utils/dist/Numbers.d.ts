export declare const ceilToUnit: (value: number, unit: number) => number;
export declare const floorToUnit: (value: number, unit: number) => number;
export declare const roundToUnit: (value: number, unit: number) => number;
export declare function formatToKoreanNumber(value: number, options?: {
    floorUnit?: number;
    ceilUnit?: number;
    formatAllDigits?: boolean;
}): string;
export declare function formatToKRW(value: number, options?: {
    shouldHaveSpaceBeforeWon?: boolean;
    floorUnit?: number;
    ceilUnit?: number;
    formatAllDigits?: boolean;
}): string;
export declare function commaizeNumber(value: string | number): string;
export declare function floorAndFormatNumber(value: number): string;
export declare function decommaizeNumber(numStr: string): number;
export declare function formatPhoneNumber(phoneNumber: string): string;
export declare function formatBusinessRegistrationNumber(businessRegistrationNumber: string): string;
