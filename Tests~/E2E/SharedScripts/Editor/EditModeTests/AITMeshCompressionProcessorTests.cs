// -----------------------------------------------------------------------
// AITMeshCompressionProcessorTests.cs - Mesh 압축 처리기 회귀 가드
//
// Level 0: EffectiveEnabled tri-state 순수 로직 검증(AssetDatabase 비의존).
// Level 1: 실 AssetDatabase 기반 apply→restore 왕복 검증. AITTextureSizeClampProcessorAtlasTests
//   스타일을 따른다 — 임계값(256KB) 초과/미만 자산을 같은 ApplyForBuild 호출로 동시에 스코프해
//   "임계값을 넘는 것만" 건드리는지(비-vacuous 변형 + no-op 보존)와 바이트 단위 verbatim 복원을
//   한 번에 검증한다.
//
// 두 경로 모두 검증:
//   (a) ModelImporter 경로(.obj) — meshCompression 이 Off 인 것만 Medium 으로 상향, 이미 Low 등으로
//       설정된 자산은 사용자 의도를 존중해 건드리지 않음을 별도로 확인.
//   (b) 직렬화 Mesh .asset 경로 — MeshUtility.SetMeshCompression 적용 후 실제로 온디스크 바이트가
//       바뀌는지(비-vacuous)까지 확인.
//
// 두 테스트 모두 대상을 Resources/ 하위에 배치한다 — AITMeshCompressionProcessor 의 대상 탐지가
// "빌드 씬 의존성 + Resources/ 하위"만 스코프하고(다른 처리기와 달리 config 의 대상 폴더 필드가
// 없음), 씬 배선 없이 스코프하려면 Resources/ 경로가 유일한 저비용 방법이기 때문이다
// (HeavyBuildRunner 가 동일 이유로 Assets/Resources/ 아래 메시를 생성하는 것과 같은 패턴).

using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITMeshCompressionProcessorEffectiveEnabledTests
{
    [Test]
    public void EffectiveEnabled_NullConfig_ReturnsFalse()
    {
        Assert.IsFalse(AITMeshCompressionProcessor.EffectiveEnabled(null),
            "config 가 null 이면 항상 비활성이어야 함.");
    }

    [Test]
    public void EffectiveEnabled_ExplicitOff_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.meshCompression = 0;
            Assert.IsFalse(AITMeshCompressionProcessor.EffectiveEnabled(config));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void EffectiveEnabled_ExplicitOn_ReturnsTrue()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.meshCompression = 1;
            Assert.IsTrue(AITMeshCompressionProcessor.EffectiveEnabled(config));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    // 자동(-1)은 신규 손실 레버 opt-in 컨벤션(audioStreamTranscode/textureStreamJpeg 와 동일 posture)에
    // 따라 GetDefaultMeshCompression()(=현재 false)을 그대로 따라야 한다.
    [Test]
    public void EffectiveEnabled_Auto_MatchesGetDefaultMeshCompression()
    {
        var config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        try
        {
            config.meshCompression = -1;
            Assert.AreEqual(AITDefaultSettings.GetDefaultMeshCompression(),
                AITMeshCompressionProcessor.EffectiveEnabled(config),
                "자동(-1)은 GetDefaultMeshCompression() 을 그대로 따라야 함.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    // 시각 검증 게이트 미통과 상태를 명시적으로 고정 — 이 값이 실수로 true 로 뒤집히면
    // (구버전 AITConfig.asset 등에서) 사용자 의도 없이 lossy 양자화가 조용히 켜진다.
    [Test]
    public void GetDefaultMeshCompression_IsCurrentlyOff()
    {
        Assert.IsFalse(AITDefaultSettings.GetDefaultMeshCompression(),
            "Mesh 압축은 시각 검증 게이트를 통과하기 전까지 auto=OFF 여야 함(명시 활성(1)에서만 동작).");
    }
}

[TestFixture]
public class AITMeshCompressionProcessorMeshAssetTests
{
    private const string TempDir = "Assets/AITTest_MeshCompression_MeshAsset";
    private const string ResourcesDir = TempDir + "/Resources/Meshes";
    private const string LargeMeshPath = ResourcesDir + "/large_mesh.asset";
    private const string SmallMeshPath = ResourcesDir + "/small_mesh.asset";

    // 프로세서 내부 상수와 동일해야 한다(백업/마커 잔존물 검증용).
    private const string BackupSuffix = ".aitmeshcompbak";
    private const string MarkerRelative = "Assets/.ait-meshcomp-active";

    // 임계값(256KB)을 여유 있게 넘도록 grid=80(정점 6561개, pos+normal+uv+uv2+tangent+16bit 인덱스)
    // 사용 — 이 속성 조합·밀도는 배경 실측(grid=70, 정점 5041개 → 평균 325.75KB/메시, HeavyGen 픽스처)과
    // 동일 구성이라 실측 대비 산출 크기를 신뢰할 수 있다(grid 80은 그보다 더 큰 여유 마진).
    // 소형은 grid=4(정점 25개)로 임계값에 한참 못 미침.
    private const int LargeGrid = 80;
    private const int SmallGrid = 4;
    private const long ThresholdBytes = 256 * 1024L;

    private string _projectRoot;
    private string _largeAbsPath;
    private string _smallAbsPath;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _largeAbsPath = Path.Combine(_projectRoot, LargeMeshPath);
        _smallAbsPath = Path.Combine(_projectRoot, SmallMeshPath);

        string dirAbs = Path.Combine(_projectRoot, ResourcesDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        CreateGridMeshAsset(LargeMeshPath, LargeGrid);
        CreateGridMeshAsset(SmallMeshPath, SmallGrid);

        AssetDatabase.Refresh();

        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.meshCompression = 1; // 명시적 ON(자동은 시각 검증 전까지 비활성이라 왕복 테스트가 성립하지 않음).
    }

    [TearDown]
    public void TearDown()
    {
        // 잔존 백업 파일 정리(테스트 실패 시에도 샘플 프로젝트를 더럽히지 않도록 — 베스트 에포트).
        string assetsAbs = Application.dataPath;
        foreach (var bak in Directory.GetFiles(assetsAbs, "*" + BackupSuffix, SearchOption.AllDirectories))
        {
            try { File.Delete(bak); } catch { /* best-effort */ }
        }

        string marker = Path.Combine(_projectRoot, MarkerRelative);
        try { if (File.Exists(marker)) { File.Delete(marker); } } catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(LargeMeshPath);
        AssetDatabase.DeleteAsset(SmallMeshPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ApplyThenRestore_SerializedMeshAsset_CompressesOverThresholdPreservesUnderThresholdAndRestoresVerbatim()
    {
        Assert.IsTrue(File.Exists(_largeAbsPath), "사전조건: 대형 Mesh 에셋이 존재해야 함");
        Assert.IsTrue(File.Exists(_smallAbsPath), "사전조건: 소형 Mesh 에셋이 존재해야 함");
        Assert.GreaterOrEqual(new FileInfo(_largeAbsPath).Length, ThresholdBytes,
            "사전조건: 대형 메시 원본 크기가 임계값(256KB) 이상이어야 함");
        Assert.Less(new FileInfo(_smallAbsPath).Length, ThresholdBytes,
            "사전조건: 소형 메시 원본 크기가 임계값(256KB) 미만이어야 함");

        byte[] largeOriginalBytes = File.ReadAllBytes(_largeAbsPath);
        byte[] smallOriginalBytes = File.ReadAllBytes(_smallAbsPath);

        // ── 적용(AITWebGLBuilder 가 BuildPlayer 직전에 호출하는 실 진입점) ──
        var handle = AITMeshCompressionProcessor.ApplyForBuild(_config);

        Assert.IsTrue(handle != null && handle.Active,
            "Mesh 압축 프로세서가 활성화되어야 함(meshCompression=1, 임계값 초과 메시 존재). " +
            "비활성이면 apply→restore 왕복이 성립하지 않음.");
        Assert.AreEqual(1, handle.AssetCount, "임계값 초과 직렬화 Mesh .asset 1개만 처리되어야 함(소형은 no-op).");
        Assert.AreEqual(0, handle.ModelCount, "이 테스트는 직렬화 Mesh .asset 만 다루므로 모델 임포터 처리 건수는 0이어야 함.");

        // ── 대형 메시: 실제로 압축 설정이 반영됐는지(비-vacuous) + 백업 존재 ──
        string largeBackupPath = _largeAbsPath + BackupSuffix;
        Assert.IsTrue(File.Exists(largeBackupPath), "적용 후 대형 메시의 원본 백업(.aitmeshcompbak)이 존재해야 함.");
        Assert.AreNotEqual(largeOriginalBytes, File.ReadAllBytes(_largeAbsPath),
            "적용 후 대형 메시는 압축 설정이 실제로 반영되어 원본과 온디스크 바이트가 달라야 함(비-vacuous 검증).");

        // ── 소형 메시: 전혀 건드려지지 않았는지(백업 미생성 포함) ──
        string smallBackupPath = _smallAbsPath + BackupSuffix;
        Assert.IsFalse(File.Exists(smallBackupPath), "소형 메시는 임계값 미만이라 백업이 생성되지 않아야 함(변경 대상 아님).");
        Assert.AreEqual(smallOriginalBytes, File.ReadAllBytes(_smallAbsPath),
            "소형 메시는 온디스크 바이트가 원본과 동일해야 함(무변경).");

        // ── 복원(이 테스트의 검증 대상 그 자체) ──
        AITMeshCompressionProcessor.RestoreForBuild(handle);
        AssetDatabase.Refresh();

        // ── 검증: 대형 메시가 원본과 바이트 단위로 동일 + 잔존물 전무 ──
        Assert.IsTrue(File.Exists(_largeAbsPath), "복원 후 대형 메시 에셋이 존재해야 함");
        Assert.AreEqual(largeOriginalBytes, File.ReadAllBytes(_largeAbsPath),
            "복원 후 대형 메시는 원본과 바이트 단위로 동일해야 한다. 불일치는 Mesh 압축 프로세서의 복원이 " +
            "에셋을 영구 오염시킨다는 의미다.");

        Assert.IsFalse(File.Exists(largeBackupPath), "복원 후 백업(.aitmeshcompbak)이 삭제되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, MarkerRelative)),
            "복원 후 Mesh 압축 마커(Assets/.ait-meshcomp-active)가 제거되어야 함");
    }

    /// <summary>
    /// 결정론적 절차 생성 그리드 Mesh(수식 기반, 무시드 Random 미사용)를 만들어 저장한다.
    /// HeavyBuildRunner.GenerateMesh 와 동일한 발상(grid 밀도로 파일 크기를 통제)이나
    /// 독립적으로 작성됨 — HeavyBuildRunner.cs 는 이 작업에서 수정 대상이 아니다.
    /// </summary>
    private static void CreateGridMeshAsset(string assetPath, int grid)
    {
        int dim = grid + 1;
        int vcount = dim * dim;
        var verts = new Vector3[vcount];
        var normals = new Vector3[vcount];
        var uv = new Vector2[vcount];
        var uv2 = new Vector2[vcount];
        var tangents = new Vector4[vcount];

        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                int vi = y * dim + x;
                float fx = (float)x / grid;
                float fy = (float)y / grid;
                verts[vi] = new Vector3(fx - 0.5f, 0f, fy - 0.5f);
                normals[vi] = Vector3.up;
                uv[vi] = new Vector2(fx, fy);
                uv2[vi] = new Vector2(fy, fx);
                tangents[vi] = new Vector4(1, 0, 0, -1);
            }
        }

        var tris = new List<int>(grid * grid * 6);
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                int v0 = y * dim + x;
                int v1 = v0 + 1;
                int v2 = v0 + dim;
                int v3 = v2 + 1;
                tris.Add(v0); tris.Add(v2); tris.Add(v1);
                tris.Add(v1); tris.Add(v2); tris.Add(v3);
            }
        }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(assetPath) };
        if (vcount > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.tangents = tangents;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, assetPath);
    }
}

[TestFixture]
public class AITMeshCompressionProcessorModelImporterTests
{
    private const string TempDir = "Assets/AITTest_MeshCompression_Model";
    private const string ResourcesDir = TempDir + "/Resources/Models";
    private const string LargeObjPath = ResourcesDir + "/large_model.obj";
    private const string SmallObjPath = ResourcesDir + "/small_model.obj";
    private const string PreCompressedObjPath = ResourcesDir + "/precompressed_model.obj";

    // 프로세서 내부 상수와 동일해야 한다(백업/마커 잔존물 검증용).
    private const string BackupSuffix = ".aitmeshcompbak";
    private const string MarkerRelative = "Assets/.ait-meshcomp-active";
    private const long ThresholdBytes = 256 * 1024L;

    // "v x y z\n" 한 줄 ≈ 29바이트 → 15000줄 ≈ 425KB, 256KB 임계값을 여유 있게 초과.
    private const int LargeVertexCount = 15000;
    private const int SmallVertexCount = 3;

    private string _projectRoot;
    private string _largeAbsPath;
    private string _smallAbsPath;
    private string _preCompressedAbsPath;
    private AITEditorScriptObject _config;

    [SetUp]
    public void SetUp()
    {
        _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        _largeAbsPath = Path.Combine(_projectRoot, LargeObjPath);
        _smallAbsPath = Path.Combine(_projectRoot, SmallObjPath);
        _preCompressedAbsPath = Path.Combine(_projectRoot, PreCompressedObjPath);

        string dirAbs = Path.Combine(_projectRoot, ResourcesDir);
        if (!Directory.Exists(dirAbs))
        {
            Directory.CreateDirectory(dirAbs);
        }

        File.WriteAllText(_largeAbsPath, BuildObjText(LargeVertexCount));
        File.WriteAllText(_smallAbsPath, BuildObjText(SmallVertexCount));
        File.WriteAllText(_preCompressedAbsPath, BuildObjText(LargeVertexCount));

        AssetDatabase.Refresh();

        // precompressed 대상: 이미 사용자가 압축 설정을 명시한 상태를 시뮬레이트(Off 가 아님 → no-op 대상).
        var preImporter = AssetImporter.GetAtPath(PreCompressedObjPath) as ModelImporter;
        Assert.IsNotNull(preImporter, "사전조건: precompressed obj 가 ModelImporter 로 임포트되어야 함");
        preImporter.meshCompression = ModelImporterMeshCompression.Low;
        preImporter.SaveAndReimport();

        _config = ScriptableObject.CreateInstance<AITEditorScriptObject>();
        _config.meshCompression = 1; // 명시적 ON
    }

    [TearDown]
    public void TearDown()
    {
        string assetsAbs = Application.dataPath;
        foreach (var bak in Directory.GetFiles(assetsAbs, "*" + BackupSuffix, SearchOption.AllDirectories))
        {
            try { File.Delete(bak); } catch { /* best-effort */ }
        }

        string marker = Path.Combine(_projectRoot, MarkerRelative);
        try { if (File.Exists(marker)) { File.Delete(marker); } } catch { /* best-effort */ }

        AssetDatabase.DeleteAsset(LargeObjPath);
        AssetDatabase.DeleteAsset(SmallObjPath);
        AssetDatabase.DeleteAsset(PreCompressedObjPath);
        AssetDatabase.DeleteAsset(TempDir);
        AssetDatabase.Refresh();

        if (_config != null)
        {
            Object.DestroyImmediate(_config);
            _config = null;
        }
    }

    [Test]
    public void ApplyThenRestore_ModelImporterAsset_ChangesOffToMediumPreservesOthersAndRestoresVerbatim()
    {
        Assert.IsTrue(File.Exists(_largeAbsPath), "사전조건: 대형 OBJ 가 존재해야 함");
        Assert.IsTrue(File.Exists(_smallAbsPath), "사전조건: 소형 OBJ 가 존재해야 함");
        Assert.GreaterOrEqual(new FileInfo(_largeAbsPath).Length, ThresholdBytes,
            "사전조건: 대형 OBJ 원본 크기가 임계값(256KB) 이상이어야 함");
        Assert.Less(new FileInfo(_smallAbsPath).Length, ThresholdBytes,
            "사전조건: 소형 OBJ 원본 크기가 임계값(256KB) 미만이어야 함");

        var largeImporterBefore = AssetImporter.GetAtPath(LargeObjPath) as ModelImporter;
        Assert.IsNotNull(largeImporterBefore, "사전조건: 대형 OBJ 가 ModelImporter 로 임포트되어야 함");
        Assert.AreEqual(ModelImporterMeshCompression.Off, largeImporterBefore.meshCompression,
            "사전조건: 대형 OBJ 의 기본 meshCompression 은 Off 여야 함");

        string largeMetaPath = _largeAbsPath + ".meta";
        string smallMetaPath = _smallAbsPath + ".meta";
        string preMetaPath = _preCompressedAbsPath + ".meta";
        byte[] largeMetaOriginal = File.ReadAllBytes(largeMetaPath);
        byte[] smallMetaOriginal = File.ReadAllBytes(smallMetaPath);
        byte[] preMetaOriginal = File.ReadAllBytes(preMetaPath);

        // ── 적용 ──
        var handle = AITMeshCompressionProcessor.ApplyForBuild(_config);

        Assert.IsTrue(handle != null && handle.Active,
            "Mesh 압축 프로세서가 활성화되어야 함(meshCompression=1, 임계값 초과 + Off 상태인 모델 존재).");
        Assert.AreEqual(1, handle.ModelCount,
            "임계값 초과 + meshCompression==Off 인 모델 1개만 처리되어야 함(소형/이미 압축설정된 것은 no-op).");
        Assert.AreEqual(0, handle.AssetCount, "이 테스트는 ModelImporter 자산만 다루므로 직렬화 Mesh 처리 건수는 0이어야 함.");

        // ── 대형 OBJ: Off → Medium 상향 확인(비-vacuous) + .meta 백업 존재 ──
        var largeImporterApplied = AssetImporter.GetAtPath(LargeObjPath) as ModelImporter;
        Assert.AreEqual(ModelImporterMeshCompression.Medium, largeImporterApplied.meshCompression,
            "적용 후 대형 OBJ 의 meshCompression 은 Medium 이어야 함(Off→Medium 상향 실증).");
        string largeMetaBackup = largeMetaPath + BackupSuffix;
        Assert.IsTrue(File.Exists(largeMetaBackup), "적용 후 대형 OBJ .meta 백업(.aitmeshcompbak)이 존재해야 함.");

        // ── 소형 OBJ: 임계값 미만 → 전혀 건드려지지 않아야 함 ──
        Assert.IsFalse(File.Exists(smallMetaPath + BackupSuffix), "소형 OBJ 는 백업이 생성되지 않아야 함(변경 대상 아님).");
        var smallImporterApplied = AssetImporter.GetAtPath(SmallObjPath) as ModelImporter;
        Assert.AreEqual(ModelImporterMeshCompression.Off, smallImporterApplied.meshCompression,
            "소형 OBJ 의 meshCompression 은 원래 값(Off)을 유지해야 함.");

        // ── precompressed OBJ: 이미 Off 가 아니므로 사용자 의도 존중 → no-op ──
        Assert.IsFalse(File.Exists(preMetaPath + BackupSuffix), "이미 압축 설정(Low)이 있는 OBJ 는 백업이 생성되지 않아야 함.");
        var preImporterApplied = AssetImporter.GetAtPath(PreCompressedObjPath) as ModelImporter;
        Assert.AreEqual(ModelImporterMeshCompression.Low, preImporterApplied.meshCompression,
            "이미 Low 로 설정된 OBJ 는 건드리지 않고 그대로 유지해야 함(사용자 의도 존중).");

        // ── 복원(이 테스트의 검증 대상 그 자체) ──
        AITMeshCompressionProcessor.RestoreForBuild(handle);
        AssetDatabase.Refresh();

        // ── 검증: 대형 OBJ 가 Off 로 원복 + .meta 바이트 단위 동일 + 잔존물 전무 ──
        var largeImporterRestored = AssetImporter.GetAtPath(LargeObjPath) as ModelImporter;
        Assert.AreEqual(ModelImporterMeshCompression.Off, largeImporterRestored.meshCompression,
            "복원 후 대형 OBJ 의 meshCompression 은 원래 값(Off)으로 돌아와야 함.");
        Assert.AreEqual(largeMetaOriginal, File.ReadAllBytes(largeMetaPath),
            "복원 후 대형 OBJ .meta 는 원본과 바이트 단위로 동일해야 한다. 불일치는 Mesh 압축 프로세서의 " +
            "복원이 임포터 설정을 영구 오염시킨다는 의미다.");

        // 건드리지 않은 자산들도 부수 효과 없이 그대로여야 함(복원 경로가 무관한 .meta 를 건드리지 않는지 확인).
        Assert.AreEqual(smallMetaOriginal, File.ReadAllBytes(smallMetaPath), "소형 OBJ .meta 는 시종 불변이어야 함.");
        Assert.AreEqual(preMetaOriginal, File.ReadAllBytes(preMetaPath), "precompressed OBJ .meta 는 시종 불변이어야 함.");

        Assert.IsFalse(File.Exists(largeMetaBackup), "복원 후 백업(.aitmeshcompbak)이 삭제되어야 함");
        Assert.IsFalse(File.Exists(Path.Combine(_projectRoot, MarkerRelative)),
            "복원 후 Mesh 압축 마커(Assets/.ait-meshcomp-active)가 제거되어야 함");
    }

    /// <summary>결정론적(수식 기반) OBJ 텍스트 생성. 무시드 Random 미사용 — vertexCount 로 파일 크기를 통제.</summary>
    private static string BuildObjText(int vertexCount)
    {
        var sb = new StringBuilder(vertexCount * 30);
        for (int i = 0; i < vertexCount; i++)
        {
            float fx = (i % 100) * 0.01f;
            float fy = ((i / 100) % 100) * 0.01f;
            float fz = (i / 10000) * 0.01f;
            sb.Append("v ")
              .Append(fx.ToString("F6"))
              .Append(' ')
              .Append(fy.ToString("F6"))
              .Append(' ')
              .Append(fz.ToString("F6"))
              .Append('\n');
        }

        sb.Append("f 1 2 3\n");
        return sb.ToString();
    }
}
