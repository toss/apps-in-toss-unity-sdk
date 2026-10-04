// -----------------------------------------------------------------------
// AITDataRawSize.cs - .data 파일의 압축 해제 후 크기 측정
//
// 현재는 -1 을 돌려주는 스텁이다. 실제 측정(빌드 시 내장 Node 로 brotli/gzip 을 풀어 길이를 잼)은
// 후속 배치가 채운다. 결과는 AITPerfFlags 가 window.__AIT_PERF.dataRawSize 로 주입하고,
// Runtime/ait-databuf.js 가 data Response 의 Content-Length 로 쓴다.
// -----------------------------------------------------------------------

namespace AppsInToss.Editor
{
    internal static class AITDataRawSize
    {
        /// <summary>
        /// .data 파일(.br/.gz/무압축)의 압축 해제 후 바이트 수를 잰다.
        /// </summary>
        /// <param name="dataPath">.data 파일의 전체 경로.</param>
        /// <returns>압축 해제 크기(바이트). 측정할 수 없으면 -1(그러면 런타임 훅은 종전 동작으로 돌아간다). 예외를 던지지 않는다.</returns>
        internal static long Measure(string dataPath)
        {
            return -1;
        }
    }
}
