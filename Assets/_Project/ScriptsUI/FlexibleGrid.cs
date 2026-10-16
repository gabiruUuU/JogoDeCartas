using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class FlexibleGrid : MonoBehaviour
{
    [SerializeField] int columns = 2;
    [SerializeField] int rows = 5;

    GridLayoutGroup grid;
    RectTransform rt;

    void OnEnable() => Apply();
    void OnRectTransformDimensionsChange() => Apply();

    void Apply()
    {
        if (grid == null)
        {
            grid = GetComponent<GridLayoutGroup>();
            rt = (RectTransform)transform;
        }
        Rect r = rt.rect;
        if (r.width <= 0 || r.height <= 0) return;

        float w = (r.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
        float h = (r.height - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
    }
}