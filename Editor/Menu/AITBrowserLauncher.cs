using AppsInToss.Editor;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 서버 타입별 브라우저 경로 해석 및 Vite 포트 대기 후 브라우저 열기 유틸리티.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITBrowserLauncher
    {
        /// <summary>
        /// 서버 타입별 브라우저 경로 반환.
        /// Dev는 /index.html, Prod는 /
        /// </summary>
        internal static string GetBrowserPath(ServerType type) =>
            type == ServerType.Dev ? "/index.html" : "/";

        /// <summary>
        /// Vite 포트가 열릴 때까지 대기한 후 해당 서버 타입의 경로로 브라우저를 엽니다.
        /// </summary>
        internal static void OpenBrowser(int vitePort, ServerType type)
        {
            PortResolver.WaitForPortAndOpenBrowser(vitePort, $"http://localhost:{vitePort}{GetBrowserPath(type)}");
        }
    }
}
