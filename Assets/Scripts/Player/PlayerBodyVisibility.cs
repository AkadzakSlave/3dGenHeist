using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerBodyVisibility : MonoBehaviour
{
    [Header("Perspective Settings")]
    [Tooltip("Если true — локальный игрок: тело отбрасывает тень, но скрыто из вида камеры. Если false — сторонний игрок: тело видно со всеми анимациями.")]
    public bool isLocalPlayer = true;

    [Header("Exclusions")]
    [Tooltip("Контейнер предметов в руках (от первого лица), которые всегда должны быть видимы локальному игроку")]
    public Transform itemsInHandRoot;

    private readonly List<Renderer> bodyRenderers = new List<Renderer>();

    private void Awake()
    {
        CacheAndApply();
    }

    private void Start()
    {
        CacheAndApply();
    }

    public void SetLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;
        ApplyVisibility();
    }

    public void CacheAndApply()
    {
        bodyRenderers.Clear();
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (var rend in allRenderers)
        {
            if (rend == null) continue;

            // Исключаем предметы от 1-го лица
            if (itemsInHandRoot != null && rend.transform.IsChildOf(itemsInHandRoot))
            {
                continue;
            }

            // Исключаем любые предметы из инвентаря
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.allPossibleItems != null)
            {
                bool isInventoryItem = false;
                foreach (var item in PlayerInventory.Instance.allPossibleItems)
                {
                    if (item != null && rend.transform.IsChildOf(item.transform))
                    {
                        isInventoryItem = true;
                        break;
                    }
                }
                if (isInventoryItem) continue;
            }

            bodyRenderers.Add(rend);
        }

        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        ShadowCastingMode targetMode = isLocalPlayer ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

        foreach (var rend in bodyRenderers)
        {
            if (rend != null)
            {
                rend.shadowCastingMode = targetMode;
            }
        }
    }
}
