import { Primitive } from '@apps-in-toss/web-bridge';

type LoggerParams = {
    log_name?: string;
} & {
    [key: string]: Primitive;
};
declare const Analytics: {
    screen: (params?: LoggerParams) => Promise<void> | undefined;
    impression: (params?: LoggerParams) => Promise<void> | undefined;
    click: (params?: LoggerParams) => Promise<void> | undefined;
};

export { Analytics };
