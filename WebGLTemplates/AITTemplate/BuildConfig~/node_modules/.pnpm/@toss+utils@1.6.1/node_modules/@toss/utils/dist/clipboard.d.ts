declare function writeText(text: string): Promise<boolean>;
export declare const clipboard: {
    writeText: typeof writeText;
};
export {};
