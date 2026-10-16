using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafeArea;
    Vector2Int lastResolution;

    void OnEnable() => Apply();
    void OnRectTransformDimensionsChange() => Apply();

    void Apply()
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        Rect safe = Screen.safeArea;
        var res = new Vector2Int(Screen.width, Screen.height);
        if (res.x <= 0 || res.y <= 0) return;
        if (safe == lastSafeArea && res == lastResolution) return;

        lastSafeArea = safe;
        lastResolution = res;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= res.x; min.y /= res.y;
        max.x /= res.x; max.y /= res.y;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}