// -----------------------------------------------------------------------
// AITJsStringEscaper.cs - 작은따옴표 JS/TS 문자열 리터럴 이스케이프
//
// 빌드 산출물(index.html / granite.config.ts / apps-in-toss.config.ts)의 플레이스홀더
// 치환은 전부 단순 문자열 Replace 다. 템플릿에서 토큰이 이미 작은따옴표 안에 놓여 있는 자리
// (예: displayName: '%AIT_DISPLAY_NAME%')에 사용자 입력값을 그대로 끼워 넣으면, 값에 들어 있는
// 작은따옴표 하나가 리터럴을 조기 종료시켜 그 파일 전체를 문법 오류로 만든다.
//
// 실제 노출 경로: AIT Configuration 창의 "표시 이름"·"주 색상"·"아이콘 URL"·"Vite 호스트"·
// "출력 디렉토리"는 아무 문자 제한 없는 TextField 다(appName 만 IsAppNameValid 로 영숫자+하이픈
// 검증을 받는다). 그래서 "Dave's Adventure" 같은 흔한 이름이 저장→빌드까지 그대로 흘러가
// index.html 의 인라인 <script> 블록과 granite.config.ts 를 동시에 깨뜨린다.
// AITBuildValidator.FindUnsubstitutedPlaceholders 는 미치환 %AIT_*% 잔존만 검사하므로
// (치환 자체는 성공했고 문법만 깨진) 이 케이스를 통과시켜 빌드 시점에 잡히지 않는다.
// -----------------------------------------------------------------------

using System.Text;

namespace AppsInToss.Editor.Package
{
    internal static class AITJsStringEscaper
    {
        /// <summary>
        /// 값을 <b>작은따옴표 JS/TS 문자열 리터럴 안쪽</b>에 안전하게 넣을 수 있도록 이스케이프한다.
        /// 감싸는 따옴표는 템플릿에 이미 있으므로 이 메서드는 따옴표를 붙이지 않는다.
        ///
        /// ⚠️ 문자열 리터럴 자리에만 쓸 것. %AIT_PERMISSIONS% / %AIT_NAVIGATION_BAR% 처럼 값 자체가
        /// JSON·불리언 <b>코드</b>로 전개되는 토큰에 적용하면 오히려 산출물이 깨진다.
        /// </summary>
        internal static string EscapeSingleQuoted(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    // 백슬래시가 가장 먼저다 — 뒤이어 삽입할 이스케이프 시퀀스가 다시 먹히면 안 된다.
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;

                    // 줄바꿈은 리터럴을 그 자리에서 끝내 버린다(개행이 들어간 값도 실제로 가능하다).
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;

                    // '<' 를 막으면 값 안의 "</script>" 가 index.html 의 인라인 <script> 블록을 조기
                    // 종료하는 것과, "<!--" 가 레거시 HTML 주석을 여는 것을 함께 차단한다.
                    // \x3C 는 표준 JS 이스케이프라 TS 설정 파일에서도 그대로 '<' 로 평가된다.
                    case '<': sb.Append("\\x3C"); break;

                    // U+2028/U+2029 는 ES2019 이전 파서가 줄바꿈으로 취급해 문법 오류를 낸다.
                    // (esbuild/vite 가 어떤 target 으로 파싱하든 안전하도록 항상 이스케이프한다.)
                    case '\u2028': sb.Append("\\u2028"); break;
                    case '\u2029': sb.Append("\\u2029"); break;

                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }
    }
}
