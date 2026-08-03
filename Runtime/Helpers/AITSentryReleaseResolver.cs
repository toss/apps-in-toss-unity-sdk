// -----------------------------------------------------------------------
// <copyright file="AITSentryReleaseResolver.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Sentry environment/release 파생 로직
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

// 빌드 전처리기(AppsInToss.Sentry.Editor)와 EditMode 테스트가 internal 파생 로직에 접근합니다.
[assembly: InternalsVisibleTo("AppsInToss.Sentry.Editor")]
[assembly: InternalsVisibleTo("AppsInTossEditModeTests")]

namespace AppsInToss
{
    /// <summary>
    /// SDK 버전 문자열로부터 Sentry environment / release 식별자를 파생하는 순수 로직.
    /// 문자열 연산만 수행하므로 Sentry 패키지에 의존하지 않으며, Sentry 미설치 환경에서도 단위 테스트가 가능합니다.
    /// </summary>
    /// <remarks>
    /// 사용처: <c>AITSentryDsnInjector</c>가 WebGL 빌드 시 <c>SentryOptions.asset</c>에 bake 합니다.
    /// Sentry의 environment/release는 init-time(BeforeSceneLoad) 전용 옵션이라 런타임 scope로 변경할 수 없으므로,
    /// 빌드 시점에 결정해 asset으로 구워야 합니다. prerelease SDK(베타 파일럿) 빌드의 에러가 stable triage를
    /// 오염시키지 않도록 environment를 분리하는 것이 목적입니다.
    /// </remarks>
    internal static class AITSentryReleaseResolver
    {
        /// <summary>prerelease 빌드에 부여되는 Sentry environment 이름.</summary>
        internal const string BetaEnvironment = "beta";

        /// <summary>release 식별자 prefix. release.yml의 getsentry/action-release 컨벤션(apps-in-toss.unity@X.Y.Z)과 동일.</summary>
        internal const string ReleasePrefix = "apps-in-toss.unity@";

        // AITVersion.Version이 패키지 정보를 찾지 못했을 때 반환하는 폴백 값.
        private const string UnknownVersion = "unknown";

        /// <summary>
        /// 버전 문자열이 semver prerelease(예: "3.0.0-beta.9d42c0b", "3.0.0-rc.1")인지 판정합니다.
        /// </summary>
        /// <remarks>
        /// SemVer 2.0 규칙을 따릅니다:
        ///   - build metadata("+..." 뒤)는 판정에서 제외 (예: "2.6.1+build-20240101"은 stable).
        ///   - '-' 뒤에 비어 있지 않은 prerelease 식별자가 있어야 prerelease (예: "3.0.0-"은 stable로 취급).
        /// </remarks>
        internal static bool IsPrerelease(string sdkVersion)
        {
            if (string.IsNullOrWhiteSpace(sdkVersion) || sdkVersion == UnknownVersion)
            {
                return false;
            }

            // build metadata("+...")는 prerelease 판정에서 제외하고 core 부분만 검사한다.
            string core = sdkVersion.Split('+')[0];
            int dash = core.IndexOf('-');
            return dash >= 0 && dash < core.Length - 1;
        }

        /// <summary>
        /// Sentry environment를 결정합니다.
        /// 명시된 <paramref name="envOverride"/>가 있으면 그 값을 우선하고, 없으면 prerelease 빌드일 때만 "beta"를 반환합니다.
        /// stable 빌드는 <c>null</c>을 반환하여 environment를 설정하지 않으므로 Sentry 기본값("production")을 그대로 사용합니다(동작 불변).
        /// </summary>
        /// <param name="sdkVersion">SDK 버전 문자열(<c>AITVersion.Version</c>).</param>
        /// <param name="envOverride">명시적 환경변수(<c>SENTRY_ENVIRONMENT</c>). 비어 있으면 무시됩니다.</param>
        /// <returns>설정할 environment 문자열. <c>null</c>이면 override 하지 않음.</returns>
        internal static string ResolveEnvironment(string sdkVersion, string envOverride)
        {
            if (!string.IsNullOrEmpty(envOverride))
            {
                return envOverride;
            }

            return IsPrerelease(sdkVersion) ? BetaEnvironment : null;
        }

        /// <summary>
        /// Sentry release를 결정합니다.
        /// 명시된 <paramref name="releaseOverride"/>가 있으면 그 값을 우선하고, 없으면 버전을 알 때 "apps-in-toss.unity@{버전}"을 반환합니다.
        /// 이는 release.yml이 생성하는 Sentry release 식별자와 정합하여 release-health/auto-resolve 연결을 정확하게 합니다.
        /// </summary>
        /// <param name="sdkVersion">SDK 버전 문자열(<c>AITVersion.Version</c>).</param>
        /// <param name="releaseOverride">명시적 환경변수(<c>SENTRY_RELEASE</c>). 비어 있으면 무시됩니다.</param>
        /// <returns>설정할 release 문자열. <c>null</c>이면 override 하지 않음(Sentry 기본값 Application.version 사용).</returns>
        internal static string ResolveRelease(string sdkVersion, string releaseOverride)
        {
            if (!string.IsNullOrEmpty(releaseOverride))
            {
                return releaseOverride;
            }

            if (string.IsNullOrWhiteSpace(sdkVersion) || sdkVersion == UnknownVersion)
            {
                return null;
            }

            return ReleasePrefix + sdkVersion;
        }
    }
}
