// -----------------------------------------------------------------------
// AITFirstFrameRuntimeTests.cs - 첫 프레임 감지(aitOnFirstFrame) 런타임 실행 검증
// Level 0: WebGLTemplates/AITTemplate/index.html 의 AIT_FIRST_FRAME_BEGIN/END 구간을 잘라
//  Node 프로세스에서 실제로 실행한다(canvas/WebGL 컨텍스트 mock). 로딩 오버레이는 이 감지가
//  부르는 콜백으로 숨겨지므로, 감지가 어긋나면 게임 화면이 가려진 채 남거나 그려지기 전에 드러난다.
//  하네스 구조는 AITPageCacheRuntimeTests 와 동일 패턴(Node 미탐지 시 Assert.Ignore,
//  ASSERT_FAIL/HARNESS_OK 프로토콜)을 따른다.
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITFirstFrameRuntimeTests
{
    private const string BeginMarker = "// AIT_FIRST_FRAME_BEGIN";
    private const string EndMarker = "// AIT_FIRST_FRAME_END";

    private const string HarnessSource = @"import { readFileSync } from 'node:fs';

const body = readFileSync(process.argv[2], 'utf8');
const scenario = process.argv[3];
const aitOnFirstFrame = (0, eval)('(function () {' + body + '; return aitOnFirstFrame; })()');

function fail(msg) { process.stderr.write('ASSERT_FAIL: ' + msg + '\n'); process.exit(1); }
function assert(cond, msg) { if (!cond) fail(msg); }
const tick = () => new Promise((r) => setTimeout(r, 0));

// WebGL 컨텍스트 mock: draw 메서드는 프로토타입에 두어 실제 컨텍스트와 같은 모양으로 만든다.
class Ctx {
  constructor() { this.FRAMEBUFFER_BINDING = 0x8CA6; this.bound = null; this.calls = []; this.params = 0; }
  getParameter(p) { this.params++; return p === this.FRAMEBUFFER_BINDING ? this.bound : null; }
  drawElements(a, b) { this.calls.push(['drawElements', a, b]); return 'de'; }
  drawArrays(a, b) { this.calls.push(['drawArrays', a, b]); return 'da'; }
  drawElementsInstanced() { this.calls.push(['drawElementsInstanced']); }
  drawArraysInstanced() { this.calls.push(['drawArraysInstanced']); }
  drawRangeElements() { this.calls.push(['drawRangeElements']); }
}
class Canvas {
  constructor(ctx) { this.ctx = ctx; this.requested = []; }
  getContext(type) { this.requested.push(type); return type === '2d' ? { is2d: true } : this.ctx; }
}
const own = (o, k) => Object.prototype.hasOwnProperty.call(o, k);
const DRAWS = ['drawElements', 'drawArrays', 'drawElementsInstanced', 'drawArraysInstanced', 'drawRangeElements'];

const scenarios = {
  // 기본 프레임버퍼로 나가는 첫 draw 에서 한 번만 콜백하고, 콜백은 draw 가 반환한 뒤(마이크로태스크)에 돈다.
  async first_default_draw_fires_once() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx); let fired = 0;
    aitOnFirstFrame(canvas, () => { fired++; });
    const got = canvas.getContext('webgl2', { alpha: false });
    assert(got === ctx, 'getContext 가 원래 컨텍스트를 그대로 돌려줘야 한다');
    assert(fired === 0, '컨텍스트 생성만으로 콜백하면 안 된다');
    const ret = ctx.drawElements(4, 6);
    assert(ret === 'de', '감싼 draw 가 원래 반환값을 돌려줘야 한다');
    assert(ctx.calls.length === 1 && ctx.calls[0][1] === 4 && ctx.calls[0][2] === 6, '원래 draw 가 인자 그대로 호출돼야 한다');
    assert(fired === 0, '콜백은 draw 호출 스택 안에서 동기로 돌면 안 된다');
    await tick();
    assert(fired === 1, '첫 draw 뒤 콜백 1회 기대, 실제 ' + fired);
    ctx.drawArrays(0, 3); ctx.drawElements(4, 6);
    await tick();
    assert(fired === 1, '이후 draw 에서 다시 콜백하면 안 된다, 실제 ' + fired);
  },
  // 감지 뒤에는 draw call 경로에 래퍼가 남지 않는다(프레임마다 드는 비용 0).
  async unhooks_after_first_draw() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx);
    aitOnFirstFrame(canvas, () => {});
    canvas.getContext('webgl2');
    assert(own(ctx, 'drawElements'), '감지 전에는 래퍼가 걸려 있어야 한다');
    ctx.drawArrays(0, 3);
    for (const m of DRAWS) assert(!own(ctx, m), m + ' 래퍼가 남아 있다');
    const before = ctx.params;
    ctx.drawElements(4, 6);
    assert(ctx.params === before, '감지 뒤 draw 가 getParameter 를 부르면 안 된다');
  },
  // FBO(렌더 텍스처)로 나가는 draw 는 첫 프레임으로 치지 않는다.
  async offscreen_draw_does_not_fire() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx); let fired = 0;
    aitOnFirstFrame(canvas, () => { fired++; });
    canvas.getContext('webgl2');
    ctx.bound = { fbo: 1 };
    ctx.drawElements(4, 6); ctx.drawArrays(0, 3);
    await tick();
    assert(fired === 0, 'FBO draw 에서 콜백하면 안 된다');
    assert(own(ctx, 'drawElements'), 'FBO draw 뒤에도 감지가 유지돼야 한다');
    ctx.bound = null;
    ctx.drawArrays(0, 3);
    await tick();
    assert(fired === 1, '기본 프레임버퍼 draw 에서 콜백 1회 기대, 실제 ' + fired);
  },
  // 다른 코드가 먼저 걸어 둔 인스턴스 래퍼(성능 측정 하네스 등)는 감지 뒤에도 그대로 복원된다.
  async restores_preexisting_instance_wrapper() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx); let outer = 0;
    const proto = ctx.drawElements;
    const pre = function () { outer++; return proto.apply(ctx, arguments); };
    ctx.drawElements = pre;
    aitOnFirstFrame(canvas, () => {});
    canvas.getContext('webgl');
    ctx.drawElements(4, 6);
    assert(outer === 1, '기존 래퍼가 첫 draw 에서 호출돼야 한다');
    assert(ctx.drawElements === pre, '감지 뒤 기존 래퍼가 복원돼야 한다');
    ctx.drawElements(4, 6);
    assert(outer === 2, '복원된 기존 래퍼가 계속 호출돼야 한다');
  },
  // 감지 도중 다른 코드가 같은 이름을 덮어썼으면 그 값을 지우지 않는다.
  async keeps_foreign_override() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx);
    aitOnFirstFrame(canvas, () => {});
    canvas.getContext('webgl2');
    const foreign = function () { return 'foreign'; };
    ctx.drawElements = foreign;
    ctx.drawArrays(0, 3);
    assert(ctx.drawElements === foreign, '남이 덮어쓴 메서드를 지우면 안 된다');
    assert(!own(ctx, 'drawArrays'), '자기 래퍼는 걷어내야 한다');
  },
  // WebGL 이 아닌 컨텍스트, 컨텍스트 생성 실패(null)는 그대로 통과시키고 콜백하지 않는다.
  async non_webgl_and_null_pass_through() {
    const canvas = new Canvas(null); let fired = 0;
    aitOnFirstFrame(canvas, () => { fired++; });
    assert(canvas.getContext('webgl2') === null, 'null 컨텍스트를 그대로 돌려줘야 한다');
    const c2d = canvas.getContext('2d');
    assert(c2d && c2d.is2d, '2d 컨텍스트를 그대로 돌려줘야 한다');
    await tick();
    assert(fired === 0, '콜백하면 안 된다');
  },
  // getParameter 가 던져도(컨텍스트 유실) draw 는 깨지지 않고 첫 프레임으로 친다.
  async get_parameter_throw_is_tolerated() {
    const ctx = new Ctx(); const canvas = new Canvas(ctx); let fired = 0;
    ctx.getParameter = () => { throw new Error('lost'); };
    aitOnFirstFrame(canvas, () => { fired++; });
    canvas.getContext('webgl2');
    assert(ctx.drawArrays(0, 3) === 'da', 'draw 가 정상 반환해야 한다');
    await tick();
    assert(fired === 1, '콜백 1회 기대, 실제 ' + fired);
  },
};

if (!scenarios[scenario]) fail('unknown scenario: ' + scenario);
await scenarios[scenario]();
process.stdout.write('HARNESS_OK\n');
";

    private static string ExtractFirstFrameSource()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/index.html",
                out string path,
                typeof(AITConvertCore)),
            "WebGLTemplates/AITTemplate/index.html 경로를 찾지 못했습니다.");
        string html = File.ReadAllText(path);
        int begin = html.IndexOf(BeginMarker, StringComparison.Ordinal);
        int end = html.IndexOf(EndMarker, StringComparison.Ordinal);
        Assert.GreaterOrEqual(begin, 0, "AIT_FIRST_FRAME_BEGIN 마커를 찾을 수 없습니다.");
        Assert.Greater(end, begin, "AIT_FIRST_FRAME_END 마커를 찾을 수 없습니다.");
        return html.Substring(begin, end - begin);
    }

    private static void RunScenario(string scenarioName)
    {
        string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
        if (string.IsNullOrEmpty(nodePath))
        {
            Assert.Ignore("Node 실행 파일 없음 — 런타임 실행 테스트 건너뜀");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "ait-first-frame-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string scriptPath = Path.Combine(tempDir, "script.js");
        string harnessPath = Path.Combine(tempDir, "harness.mjs");

        try
        {
            File.WriteAllText(scriptPath, ExtractFirstFrameSource());
            File.WriteAllText(harnessPath, HarnessSource);

            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{harnessPath}\" \"{scriptPath}\" {scenarioName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            AITProcessExecutor.Result result = AITProcessExecutor.Run(startInfo, 60000);

            Assert.IsFalse(result.TimedOut,
                $"시나리오 '{scenarioName}' 하네스가 60초 내 종료되지 않았습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
            Assert.AreEqual(0, result.ExitCode,
                $"시나리오 '{scenarioName}' 하네스가 실패했습니다(계약 위반).\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
            StringAssert.Contains("HARNESS_OK", result.StdOut,
                $"시나리오 '{scenarioName}' 하네스가 성공 마커를 출력하지 않았습니다.\n--- STDOUT ---\n{result.StdOut}\n--- STDERR ---\n{result.StdErr}");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort 정리 — 실패해도 테스트 결과에 영향 없음 */ }
        }
    }

    [Test]
    public void FirstDefaultFramebufferDraw_FiresCallbackOnce_AfterDrawReturns() => RunScenario("first_default_draw_fires_once");

    [Test]
    public void AfterFirstDraw_NoWrapperRemainsOnDrawPath() => RunScenario("unhooks_after_first_draw");

    [Test]
    public void OffscreenDraw_DoesNotCountAsFirstFrame() => RunScenario("offscreen_draw_does_not_fire");

    [Test]
    public void PreexistingInstanceWrapper_IsRestored() => RunScenario("restores_preexisting_instance_wrapper");

    [Test]
    public void ForeignOverrideDuringDetection_IsKept() => RunScenario("keeps_foreign_override");

    [Test]
    public void NonWebGLOrNullContext_PassesThroughWithoutCallback() => RunScenario("non_webgl_and_null_pass_through");

    [Test]
    public void GetParameterThrow_IsTolerated() => RunScenario("get_parameter_throw_is_tolerated");

    /// <summary>
    /// 오버레이 숨김이 고정 지연이 아니라 첫 프레임 감지에 묶여 있어야 한다. 500ms 타이머는 감지가
    /// 안 되는 환경의 상한으로만 남는다.
    /// </summary>
    [Test]
    public void Template_HidesOverlayOnFirstFrame_WithTimerFallback()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/index.html",
                out string path,
                typeof(AITConvertCore)));
        string html = File.ReadAllText(path);

        StringAssert.Contains("aitOnFirstFrame(canvas, function () {", html);
        StringAssert.Contains("if (_aitInstanceReady) hideLoadingScreen();", html);
        StringAssert.Contains("if (_aitFirstFrameDrawn) hideLoadingScreen();", html);
        StringAssert.Contains("else setTimeout(hideLoadingScreen, 500);", html);
    }
}
