using System.Collections.Generic;
using UnityEngine;

public class InteractableHighlighter : MonoBehaviour
{
    [Header("Glow Colors")]
    [Tooltip("Цвет мягкой подсветки для обычных интерактивных предметов (оружие, расходники, терминалы и т.д.)")]
    public Color itemGlowColor = new Color(1.0f, 0.82f, 0.25f, 1f);

    [Tooltip("Цвет мягкой подсветки для лута (деньги, золото, чемоданы и ценности)")]
    public Color lootGlowColor = new Color(0.2f, 1.0f, 0.35f, 1f);

    [Header("Glow Intensity")]
    [Range(0.05f, 2.0f)]
    [Tooltip("Интенсивность подсветки (0.35 - мягкое элегантное свечение без потери текстуры)")]
    public float baseGlowIntensity = 0.35f;
    public bool enablePulse = true;
    public float pulseSpeed = 3.5f;

    private struct MatOriginalState
    {
        public Material mat;
        public bool hadEmissionKeyword;
        public Texture originalEmissionMap;
    }

    private GameObject currentTarget;
    private readonly List<Renderer> activeRenderers = new List<Renderer>();
    private readonly List<MatOriginalState> modifiedMaterials = new List<MatOriginalState>();
    private MaterialPropertyBlock propBlock;
    private readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");
    private readonly int emissionMapId = Shader.PropertyToID("_EmissionMap");
    private Color currentTargetColor;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (currentTarget == null || activeRenderers.Count == 0) return;

        if (enablePulse)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * pulseSpeed);
            Color finalColor = currentTargetColor * (baseGlowIntensity * pulse);

            foreach (var r in activeRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor(emissionColorId, finalColor);
                r.SetPropertyBlock(propBlock);
            }
        }
    }

    public void SetHighlight(GameObject target)
    {
        if (currentTarget == target) return;

        ClearHighlight();

        if (target == null) return;

        currentTarget = target;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        // 1. Определяем тип предмета: Лут (зеленый) или Обычный предмет (желтый)
        bool isLoot = target.GetComponentInParent<LootItem>() != null || 
                      target.GetComponentInChildren<LootItem>() != null;

        currentTargetColor = isLoot ? lootGlowColor : itemGlowColor;

        // 2. Находим все рендеры (включая SkinnedMeshRenderer и MeshRenderer)
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0)
        {
            renderers = target.GetComponentsInParent<Renderer>();
        }

        if (renderers == null || renderers.Length == 0) return;

        Color applyColor = currentTargetColor * baseGlowIntensity;

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            // Игнорируем трейлы, партиклы и линии
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            // Обрабатываем материалы инстанса
            Material[] mats = r.materials;
            if (mats != null)
            {
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    bool hadKeyword = mat.IsKeywordEnabled("_EMISSION");
                    Texture origMap = mat.HasProperty(emissionMapId) ? mat.GetTexture(emissionMapId) : null;

                    modifiedMaterials.Add(new MatOriginalState
                    {
                        mat = mat,
                        hadEmissionKeyword = hadKeyword,
                        originalEmissionMap = origMap
                    });

                    if (!hadKeyword)
                    {
                        mat.EnableKeyword("_EMISSION");
                    }

                    // Если у материала есть запеченная черная карта эмиссии (как у динамита, молота или части Томпсона),
                    // временно подменяем её на whiteTexture, чтобы весь предмет светился равномерно
                    if (origMap != null && origMap != Texture2D.whiteTexture)
                    {
                        mat.SetTexture(emissionMapId, Texture2D.whiteTexture);
                    }
                }
            }

            // Устанавливаем эмиссию через MaterialPropertyBlock
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(emissionColorId, applyColor);
            propBlock.SetTexture(emissionMapId, Texture2D.whiteTexture);
            r.SetPropertyBlock(propBlock);

            activeRenderers.Add(r);
        }
    }

    public void ClearHighlight()
    {
        if (activeRenderers.Count > 0)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            propBlock.Clear();

            foreach (var r in activeRenderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
            activeRenderers.Clear();
        }

        if (modifiedMaterials.Count > 0)
        {
            foreach (var state in modifiedMaterials)
            {
                if (state.mat != null)
                {
                    if (!state.hadEmissionKeyword)
                    {
                        state.mat.DisableKeyword("_EMISSION");
                    }
                    if (state.originalEmissionMap != null)
                    {
                        state.mat.SetTexture(emissionMapId, state.originalEmissionMap);
                    }
                }
            }
            modifiedMaterials.Clear();
        }

        currentTarget = null;
    }

    private void OnDisable()
    {
        ClearHighlight();
    }

    private void OnDestroy()
    {
        ClearHighlight();
    }
}
