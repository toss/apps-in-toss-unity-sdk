declare module './createRoute' {
    interface RegisterScreenInput {
        '/test': {
            id: string;
            name: string;
        };
        '/test-schema': {
            id: string;
            count: number;
        };
        '/test-transform': {
            id: string;
        };
        '/test-with-defaults': {
            animation?: boolean;
        };
    }
    interface RegisterScreen {
        '/test': {
            id: string;
            name: string;
        };
        '/test-schema': {
            id: string;
            count: number;
        };
        '/test-transform': {
            id: number;
        };
        '/test-with-defaults': {
            animation: boolean;
        };
    }
}
export {};
