import type { AliasConfig } from '@granite-js/plugin-core';
import type { JscConfig } from '@swc/core';
/**
 * `@swc/helpers`를 사용하여 번들 크기를 최적화 하기 위한 구성입니다.
 *
 * swc 옵션과 플러그인에서 처리할 로직은 상호 의존적이기 때문에 한 곳에 모아둡니다.
 */
export declare const swcHelperOptimizationRules: {
    jsc: JscConfig;
    getAliasConfig: () => AliasConfig;
};
