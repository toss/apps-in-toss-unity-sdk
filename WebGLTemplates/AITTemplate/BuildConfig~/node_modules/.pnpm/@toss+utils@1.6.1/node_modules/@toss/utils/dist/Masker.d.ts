declare function maskName(name: string): string;
declare function maskPhoneNumber(phoneNumber: string): string;
export declare const Masker: {
    maskName: typeof maskName;
    maskPhoneNumber: typeof maskPhoneNumber;
};
export {};
