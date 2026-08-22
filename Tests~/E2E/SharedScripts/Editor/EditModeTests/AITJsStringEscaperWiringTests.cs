// -----------------------------------------------------------------------
// AITJsStringEscaperWiringTests.cs
// "템플릿에서 작은따옴표 문자열 리터럴 자리에 있는 %AIT_*% 토큰은 예외 없이
//  AITJsStringEscaper.EscapeSingleQuoted 를 거쳐 치환된다"는 계약의 소스 텍스트 가드.
//
// 이 계약이 깨지면 사용자가 표시 이름에 아포스트로피 하나만 넣어도
// (예: "Dave's Adventure") index.html 의 인라인 <script> 블록이나 granite.config.ts 가
// 문법 오류로 죽는다. UI 는 이 필드들에 아무 문자 제한을 두지 않고,
// AITBuildValidator.FindUnsubstitutedPlaceholders 는 미치환 토큰 잔존만 검사하므로
// (치환은 성공하고 문법만 깨지는) 이 케이스를 아무도 잡아주지 않는다.
//
// 대상 토큰 목록을 손으로 적지 않는 것이 핵심이다 — 템플릿을 파싱해 따옴표 자리인지
// 코드 자리인지 ★유도★하고, 그 결과로 C# 치환부를 대조한다. 그래서 템플릿에 새 토큰이
// 추가되거나 기존 토큰이 따옴표 안팎으로 옮겨가면 이 테스트가 자동으로 따라간다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITJsStringEscaperWiringTests
{
    private const string EscaperCall = "AITJsStringEscaper.EscapeSingleQuoted";

    private static readonly Regex AnyToken = new Regex(@"%AIT_[A-Z0-9_]+%");
    private static readonly Regex QuotedToken = new Regex(@"'(%AIT_[A-Z0-9_]+%)'");

    // 치환을 담당하는 C# 소스와, 그 소스가 처리하는 템플릿들.
    private sealed class SubstitutionGroup
    {
        public string SourcePath;
        public string[] TemplatePaths;
    }

    private static readonly SubstitutionGroup[] Groups =
    {
        new SubstitutionGroup
        {
            SourcePath = "Editor/Package/WebGLBuildCopier.cs",
            TemplatePaths = new[] { "WebGLTemplates/AITTemplate/index.html" },
        },
        new SubstitutionGroup
        {
            SourcePath = "Editor/Package/BuildConfigMerger.cs",
            TemplatePaths = new[]
            {
                "WebGLTemplates/AITTemplate/BuildConfig~/vite.config.ts",
                "WebGLTemplates/AITTemplate/BuildConfig~/granite.config.ts",
                "WebGLTemplates/AITTemplate/BuildConfig~/apps-in-toss.config.ts",
            },
        },
    };

    [Test]
    public void QuotedPlaceholders_AreEscapedAtEverySubstitutionSite()
    {
        foreach (SubstitutionGroup group in Groups)
        {
            string source = ReadRepoFile(group.SourcePath);
            CollectTokens(group, out HashSet<string> quoted, out HashSet<string> codeContext);

            Assert.IsNotEmpty(quoted,
                $"{group.SourcePath} 가 담당하는 템플릿에서 따옴표 자리 토큰을 하나도 찾지 못했다 — " +
                "템플릿 경로나 토큰 표기가 바뀌었는지 확인하라(가드가 조용히 무의미해지는 것 방지).");

            foreach (string token in quoted.OrderBy(t => t))
            {
                List<string> sites = FindSubstitutionSites(source, token);

                Assert.IsNotEmpty(sites,
                    $"템플릿에는 {token} 이 있는데 {group.SourcePath} 에서 치환부를 찾지 못했다. " +
                    "치환이 다른 파일로 옮겨갔거나 .Replace 호출이 여러 줄로 쪼개졌을 수 있다 — " +
                    "이 가드가 대조할 수 있도록 Groups 매핑이나 코드 서식을 맞춰라.");

                foreach (string site in sites)
                {
                    StringAssert.Contains(EscaperCall, site,
                        $"{token} 은 템플릿에서 작은따옴표 문자열 리터럴 자리에 놓이므로 " +
                        $"{EscaperCall} 을 거쳐 치환해야 한다.\n  치환부: {site.Trim()}\n" +
                        "값에 작은따옴표·개행·\"</script>\"가 들어가면 산출물이 통째로 문법 오류가 된다. " +
                        "값이 상수라 실질 no-op 인 경우에도 규칙을 예외 없이 유지한다 — " +
                        "호출부마다 안전 여부를 판단하기 시작하면 다음 토큰에서 빠뜨리게 된다.");
                }
            }

            // 같은 토큰이 한 템플릿에선 따옴표 자리, 다른 템플릿에선 코드 자리인 경우
            // 치환부 단위로는 옳게 대조할 수 없다. 현재 그런 토큰은 없으며, 생기면 여기서 알린다.
            var ambiguous = quoted.Intersect(codeContext).OrderBy(t => t).ToArray();
            CollectionAssert.IsEmpty(ambiguous,
                $"{group.SourcePath} 가 담당하는 템플릿들에서 다음 토큰이 따옴표 자리와 코드 자리에 " +
                $"동시에 쓰인다: {string.Join(", ", ambiguous)}. " +
                "이 경우 치환부 한 줄로는 어느 쪽에 맞춰야 할지 결정할 수 없으므로, " +
                "토큰을 분리하거나 템플릿 표기를 통일하라.");
        }
    }

    [Test]
    public void CodeContextPlaceholders_AreNotEscaped()
    {
        foreach (SubstitutionGroup group in Groups)
        {
            string source = ReadRepoFile(group.SourcePath);
            CollectTokens(group, out HashSet<string> quoted, out HashSet<string> codeContext);

            foreach (string token in codeContext.Except(quoted).OrderBy(t => t))
            {
                foreach (string site in FindSubstitutionSites(source, token))
                {
                    StringAssert.DoesNotContain(EscaperCall, site,
                        $"{token} 은 값이 그대로 JS/TS 코드로 전개되는 자리다(JSON·불리언·숫자). " +
                        $"{EscaperCall} 을 적용하면 따옴표·백슬래시가 이스케이프되어 산출물이 깨진다.\n" +
                        $"  치환부: {site.Trim()}");
                }
            }
        }
    }

    private static void CollectTokens(
        SubstitutionGroup group,
        out HashSet<string> quoted,
        out HashSet<string> codeContext)
    {
        quoted = new HashSet<string>();
        codeContext = new HashSet<string>();

        foreach (string templatePath in group.TemplatePaths)
        {
            string template = ReadRepoFile(templatePath);

            var quotedHere = new HashSet<string>();
            foreach (Match m in QuotedToken.Matches(template))
            {
                quotedHere.Add(m.Groups[1].Value);
            }

            foreach (Match m in AnyToken.Matches(template))
            {
                if (!quotedHere.Contains(m.Value))
                {
                    codeContext.Add(m.Value);
                }
            }

            quoted.UnionWith(quotedHere);
        }
    }

    // .Replace("<token>", ...) 가 등장하는 소스 줄 전체를 돌려준다.
    // 이 저장소의 치환 체인은 한 줄에 하나씩 쓰여 있다(서식이 바뀌면 위의 IsNotEmpty 가 알린다).
    private static List<string> FindSubstitutionSites(string source, string token)
    {
        string needle = "Replace(\"" + token + "\"";
        return source
            .Split('\n')
            .Where(line => line.Contains(needle))
            .ToList();
    }

    private static string ReadRepoFile(string relativePath)
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(relativePath, out string path, typeof(AITConvertCore)),
            $"{relativePath} 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }
}
