using UnityEngine;
using UnityEngine.UI;

public class MainMenuGridLayout : MonoBehaviour
{
    [SerializeField] int columns = 2;
    [SerializeField] int rows = 2;
    
    void OnRectTransformDimensionsChange() => Resize();
    void OnEnable() => Resize();
    
    void Resize()
    {
        var grid = GetComponent<GridLayoutGroup>();
        var rt = (RectTransform)transform;
        if (grid == null) return;

        float w = rt.rect.width  - grid.padding.left - grid.padding.right  - grid.spacing.x * (columns - 1);
        float h = rt.rect.height - grid.padding.top  - grid.padding.bottom - grid.spacing.y * (rows - 1);

        grid.cellSize = new Vector2(w / columns, h / rows);
    }
}
