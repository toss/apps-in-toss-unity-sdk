import { Plugin } from 'esbuild';
/**
 * 파일에 require.context('(.*)')라고 하는 글자가 있으면,
 * require('require-context:$1') 로 바꾼다.
 */
export declare function requireContextPlugin(): Plugin;
