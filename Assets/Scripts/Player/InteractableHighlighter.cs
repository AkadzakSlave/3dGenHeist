using System.Collections.Generic;
using UnityEngine;

public class InteractableHighlighter : MonoBehaviour
{
    [Header("Highlight Visuals")]
    [Tooltip("Цвет контура и легкого свечения (стильный яркий лазурный/синий)")]
    public Color outlineColor = new Color(0.2f, 0.85f, 1.0f, 0.95f);
    [Tooltip("Толщина обводки в метрах")]
    public float outlineWidth = 0.015f;

    [Header("Material Override (Optional)")]
    public Material customHighlightMaterial;

    private Material runtimeHighlightMaterial;
    private GameObject currentTarget;
    private readonly List<GameObject> activeOutlineObjects = new List<GameObject>();
    private readonly List<Renderer> glowingRenderers = new List<Renderer>();
    private MaterialPropertyBlock propBlock;
    private readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        InitMaterial();
    }

    private void OnValidate()
    {
        UpdateMaterialProperties();
    }

    private void InitMaterial()
    {
        if (customHighlightMaterial != null)
        {
            runtimeHighlightMaterial = new Material(customHighlightMaterial);
        }
        else
        {
            Material resMat = Resources.Load<Material>("M_InteractableHighlight");
            if (resMat != null)
            {
                runtimeHighlightMaterial = new Material(resMat);
            }
            else
            {
                Shader outlineShader = Shader.Find("Heist/InteractableOutline");
                if (outlineShader == null)
                {
                    outlineShader = Shader.Find("Heist/InteractablePinkGlow");
                }
                if (outlineShader == null)
                {
                    outlineShader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (outlineShader != null)
                {
                    runtimeHighlightMaterial = new Material(outlineShader);
                }
            }
        }

        UpdateMaterialProperties();
    }

    public void UpdateMaterialProperties()
    {
        if (runtimeHighlightMaterial != null)
        {
            runtimeHighlightMaterial.SetColor("_OutlineColor", outlineColor);
            runtimeHighlightMaterial.SetFloat("_OutlineWidth", outlineWidth);
            runtimeHighlightMaterial.SetColor("_HighlightColor", outlineColor);
            runtimeHighlightMaterial.SetColor("_GlowColor", outlineColor);
        }
    }

    public void SetHighlight(GameObject target)
    {
        if (currentTarget == target) return;

        ClearHighlight();

        if (target == null) return;

        currentTarget = target;

        if (runtimeHighlightMaterial == null)
        {
            InitMaterial();
        }

        if (runtimeHighlightMaterial == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>();
        if (meshFilters != null && meshFilters.Length > 0)
        {
            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                // 1. Create a dedicated outline child object with inverted hull shader
                GameObject outlineObj = new GameObject("__OutlineVisual");
                outlineObj.hideFlags = HideFlags.DontSave;
                outlineObj.transform.SetParent(mf.transform, false);
                outlineObj.transform.localPosition = Vector3.zero;
                outlineObj.transform.localRotation = Quaternion.identity;
                outlineObj.transform.localScale = Vector3.one;

                MeshFilter outMf = outlineObj.AddComponent<MeshFilter>();
                outMf.sharedMesh = mf.sharedMesh;

                MeshRenderer outMr = outlineObj.AddComponent<MeshRenderer>();
                outMr.sharedMaterial = runtimeHighlightMaterial;
                outMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outMr.receiveShadows = false;

                activeOutlineObjects.Add(outlineObj);

                // 2. Add subtle surface emission glow to the item
                mr.GetPropertyBlock(propBlock);
                propBlock.SetColor(emissionColorId, outlineColor * 0.35f);
                mr.SetPropertyBlock(propBlock);
                glowingRenderers.Add(mr);
            }
        }
    }

    public void ClearHighlight()
    {
        if (activeOutlineObjects.Count > 0)
        {
            foreach (var obj in activeOutlineObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            activeOutlineObjects.Clear();
        }

        if (glowingRenderers.Count > 0)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            propBlock.Clear();
            foreach (var r in glowingRenderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
            glowingRenderers.Clear();
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
        if (runtimeHighlightMaterial != null)
        {
            Destroy(runtimeHighlightMaterial);
        }
    }
}
