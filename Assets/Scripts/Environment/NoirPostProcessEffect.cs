using UnityEngine;

/// <summary>
/// Полноэкранный пост-процессинг эффект: классический нуар / комикс-стиль (как в Lethal Company, Borderlands, ZZZ).
/// Накладывается на камеру и автоматически стилизует ВСЮ игру (все модели, стены, комнаты, оружие и врагов)
/// без необходимости вручную менять материалы на сотнях объектов.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Heist/PostProcess/Noir Comic Effect")]
public class NoirPostProcessEffect : MonoBehaviour
{
    [Tooltip("Шейдер пост-процессинга (Hidden/Heist/NoirComicPostProcess)")]
    public Shader noirShader;

    [Header("Ink Outlines (Черный контур комикса)")]
    public bool enableOutline = true;
    [Range(0.01f, 0.6f), Tooltip("Чувствительность контура (меньше = больше линий)")]
    public float outlineSensitivity = 0.18f;
    [Range(0.5f, 3.0f), Tooltip("Толщина черных линий")]
    public float outlineThickness = 1.0f;
    [Tooltip("Цвет контурных линий")]
    public Color outlineColor = new Color(0.02f, 0.02f, 0.02f, 1f);

    [Header("Lighting & Contrast (Яркость и Контраст)")]
    [Range(0.5f, 2.5f), Tooltip("Общая яркость сцены (1.0 = как есть, >1.0 = светлее)")]
    public float brightness = 1.15f;
    [Range(0.5f, 1.8f), Tooltip("Контрастность изображения (1.0 = без затемнения полутонов)")]
    public float contrast = 1.0f;
    [Range(0.0f, 0.4f), Tooltip("Срезание глубоких теней в абсолютный черный (0 = мягкие тени, всё видно)")]
    public float shadowCrush = 0.0f;
    [Range(0.0f, 1.5f), Tooltip("Насыщенность (0 = черно-белый нуар, 0.9-1.0 = естественные цвета)")]
    public float saturation = 0.95f;

    [Header("Cel Shading Banding (Ступени освещения)")]
    public bool enableCelBanding = true;
    [Range(2, 16), Tooltip("Количество градаций света (чем больше шагов, тем светлее и детальнее темные зоны)")]
    public int celSteps = 6;

    [Header("Cinematic Vignette (Затемнение по краям)")]
    [Range(0.0f, 0.8f), Tooltip("Интенсивность виньетки")]
    public float vignetteIntensity = 0.15f;
    [Range(0.1f, 1.0f), Tooltip("Мягкость виньетки")]
    public float vignetteSmoothness = 0.6f;

    private Material postProcessMaterial;
    private Camera cam;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            // Требуется для расчета контуров по глубине (Edge Detection)
            cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        CheckInitMaterial();
    }

    private void OnDisable()
    {
        if (postProcessMaterial != null)
        {
            if (Application.isPlaying) Destroy(postProcessMaterial);
            else DestroyImmediate(postProcessMaterial);
            postProcessMaterial = null;
        }
    }

    private bool CheckInitMaterial()
    {
        if (noirShader == null)
        {
            noirShader = Shader.Find("Hidden/Heist/NoirComicPostProcess");
        }

        if (noirShader == null)
        {
            return false;
        }

        if (postProcessMaterial == null || postProcessMaterial.shader != noirShader)
        {
            postProcessMaterial = new Material(noirShader);
            postProcessMaterial.hideFlags = HideFlags.DontSave;
        }

        return true;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!CheckInitMaterial() || postProcessMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // Передаем параметры в шейдер
        postProcessMaterial.SetFloat("_EnableOutline", enableOutline ? 1.0f : 0.0f);
        postProcessMaterial.SetFloat("_EdgeThreshold", outlineSensitivity);
        postProcessMaterial.SetFloat("_EdgeThickness", outlineThickness);
        postProcessMaterial.SetColor("_OutlineColor", outlineColor);

        postProcessMaterial.SetFloat("_Brightness", brightness);
        postProcessMaterial.SetFloat("_Contrast", contrast);
        postProcessMaterial.SetFloat("_ShadowCrush", shadowCrush);
        postProcessMaterial.SetFloat("_Saturation", saturation);

        postProcessMaterial.SetFloat("_EnableCelBanding", enableCelBanding ? 1.0f : 0.0f);
        postProcessMaterial.SetFloat("_PosterizeSteps", celSteps);

        postProcessMaterial.SetFloat("_VignetteIntensity", vignetteIntensity);
        postProcessMaterial.SetFloat("_VignetteSmoothness", vignetteSmoothness);

        Graphics.Blit(source, destination, postProcessMaterial);
    }

    // --- ПРЕСЕТЫ В КОНТЕКСТНОМ МЕНЮ ДЛЯ БЫСТРОЙ НАСТРОЙКИ ---

    [ContextMenu("Preset: 1. Яркий комикс (Bright Comic) - ВСЁ ВИДНО")]
    public void ApplyPresetBrightComic()
    {
        enableOutline = true;
        outlineSensitivity = 0.22f;
        outlineThickness = 1.0f;
        outlineColor = new Color(0.04f, 0.04f, 0.04f, 1f);

        brightness = 1.35f;
        contrast = 1.0f;
        shadowCrush = 0.0f;
        saturation = 1.0f;

        enableCelBanding = true;
        celSteps = 7;

        vignetteIntensity = 0.1f;
        vignetteSmoothness = 0.6f;
    }

    [ContextMenu("Preset: 2. Сбалансированный Нуар (Noir Graphic Novel)")]
    public void ApplyPresetNoirComic()
    {
        enableOutline = true;
        outlineSensitivity = 0.20f;
        outlineThickness = 1.0f;
        outlineColor = new Color(0.03f, 0.03f, 0.03f, 1f);

        brightness = 1.15f;
        contrast = 1.05f;
        shadowCrush = 0.02f;
        saturation = 0.95f;

        enableCelBanding = true;
        celSteps = 6;

        vignetteIntensity = 0.18f;
        vignetteSmoothness = 0.6f;
    }

    [ContextMenu("Preset: 3. Lethal Company / Borderlands")]
    public void ApplyPresetLethalCompany()
    {
        enableOutline = true;
        outlineSensitivity = 0.16f;
        outlineThickness = 1.2f;
        outlineColor = new Color(0.02f, 0.02f, 0.02f, 1f);

        brightness = 1.2f;
        contrast = 1.1f;
        shadowCrush = 0.0f;
        saturation = 1.1f;

        enableCelBanding = true;
        celSteps = 5;

        vignetteIntensity = 0.25f;
        vignetteSmoothness = 0.5f;
    }

    [ContextMenu("Preset: 4. Чистый ч/б Нуар (Sin City)")]
    public void ApplyPresetSinCity()
    {
        enableOutline = true;
        outlineSensitivity = 0.18f;
        outlineThickness = 1.1f;
        outlineColor = Color.black;

        brightness = 1.25f;
        contrast = 1.25f;
        shadowCrush = 0.08f;
        saturation = 0.0f;

        enableCelBanding = true;
        celSteps = 4;

        vignetteIntensity = 0.35f;
        vignetteSmoothness = 0.6f;
    }

    [ContextMenu("Preset: 5. Мягкая стилизация (Subtle Cel)")]
    public void ApplyPresetSubtleCel()
    {
        enableOutline = true;
        outlineSensitivity = 0.25f;
        outlineThickness = 0.8f;
        outlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        brightness = 1.05f;
        contrast = 1.0f;
        shadowCrush = 0.0f;
        saturation = 1.0f;

        enableCelBanding = true;
        celSteps = 8;

        vignetteIntensity = 0.08f;
        vignetteSmoothness = 0.6f;
    }
}
