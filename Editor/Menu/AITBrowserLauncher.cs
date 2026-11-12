namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// Dev Server 브라우저 경로 해석 및 Vite 포트 대기 후 브라우저 열기 유틸리티.
    /// (Production Server 제거로 단일 서버 구조 — 경로 분기 없음)
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITBrowserLauncher
    {
        /// <summary>Dev Server 브라우저 경로.</summary>
        internal const string BrowserPath = "/index.html";

        /// <summary>
        /// Vite 포트가 열릴 때까지 대기한 후 Dev Server 경로로 브라우저를 엽니다.
        /// </summary>
        internal static void OpenBrowser(int vitePort)
        {
            PortResolver.WaitForPortAndOpenBrowser(vitePort, $"http://localhost:{vitePort}{BrowserPath}");
        }
    }
}
