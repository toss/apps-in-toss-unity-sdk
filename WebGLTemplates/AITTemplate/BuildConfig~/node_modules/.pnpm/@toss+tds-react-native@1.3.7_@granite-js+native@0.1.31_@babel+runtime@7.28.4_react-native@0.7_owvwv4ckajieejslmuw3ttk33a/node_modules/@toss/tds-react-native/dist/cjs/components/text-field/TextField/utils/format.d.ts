import type { TextFieldValue } from '../types';
export declare const format: {
    phoneNumber: {
        transform: typeof transformToPhoneNumber;
        reset: typeof resetFromPhoneNumber;
    };
    price: {
        transform: typeof transformToPrice;
        reset: typeof resetFromPrice;
    };
};
declare function resetFromPhoneNumber(value: TextFieldValue): string;
declare function transformToPhoneNumber(value: TextFieldValue): string;
declare function resetFromPrice(value: TextFieldValue): string;
declare function transformToPrice(value: TextFieldValue): string;
export {};
