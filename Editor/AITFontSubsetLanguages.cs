// -----------------------------------------------------------------------
// <copyright file="AITFontSubsetLanguages.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font subset 동적 텍스트 언어 → 유니코드 범위 매핑
// </copyright>
// -----------------------------------------------------------------------
//
// 서버발 동적 텍스트(닉네임·채팅 등)에 등장할 수 있는 언어를 개발자가 명시 선택하면(fontSubsetLanguages),
// 그 언어의 유니코드 범위를 subset 보존 범위에 union 한다. 선택 = 인지된 활성화: 자동 모드에서
// 아무 언어도 선택하지 않으면 "동적 텍스트 리스크를 인지하지 못한 것"으로 간주해 subset 자체를
// 건너뛴다(AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection).
//
// Ranges 표기는 subset-font-runner.mjs / AITFontUsedCharScanner.FormatRanges 와 동일한
// "U+XXXX-YYYY,U+XXXX" 콤마 구분 fontTools 표기다.
//
// ko(한국어)/la(영어·라틴)는 AlwaysIncluded=true — AITFontUsedCharScanner.BaselineRanges 가
// 이미 항상 보존하므로(스캔 여부 무관), 여기 Ranges 는 UI 표시/참고용일 뿐 BuildRanges 결과에는
// 포함시키지 않는다(중복 union 방지).

using System.Collections.Generic;
using System.Text;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 동적 텍스트 언어 태그 ↔ 유니코드 보존 범위 매핑을 제공하는 순수 정적 헬퍼.
    /// 부수 효과가 없어 EditMode 단위 테스트에서 직접 검증한다.
    /// </summary>
    public static class AITFontSubsetLanguages
    {
        /// <summary>언어 선택 항목 하나(테이블 행).</summary>
        public readonly struct Entry
        {
            /// <summary>언어 태그(fontSubsetLanguages CSV 에 쓰이는 식별자). 예) "ja".</summary>
            public readonly string Tag;

            /// <summary>Configuration 창에 표시할 한국어 라벨.</summary>
            public readonly string Label;

            /// <summary>보존할 유니코드 범위(fontTools 표기, 콤마 구분). AlwaysIncluded 항목은 참고용.</summary>
            public readonly string Ranges;

            /// <summary>true 면 스캐너 BaselineRanges 가 이미 항상 보존하는 언어(체크 고정 표시용).</summary>
            public readonly bool AlwaysIncluded;

            public Entry(string tag, string label, string ranges, bool alwaysIncluded)
            {
                Tag = tag;
                Label = label;
                Ranges = ranges;
                AlwaysIncluded = alwaysIncluded;
            }
        }

        /// <summary>
        /// 지원 언어 테이블. 순서가 fontSubsetLanguages CSV 직렬화 순서를 결정한다(결정적 직렬화).
        /// </summary>
        public static readonly Entry[] Table =
        {
            new Entry("ko", "한국어", "U+AC00-D7A3,U+1100-11FF,U+3130-318F", true),
            new Entry("la", "영어/기본 라틴", "U+0020-007E,U+00A0-00FF", true),
            new Entry("ja", "일본어", "U+3000-303F,U+3040-309F,U+30A0-30FF,U+31F0-31FF,U+4E00-9FFF,U+FF00-FFEF", false),
            new Entry("zh-Hans", "중국어(간체)", "U+3000-303F,U+3400-4DBF,U+4E00-9FFF,U+FF00-FFEF", false),
            new Entry("zh-Hant", "중국어(번체)", "U+3000-303F,U+3100-312F,U+31A0-31BF,U+3400-4DBF,U+4E00-9FFF,U+FF00-FFEF", false),
            new Entry("ru", "러시아어/키릴", "U+0400-04FF,U+0500-052F", false),
            new Entry("th", "태국어", "U+0E00-0E7F", false),
            new Entry("latin-ext", "라틴 확장 (유럽어·베트남어)", "U+0100-024F,U+1E00-1EFF,U+20AB", false),
            new Entry("ar", "아랍어", "U+0600-06FF,U+0750-077F,U+200C,U+FB50-FDFF,U+FE70-FEFF", false),
            new Entry("emoji", "이모지·기호", "U+200D,U+20E3,U+2122,U+2190-21FF,U+2300-23FF,U+25A0-25FF,U+2600-26FF,U+2700-27BF,U+2B00-2BFF,U+FE0E-FE0F,U+1F170-1F1E5,U+1F1E6-1F1FF,U+1F300-1F5FF,U+1F600-1F64F,U+1F680-1F6FF,U+1F780-1F7FF,U+1F900-1FAFF", false),
        };

        /// <summary>
        /// 콤마 구분 언어 태그 CSV 를 받아, AlwaysIncluded 가 아닌 태그들의 Ranges 를 union 한
        /// fontTools 표기 범위 문자열을 만든다. AlwaysIncluded 태그(ko/la)와 미지 태그는 무시(skip)한다.
        /// 태그 중복뿐 아니라 범위 토큰 자체의 중복도 제거한다(예: ja+zh-Hans 동시 선택 시
        /// 두 태그가 공유하는 U+4E00-9FFF 는 결과에 1회만 출력). 부수 효과 없음 → 단위 테스트 대상.
        /// </summary>
        /// <param name="csvTags">쉼표 구분 언어 태그(예: "ja,zh-Hans"). 중복 허용. null/빈 문자열 허용.</param>
        /// <returns>선택된 언어들의 Ranges 를 콤마로 결합한 문자열(토큰 중복 제거). 선택 없으면 빈 문자열.</returns>
        public static string BuildRanges(string csvTags)
        {
            if (string.IsNullOrEmpty(csvTags))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var seenTags = new HashSet<string>();
            var seenTokens = new HashSet<string>();
            foreach (var raw in csvTags.Split(','))
            {
                string tag = raw.Trim();
                if (tag.Length == 0 || !seenTags.Add(tag))
                {
                    continue;
                }

                Entry entry = FindEntry(tag);
                if (entry.Tag == null || entry.AlwaysIncluded || entry.Ranges.Length == 0)
                {
                    continue;
                }

                foreach (var rawToken in entry.Ranges.Split(','))
                {
                    string token = rawToken.Trim();
                    if (token.Length == 0 || !seenTokens.Add(token))
                    {
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(token);
                }
            }

            return sb.ToString();
        }

        private static Entry FindEntry(string tag)
        {
            foreach (var entry in Table)
            {
                if (entry.Tag == tag)
                {
                    return entry;
                }
            }

            return default;
        }
    }
}
