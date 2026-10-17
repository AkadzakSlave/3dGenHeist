using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Предотвращает зависание кнопок в состоянии "Selected" (когда спрайт или цвет остается выделенным после клика).
/// Сбрасывает выделение из EventSystem сразу после клика и отключает залипание навигации.
/// </summary>
[DisallowMultipleComponent]
public class ButtonAutoDeselect : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (selectable != null)
        {
            // Отключаем автоматический фокус клавиатуры/геймпада, чтобы спрайты не зависали
            Navigation nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Deselect();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Deselect();
    }

    private void Deselect()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
