using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>game posture 픽스처용 uGUI/카메라/쿼드 생성 헬퍼(전부 런타임 코드 생성 — 씬에는 AITGameScene 루트와 참조용 내장 컴포넌트만 직렬화된다).</summary>
public static class AITGameUI
{
    public static float Safe(float v)
    {
        return (float.IsNaN(v) || float.IsInfinity(v)) ? 0f : v;
    }

    public static Camera CreateCamera(float orthoSize)
    {
        var go = new GameObject("AITGameCamera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
        return cam;
    }

    public static GameObject MakeQuad(string name, Material mat, Vector3 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        mesh.triangles = new int[] { 0, 3, 2, 0, 2, 1 };
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;
        return go;
    }

    public static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static Text CreateText(Transform parent, string name, string text, Font font, int fontSize,
        TextAnchor align, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button CreateButton(Transform parent, string name, string label, Font font, Vector2 anchor,
        Vector2 anchoredPos, Vector2 size, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.55f, 0.95f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        CreateText(go.transform, "Label", label, font, 64, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return btn;
    }

    /// <summary>Overlay 캔버스 위 RectTransform 의 화면 정규화 중심/크기(원점 좌상단).</summary>
    public static AITGameButtonInfo Describe(string name, RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);   // Overlay 캔버스에서는 화면 픽셀 좌표
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);
        var info = new AITGameButtonInfo();
        info.name = name;
        info.x = Safe((c[0].x + c[2].x) * 0.5f / w);
        info.y = Safe(1f - (c[0].y + c[2].y) * 0.5f / h);
        info.w = Safe(Mathf.Abs(c[2].x - c[0].x) / w);
        info.h = Safe(Mathf.Abs(c[2].y - c[0].y) / h);
        return info;
    }
}
