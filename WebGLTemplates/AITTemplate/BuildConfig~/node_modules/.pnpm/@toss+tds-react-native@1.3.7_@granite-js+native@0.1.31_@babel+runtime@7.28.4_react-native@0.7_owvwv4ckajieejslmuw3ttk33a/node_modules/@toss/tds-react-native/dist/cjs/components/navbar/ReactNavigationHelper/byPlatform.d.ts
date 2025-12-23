export declare function byPlatform<IOSResult, AndroidResult, MacOSResult, WindowsResult, WebResult, DefaultResult>({ ...props }: {
    ios?: () => IOSResult;
    android?: () => AndroidResult;
    macos?: () => MacOSResult;
    windows?: () => WindowsResult;
    web?: () => WebResult;
    fallback: () => DefaultResult;
}): IOSResult | AndroidResult | MacOSResult | WindowsResult | WebResult | DefaultResult;
