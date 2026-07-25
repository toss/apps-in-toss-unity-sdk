// -----------------------------------------------------------------------
// AITBrotliRecompressTests.cs - brotli .br q11 in-place 재인코딩(스파이크) 검증
//
// 대상: AITBrotliCompressor.RecompressBrFilesInPlace (실제 재인코딩 + 안전 가드 + 원자적 교체)
//       WebGLBuildCopier.EffectiveBrotliRecompress (플래그/환경변수 게이트)
//
// 배경: Unity 내장 brotli 는 저품질(~q5)이라 빌드 산출물 .br 을 디코드→q11 재인코딩하면
//  data/wasm 이 더 작아진다. 이 훅은 buildSrc 의 .br 을 in-place 로 갱신하되,
//  (a) 재인코딩이 원본 이상이면 원본 유지, (b) 디코드 실패 시 원본 유지, (c) 재디코드 검증
//  실패 시 원본 유지, .unityweb(Unity 감지 마커) 및 비-.br 파일은 절대 건드리지 않는다.
//
// 이 테스트는 실제로 내장 Node(AITBrotliCompressor.TryResolveNode)를 써서 합성 .br 을 만들고
//  재인코딩을 실행한다. 재인코딩과 픽스처 생성 모두 '동일한' 내장 Node 를 쓰므로(같은 brotli
//  라이브러리) q11↔q11 라운드트립이 결정적이다(시나리오 5의 "이득 없음→원본 유지" 근거).
//
// Node 미탐지 환경(오프라인 batchmode 등)에서는 Assert.Ignore 로 건너뛴다 — AITEarlyFetchRuntimeTests
//  와 동일한 컨벤션. 픽스처 입력은 수 MB 이하 소형으로 유지해 q11 인코딩이 수 초 내 끝난다.
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.Package;

[TestFixture]
public class AITBrotliRecompressTests
{
    // 합성 데이터 생성/인코딩/디코딩 검증을 한 파일에 모은 Node 유틸리티.
    //  gen  <dst> <size> <seed>      : 압축 잘 되는 pseudo-text 생성(q1↔q11 이득이 유의미)
    //  enc  <src> <dst> <q>          : q + LGWIN=24 + SIZE_HINT(=원본 크기)로 .br 생성 — 재인코딩 파라미터와 동일하게 맞춰
    //                                  q11 픽스처는 재인코딩 결과와 바이트가 정확히 일치(시나리오 5 결정성)
    //  dec  <brfile> <rawref>        : .br 을 디코드해 원본 참조와 바이트 비교 → 'MATCH n' / 'MISMATCH ...'
    private const string UtilJs =
        "'use strict';\n" +
        "const zlib=require('zlib'),fs=require('fs');\n" +
        "const mode=process.argv[2];\n" +
        "const WORDS=['the','quick','brown','fox','jumps','over','lazy','dog','apps','toss','unity','build','brotli','quality','stream','asset','and','of','to','a'];\n" +
        "function pseudoText(n,seed){let s=(seed>>>0)||1;const parts=[];let len=0;while(len<n){s=(1103515245*s+12345)>>>0;const t=WORDS[(s>>>8)%WORDS.length];parts.push(t);len+=t.length+1;}return Buffer.from(parts.join(' '),'utf8').slice(0,n);}\n" +
        "function encParams(q,hint){return {params:{[zlib.constants.BROTLI_PARAM_QUALITY]:q,[zlib.constants.BROTLI_PARAM_LGWIN]:24,[zlib.constants.BROTLI_PARAM_SIZE_HINT]:hint}};}\n" +
        "try{\n" +
        "  if(mode==='gen'){const dst=process.argv[3],n=parseInt(process.argv[4],10),seed=parseInt(process.argv[5]||'1',10);fs.writeFileSync(dst,pseudoText(n,seed));process.stdout.write('GEN '+n);}\n" +
        "  else if(mode==='enc'){const src=process.argv[3],dst=process.argv[4],q=parseInt(process.argv[5],10);const buf=fs.readFileSync(src);const br=zlib.brotliCompressSync(buf,encParams(q,buf.length));fs.writeFileSync(dst,br);process.stdout.write('ENC '+buf.length+' '+br.length);}\n" +
        "  else if(mode==='dec'){const br=fs.readFileSync(process.argv[3]);let dec;try{dec=zlib.brotliDecompressSync(br);}catch(e){process.stdout.write('DECERR '+String((e&&e.message)||e));process.exit(0);}const ref=fs.readFileSync(process.argv[4]);process.stdout.write(Buffer.compare(dec,ref)===0?('MATCH '+dec.length):('MISMATCH '+dec.length+'/'+ref.length));}\n" +
        "  else{process.stdout.write('UNKNOWN_MODE '+mode);process.exit(3);}\n" +
        "}catch(e){process.stderr.write('UTIL_ERR '+String((e&&e.stack)||e));process.exit(4);}\n";

    private const int RawSize = 1 << 20; // 1MB — q11 인코딩이 수백 ms 내 끝나는 소형 입력

    private string _node;
    private string _tempRoot;
    private string _buildDir;
    private string _util;

    [SetUp]
    public void SetUp()
    {
        // RecompressBrFilesInPlace 가 내부적으로 쓰는 것과 '동일한' 내장 Node 를 확보한다.
        // 미가용(오프라인 등)이면 재인코딩 자체가 no-op 이므로 테스트를 건너뛴다.
        if (!AITBrotliCompressor.TryResolveNode(out _node) || string.IsNullOrEmpty(_node))
        {
            Assert.Ignore("내장 Node 미가용 — brotli 재인코딩 런타임 테스트 건너뜀");
        }

        _tempRoot = Path.Combine(Path.GetTempPath(), "ait-brotli-recompress-" + Guid.NewGuid().ToString("N"));
        _buildDir = Path.Combine(_tempRoot, "Build");
        Directory.CreateDirectory(_buildDir);
        _util = Path.Combine(_tempRoot, "util.js");
        File.WriteAllText(_util, UtilJs);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch { /* best-effort 정리 — 실패해도 테스트 결과에 영향 없음 */ }
    }

    // ─────────────────────────── 시나리오 ───────────────────────────

    [Test]
    public void Roundtrip_LowQualityBr_ShrinksAndDecodesIdentically()
    {
        // (1) 반복 패턴 데이터를 q1(저품질)로 .br 생성 → q11 재인코딩 시 (a) 크기 감소,
        //     (b) 재인코딩본을 디코드한 바이트가 원본과 완전히 동일해야 한다.
        string raw = Path.Combine(_tempRoot, "webgl.data.raw");
        string br = Path.Combine(_buildDir, "webgl.data.br");
        RunUtil($"gen \"{raw}\" {RawSize} 42");
        RunUtil($"enc \"{raw}\" \"{br}\" 1");

        long before = new FileInfo(br).Length;

        AITBrotliCompressor.RecompressBrFilesInPlace(_buildDir);

        long after = new FileInfo(br).Length;
        Assert.Less(after, before,
            $"q11 재인코딩 후 .br 이 작아져야 한다(before={before}, after={after}).");

        string decOut = RunUtil($"dec \"{br}\" \"{raw}\"");
        StringAssert.StartsWith("MATCH", decOut,
            $"재인코딩된 .br 의 디코드 바이트가 원본과 동일해야 한다. util 출력: {decOut}");
    }

    [Test]
    public void UnitywebFile_IsNeverTouched()
    {
        // (2) .unityweb(decompressionFallback 산출물, 감지 마커)은 실제 brotli 라도 이름 기준으로
        //     제외되어야 한다 — 바이트 하나도 바뀌면 안 된다. (같은 디렉토리에 유효 .br 을 함께 두어
        //     재인코딩 루프가 실제로 돌아도 .unityweb 은 건너뛰는지 확인한다.)
        string raw = Path.Combine(_tempRoot, "src.raw");
        string br = Path.Combine(_buildDir, "webgl.data.br");
        string unityweb = Path.Combine(_buildDir, "webgl.data.unityweb");
        RunUtil($"gen \"{raw}\" {RawSize} 7");
        RunUtil($"enc \"{raw}\" \"{br}\" 1");
        RunUtil($"enc \"{raw}\" \"{unityweb}\" 1"); // .unityweb 도 실제 brotli(마커 없어도 이름으로 제외되는지 검증)

        byte[] unitywebBefore = File.ReadAllBytes(unityweb);

        AITBrotliCompressor.RecompressBrFilesInPlace(_buildDir);

        byte[] unitywebAfter = File.ReadAllBytes(unityweb);
        CollectionAssert.AreEqual(unitywebBefore, unitywebAfter,
            ".unityweb 파일은 재인코딩 대상에서 제외되어 바이트가 그대로여야 한다.");
    }

    [Test]
    public void BrFileWithUnitywebSubstring_IsSkippedByMarkerGuard()
    {
        // (2b) EndsWith(".br") 필터만으로는 걸러지지 않는 케이스: 파일명이 "unityweb" 문자열을
        //     포함하면서 동시에 ".br" 로 끝나는 경우(예: webgl.data.unityweb.br). 이 픽스처는
        //     실제 빌드 파이프라인에서는 나오지 않지만(AITBuildValidator.GetFilePatterns —
        //     decompressionFallback 산출물은 항상 ".unityweb" 로 '끝나고' ".br" 접미는 붙지 않음),
        //     RecompressBrFilesInPlace 227~231행의 IndexOf(".unityweb") 마커 가드를 실제로
        //     뮤테이션 킬하기 위한 것이다 — 이 가드를 지우면 EndsWith(".br") 필터만 남아 이 파일도
        //     재인코딩 대상에 포함되고(유효 brotli이므로 실행도 성공), q1→q11 재인코딩으로 바이트가
        //     반드시 줄어들어(시나리오 1과 동일 논리) 이 테스트가 RED 로 전환된다.
        string raw = Path.Combine(_tempRoot, "src.raw");
        string br = Path.Combine(_buildDir, "webgl.data.unityweb.br");
        RunUtil($"gen \"{raw}\" {RawSize} 5");
        RunUtil($"enc \"{raw}\" \"{br}\" 1");

        byte[] before = File.ReadAllBytes(br);

        AITBrotliCompressor.RecompressBrFilesInPlace(_buildDir);

        byte[] after = File.ReadAllBytes(br);
        CollectionAssert.AreEqual(before, after,
            "파일명에 \"unityweb\"이 포함되면 \".br\"로 끝나더라도 마커 가드에 의해 재인코딩 대상에서 제외되어 바이트가 그대로여야 한다.");
    }

    [Test]
    public void NonBrFile_IsIgnored()
    {
        // (3) .br 이 아닌 파일(예: 비압축 .data, .txt)은 무시되어야 한다.
        //     (유효 .br 을 함께 두어 루프가 돌아도 비-.br 은 건드리지 않는지 확인.)
        string raw = Path.Combine(_tempRoot, "src.raw");
        string br = Path.Combine(_buildDir, "webgl.data.br");
        string plain = Path.Combine(_buildDir, "notes.txt");
        RunUtil($"gen \"{raw}\" {RawSize} 11");
        RunUtil($"enc \"{raw}\" \"{br}\" 1");
        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes("이 파일은 .br 이 아니므로 재인코딩 대상이 아니다.");
        File.WriteAllBytes(plain, plainBytes);

        byte[] plainBefore = File.ReadAllBytes(plain);

        AITBrotliCompressor.RecompressBrFilesInPlace(_buildDir);

        byte[] plainAfter = File.ReadAllBytes(plain);
        CollectionAssert.AreEqual(plainBefore, plainAfter,
            ".br 이 아닌 파일은 재인코딩 대상이 아니므로 바이트가 그대로여야 한다.");
    }

    [Test]
    public void AlreadyQ11Br_KeepsOriginal_NoAdoption()
    {
        // (5) 이미 q11 로 압축된 .br 은 재인코딩해도 더 작아지지 않는다(reBr >= origBr) → 원본 유지.
        //     픽스처와 재인코딩이 동일 파라미터(q11 + SIZE_HINT)를 쓰므로 결과 바이트가 정확히
        //     일치 → reBr == origBr → 채택 안 함 → 파일이 바이트 그대로여야 한다.
        string raw = Path.Combine(_tempRoot, "src.raw");
        string br = Path.Combine(_buildDir, "webgl.data.br");
        RunUtil($"gen \"{raw}\" {RawSize} 99");
        RunUtil($"enc \"{raw}\" \"{br}\" 11");

        byte[] before = File.ReadAllBytes(br);

        AITBrotliCompressor.RecompressBrFilesInPlace(_buildDir);

        byte[] after = File.ReadAllBytes(br);
        CollectionAssert.AreEqual(before, after,
            "이미 q11 인 .br 은 재인코딩 이득이 없으므로(reBr >= origBr) 원본이 그대로 유지되어야 한다.");
    }

    [Test]
    public void EffectiveBrotliRecompress_FlagOff_IsNoOpGate()
    {
        // (4) 플래그 OFF(기본값) + 환경변수 미설정이면 게이트가 false → 훅이 호출되지 않아 완전 no-op.
        //     환경변수 오버라이드가 양방향으로 동작하는지도 함께 검증한다.
        const string EnvKey = "AIT_BROTLI_RECOMPRESS";
        string saved = Environment.GetEnvironmentVariable(EnvKey);
        var offConfig = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        var onConfig = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            onConfig.brotliRecompress = true;

            // 환경변수 없음: 설정값 그대로.
            Environment.SetEnvironmentVariable(EnvKey, null);
            Assert.IsFalse(WebGLBuildCopier.EffectiveBrotliRecompress(offConfig),
                "기본(false) + 환경변수 없음 → 게이트 false(완전 no-op).");
            Assert.IsFalse(offConfig.brotliRecompress, "선언 기본값은 false 여야 한다.");
            Assert.IsTrue(WebGLBuildCopier.EffectiveBrotliRecompress(onConfig),
                "brotliRecompress=true → 게이트 true.");

            // 환경변수 '1'/'true' → 강제 활성(설정 false 여도 override).
            Environment.SetEnvironmentVariable(EnvKey, "1");
            Assert.IsTrue(WebGLBuildCopier.EffectiveBrotliRecompress(offConfig),
                "AIT_BROTLI_RECOMPRESS=1 → 설정이 false 여도 활성.");
            Environment.SetEnvironmentVariable(EnvKey, "true");
            Assert.IsTrue(WebGLBuildCopier.EffectiveBrotliRecompress(offConfig),
                "AIT_BROTLI_RECOMPRESS=true → 활성.");

            // 환경변수 '0'/'false' → 강제 비활성(설정 true 여도 override).
            Environment.SetEnvironmentVariable(EnvKey, "0");
            Assert.IsFalse(WebGLBuildCopier.EffectiveBrotliRecompress(onConfig),
                "AIT_BROTLI_RECOMPRESS=0 → 설정이 true 여도 비활성.");
            Environment.SetEnvironmentVariable(EnvKey, "false");
            Assert.IsFalse(WebGLBuildCopier.EffectiveBrotliRecompress(onConfig),
                "AIT_BROTLI_RECOMPRESS=false → 비활성.");

            // null config 방어.
            Environment.SetEnvironmentVariable(EnvKey, null);
            Assert.IsFalse(WebGLBuildCopier.EffectiveBrotliRecompress(null),
                "config=null → 안전하게 false.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvKey, saved);
            UnityEngine.Object.DestroyImmediate(offConfig);
            UnityEngine.Object.DestroyImmediate(onConfig);
        }
    }

    // ─────────────────────────── 헬퍼 ───────────────────────────

    // util.js 를 argv 로 실행하고 stdout 을 돌려준다(비정상 종료/타임아웃은 Assert 실패).
    private string RunUtil(string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _node,
            Arguments = $"\"{_util}\" {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _tempRoot,
        };

        AITProcessExecutor.Result result = AITProcessExecutor.Run(startInfo, 60000);
        Assert.IsFalse(result.TimedOut,
            $"util.js '{args}' 가 60초 내 종료되지 않았습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
        Assert.AreEqual(0, result.ExitCode,
            $"util.js '{args}' 가 비정상 종료했습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
        return result.StdOut;
    }
}
