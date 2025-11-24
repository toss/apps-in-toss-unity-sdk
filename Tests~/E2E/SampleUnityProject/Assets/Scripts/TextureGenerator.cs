using UnityEngine;

/// <summary>
/// 런타임에 대용량 텍스처를 생성하여 메모리 및 렌더링 성능을 테스트합니다.
/// </summary>
public class TextureGenerator : MonoBehaviour
{
    [Header("Texture Settings")]
    public int textureSize = 1024;
    public TextureFormat format = TextureFormat.RGBA32;
    public bool useMipMaps = true;

    [Header("Pattern Settings")]
    public Color color1 = Color.red;
    public Color color2 = Color.blue;
    public int patternScale = 32;

    private Texture2D generatedTexture;

    void Start()
    {
        GenerateTexture();
    }

    void Update()
    {
        // 'T' 키로 텍스처 재생성
        if (Input.GetKeyDown(KeyCode.T))
        {
            GenerateTexture();
        }
    }

    public void GenerateTexture()
    {
        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
        }

        Debug.Log($"Generating {textureSize}x{textureSize} texture...");

        generatedTexture = new Texture2D(textureSize, textureSize, format, useMipMaps);

        // 체커보드 패턴 생성
        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool isEven = ((x / patternScale) + (y / patternScale)) % 2 == 0;
                pixels[y * textureSize + x] = isEven ? color1 : color2;
            }
        }

        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply();

        // 씬의 모든 렌더러에 텍스처 적용
        ApplyTextureToScene();

        Debug.Log($"Texture generated: {textureSize}x{textureSize}, Memory: ~{(textureSize * textureSize * 4) / (1024 * 1024)}MB");
    }

    void ApplyTextureToScene()
    {
#if UNITY_2023_1_OR_NEWER
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
        Renderer[] renderers = FindObjectsOfType<Renderer>();
#endif

        foreach (Renderer renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.mainTexture = generatedTexture;
            }
        }
    }

    void OnDestroy()
    {
        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
        }
    }
}
