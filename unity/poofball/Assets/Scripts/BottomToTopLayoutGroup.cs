using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A custom LayoutGroup that positions its children bottom to top,
/// and auto-scrolls to the bottom after layout completes.
/// </summary>
[ExecuteAlways]
public class BottomToTopLayoutGroup : LayoutGroup
{
    [Header("Layout Settings")]
    [SerializeField] private float spacing = 8f;

    [Header("Auto Scroll Settings")]
    // Assign your ScrollRect here if you want the view to always jump to bottom.
    public ScrollRect scrollRect;
    public bool autoScrollToBottom = true;

    private bool layoutDirty = false; // track when a new layout pass has occurred

    /// <summary>
    /// Spacing in pixels between each child.
    /// </summary>
    public float Spacing
    {
        get => spacing;
        set
        {
            if (Mathf.Abs(spacing - value) > 0.001f)
            {
                spacing = value;
                MarkDirty();
            }
        }
    }

    #region LayoutGroup Overrides

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();

        float totalMinWidth = padding.horizontal;
        float totalPreferredWidth = padding.horizontal;

        // measure each child's widths
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childMinWidth = LayoutUtility.GetMinSize(child, 0);
            float childPreferredWidth = LayoutUtility.GetPreferredSize(child, 0);

            totalMinWidth = Mathf.Max(totalMinWidth, childMinWidth + padding.horizontal);
            totalPreferredWidth = Mathf.Max(totalPreferredWidth, childPreferredWidth + padding.horizontal);
        }

        SetLayoutInputForAxis(totalMinWidth, totalPreferredWidth, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float totalMinHeight = padding.vertical;
        float totalPreferredHeight = padding.vertical;

        // measure each child's heights
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childMinHeight = LayoutUtility.GetMinSize(child, 1);
            float childPreferredHeight = LayoutUtility.GetPreferredSize(child, 1);

            totalMinHeight += childMinHeight;
            totalPreferredHeight += childPreferredHeight;

            if (i < rectChildren.Count - 1)
            {
                totalMinHeight += spacing;
                totalPreferredHeight += spacing;
            }
        }

        SetLayoutInputForAxis(totalMinHeight, totalPreferredHeight, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        // left-align each child
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childWidth = LayoutUtility.GetPreferredSize(child, 0);
            SetChildAlongAxis(child, 0, padding.left, childWidth);
        }
    }

    public override void SetLayoutVertical()
    {
        // stack children from bottom to top
        float currentY = padding.bottom;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childHeight = LayoutUtility.GetPreferredSize(child, 1);

            SetChildAlongAxis(child, 1, currentY, childHeight);

            currentY += childHeight + spacing;
        }

        // Mark that a layout pass occurred, so we can scroll in LateUpdate
        layoutDirty = true;
    }

    #endregion

    #region MarkDirty Logic

    private void MarkDirty()
    {
        if (!IsActive())
            return;

        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        MarkDirty();
    }

    protected override void OnTransformChildrenChanged()
    {
        base.OnTransformChildrenChanged();
        MarkDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        MarkDirty();
    }
#endif

    #endregion

    // If autoScroll is enabled, scroll to bottom after layout is done.
    private void LateUpdate()
    {
        if (autoScrollToBottom && scrollRect != null)
        {
            // 0 in a standard vertical ScrollRect is bottom; 1 is top
            scrollRect.normalizedPosition = new Vector2(0, 0);

            layoutDirty = false; // reset
        }
    }
}
