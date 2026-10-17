using UnityEngine;

public enum FMODSurfaceType
{
    Concrete = 0,
    Wood = 1,
    Metal = 2,
    Flesh = 3
}

/// <summary>
/// Компонент для явного указания типа поверхности объекта (для звуков пуль FMOD, следов и шагов).
/// </summary>
[AddComponentMenu("Heist/Surface Identifier")]
public class SurfaceIdentifier : MonoBehaviour
{
    [Tooltip("Тип поверхности для звуков пуль, рикошетов и шагов")]
    public FMODSurfaceType surfaceType = FMODSurfaceType.Concrete;
}
