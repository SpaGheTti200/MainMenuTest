using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ScrollerController : MonoBehaviour
{
    [Header("References")]
    public List<GameObject> itemPrefabs;      // If even, we'll add emptyPrefab at the end.
    public GameObject emptyPrefab;            // A placeholder that must never appear as center.
    public RectTransform centerRect;

    [Header("Configuration")]
    public float spacing = 200f;

    public float focusedItemSize = 2f;
    public float sideItemsSize = 1f;

    // public float moveDuration = 0.5f;
    
    public float replacingDuration = 0.5f;
    public float resizingDuration = 0.5f;
    
    public Ease easeType = Ease.OutCubic;

    [Header("Off-screen Setup")]
    public float offScreenLeftX = -1000f;
    public float offScreenRightX = 1000f;

    // Internals
    private List<RectTransform> _items = new List<RectTransform>();
    private List<Vector2> _positions = new List<Vector2>();

    private int _finalCount;     
    private int _centerIndex;    
    private int _leftmostIndex;  
    private int _rightmostIndex;

    // A simple guard to avoid infinite loops if we do a "double scroll"
    private bool _skipCenterCheck = false;

    private void Start()
    {
        // If we have an even number of real prefabs, add emptyPrefab to make total odd
        if (itemPrefabs.Count % 2 == 0)
        {
            itemPrefabs.Add(emptyPrefab);
        }

        // Now total is odd
        _finalCount = itemPrefabs.Count;
        _centerIndex = _finalCount / 2;       // The center
        _leftmostIndex = 0;
        _rightmostIndex = _finalCount - 1;

        GeneratePositions();
        InstantiateInitialItems();
    }

    /// <summary>
    /// Generate _finalCount positions horizontally, with the center at _centerIndex.
    /// Example: if _finalCount=5, indices=0..4 => centerIndex=2 => offsets: -2, -1, 0, +1, +2
    /// </summary>
    private void GeneratePositions()
    {
        _positions.Clear();

        Vector2 centerPos = centerRect.anchoredPosition;

        for (int i = 0; i < _finalCount; i++)
        {
            int offset = i - _centerIndex;
            float newX = centerPos.x + offset * spacing;
            _positions.Add(new Vector2(newX, centerPos.y));
        }
    }

    /// <summary>
    /// Instantiate _finalCount items in _positions. The center one is bigger, the others smaller.
    /// </summary>
    private void InstantiateInitialItems()
    {
        for (int i = 0; i < _finalCount; i++)
        {
            GameObject prefab = itemPrefabs[i];
            RectTransform item = CreateItem(prefab, _positions[i]);

            // Center => focused scale
            if (i == _centerIndex)
            {
                item.localScale = Vector3.one * focusedItemSize;
            }
            else
            {
                item.localScale = Vector3.one * sideItemsSize;
            }

            _items.Add(item);
        }
    }

    public void ScrollLeft()
    {
        if (_items.Count == 0) return;

        // 1) Animate the leftmost item off-screen & destroy
        RectTransform leftItem = _items[0];
        GameObject originalPrefab = leftItem.GetComponent<ItemIdentity>().originalPrefab;

        AnimateOutAndDestroy(leftItem, new Vector2(offScreenLeftX, leftItem.anchoredPosition.y));

        // 2) Shift [1..end] left by 1
        for (int i = 1; i < _items.Count; i++)
        {
            RectTransform current = _items[i];
            int newPosIndex = i - 1;

            Sequence shiftSeq = DOTween.Sequence();
            shiftSeq.Join(
                current.DOAnchorPos(_positions[newPosIndex], replacingDuration).SetEase(easeType)
            );

            float newScale = (newPosIndex == _centerIndex) ? focusedItemSize : sideItemsSize;
            shiftSeq.Join(
                current.DOScale(newScale, resizingDuration).SetEase(easeType)
            );
        }

        // Remove from the list
        _items.RemoveAt(0);

        // 3) Re-instantiate the same prefab on the right
        RectTransform newItem = CreateItem(originalPrefab, new Vector2(offScreenRightX, _positions[_rightmostIndex].y));

        Sequence newItemSeq = DOTween.Sequence();
        newItemSeq.Join(
            newItem.DOAnchorPos(_positions[_rightmostIndex], replacingDuration).SetEase(easeType)
        );
        newItem.localScale = Vector3.one * sideItemsSize;
        newItemSeq.Join(
            newItem.DOScale(sideItemsSize, resizingDuration).SetEase(easeType)
        );

        _items.Add(newItem);

        // 4) If the center is emptyPrefab, do an extra scroll to skip it
        CheckCenterAndSkip(() => ScrollLeft());
    }

    public void ScrollRight()
    {
        if (_items.Count == 0) return;

        // 1) Animate the rightmost item off-screen & destroy
        RectTransform rightItem = _items[_items.Count - 1];
        GameObject originalPrefab = rightItem.GetComponent<ItemIdentity>().originalPrefab;

        AnimateOutAndDestroy(rightItem, new Vector2(offScreenRightX, rightItem.anchoredPosition.y));

        // 2) Shift [0..(count-2)] right by 1
        for (int i = _items.Count - 2; i >= 0; i--)
        {
            RectTransform current = _items[i];
            int newPosIndex = i + 1;

            Sequence shiftSeq = DOTween.Sequence();
            shiftSeq.Join(
                current.DOAnchorPos(_positions[newPosIndex], replacingDuration).SetEase(easeType)
            );

            float newScale = (newPosIndex == _centerIndex) ? focusedItemSize : sideItemsSize;
            shiftSeq.Join(
                current.DOScale(newScale, resizingDuration).SetEase(easeType)
            );
        }

        // Remove from the list
        _items.RemoveAt(_items.Count - 1);

        // 3) Re-instantiate the same prefab on the left
        RectTransform newItem = CreateItem(originalPrefab, new Vector2(offScreenLeftX, _positions[_leftmostIndex].y));

        Sequence newItemSeq = DOTween.Sequence();
        newItemSeq.Join(
            newItem.DOAnchorPos(_positions[_leftmostIndex], replacingDuration).SetEase(easeType)
        );
        newItem.localScale = Vector3.one * sideItemsSize;
        newItemSeq.Join(
            newItem.DOScale(sideItemsSize, resizingDuration).SetEase(easeType)
        );

        _items.Insert(0, newItem);

        // 4) If the center is emptyPrefab, do an extra scroll to skip it
        CheckCenterAndSkip(() => ScrollRight());
    }

    /// <summary>
    /// Creates an item at anchoredPos. Ensures there's an ItemIdentity to store originalPrefab.
    /// </summary>
    RectTransform CreateItem(GameObject prefab, Vector2 anchoredPos)
    {
        GameObject go = Instantiate(prefab, centerRect.parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;

        // Ensure ItemIdentity
        ItemIdentity identity = go.GetComponent<ItemIdentity>();
        if (!identity)
        {
            identity = go.AddComponent<ItemIdentity>();
        }
        identity.originalPrefab = prefab;

        return rect;
    }

    /// <summary>
    /// Animate an item off-screen, then destroy it.
    /// </summary>
    void AnimateOutAndDestroy(RectTransform item, Vector2 targetPos)
    {
        Sequence seq = DOTween.Sequence();
        seq.Join(
            item.DOAnchorPos(targetPos, replacingDuration).SetEase(easeType)
        );
        seq.OnComplete(() => Destroy(item.gameObject));
    }

    /// <summary>
    /// Checks if the current center item is emptyPrefab. If so,
    /// performs one extra scroll (left or right) to skip it.
    /// 
    /// Uses a simple boolean guard _skipCenterCheck to prevent infinite recursion.
    /// If we scroll once more and the center is STILL empty, there's no further recursion.
    /// </summary>
    void CheckCenterAndSkip(System.Action doAnotherScroll)
    {
        // If we already did a second scroll, do nothing (avoid infinite loops).
        if (_skipCenterCheck) return;

        if (_items.Count > _centerIndex)
        {
            // Check the center item
            var centerItem = _items[_centerIndex];
            var centerPrefab = centerItem.GetComponent<ItemIdentity>().originalPrefab;

            if (centerPrefab == emptyPrefab)
            {
                // We found the empty in center => do a second scroll
                _skipCenterCheck = true;
                doAnotherScroll.Invoke();
                _skipCenterCheck = false;
            }
        }
    }
}
