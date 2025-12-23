interface Menu {
    key: string;
    description: string;
    action: () => void;
}
export declare class StartMenuHandler {
    private menus;
    constructor(menus: Menu[]);
    attach(): this;
    close(): void;
    private keyPressHandler;
}
export {};
