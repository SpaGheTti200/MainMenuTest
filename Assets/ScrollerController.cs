using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ScrollerController : MonoBehaviour
{
    public List<GameObject> itemPrefabs;      
    public GameObject emptyPrefab;           
    public RectTransform centerRect;

    public float spacing = 200f;

    public float focusedItemSize = 2f;
    public float sideItemsSize = 1f;

    
    public float replacingDuration = 0.5f;
    public float resizingDuration = 0.5f;
    
    public Ease replacingEaseType = Ease.OutCubic;
    public Ease resizingEaseType = Ease.OutCubic;

    public float offScreenLeftX = -1000f;
    public float offScreenRightX = 1000f;

    private List<RectTransform> _items = new List<RectTransform>();
    private List<Vector2> _positions = new List<Vector2>();

    private int _finalCount;     
    private int _centerIndex;    
    private int _leftmostIndex;  
    private int _rightmostIndex;

    private bool _skipCenterCheck = false;

    private void Start()
    {
        if (itemPrefabs.Count % 2 == 0)
        {
            itemPrefabs.Add(emptyPrefab);
        }

        _finalCount = itemPrefabs.Count;
        _centerIndex = _finalCount / 2;       
        _leftmostIndex = 0;
        _rightmostIndex = _finalCount - 1;

        GeneratePositions();
        InstantiateInitialItems();
    }

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
    private void InstantiateInitialItems()
    {
        for (int i = 0; i < _finalCount; i++)
        {
            GameObject prefab = itemPrefabs[i];
            RectTransform item = CreateItem(prefab, _positions[i]);

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

        RectTransform leftItem = _items[0];
        GameObject originalPrefab = leftItem.GetComponent<ItemIdentity>().originalPrefab;

        AnimateOutAndDestroy(leftItem, new Vector2(offScreenLeftX, leftItem.anchoredPosition.y));

        for (int i = 1; i < _items.Count; i++)
        {
            RectTransform current = _items[i];
            int newPosIndex = i - 1;

            Sequence shiftSeq = DOTween.Sequence();
            shiftSeq.Join(
                current.DOAnchorPos(_positions[newPosIndex], replacingDuration).SetEase(replacingEaseType)
            );

            float newScale = (newPosIndex == _centerIndex) ? focusedItemSize : sideItemsSize;
            shiftSeq.Join(
                current.DOScale(newScale, resizingDuration).SetEase(resizingEaseType)
            );
        }

        _items.RemoveAt(0);

        RectTransform newItem = CreateItem(originalPrefab, new Vector2(offScreenRightX, _positions[_rightmostIndex].y));

        Sequence newItemSeq = DOTween.Sequence();
        newItemSeq.Join(
            newItem.DOAnchorPos(_positions[_rightmostIndex], replacingDuration).SetEase(replacingEaseType)
        );
        newItem.localScale = Vector3.one * sideItemsSize;
        newItemSeq.Join(
            newItem.DOScale(sideItemsSize, resizingDuration).SetEase(resizingEaseType)
        );

        _items.Add(newItem);

        CheckCenterAndSkip(() => ScrollLeft());
    }

    public void ScrollRight()
    {
        if (_items.Count == 0) return;

        RectTransform rightItem = _items[_items.Count - 1];
        GameObject originalPrefab = rightItem.GetComponent<ItemIdentity>().originalPrefab;

        AnimateOutAndDestroy(rightItem, new Vector2(offScreenRightX, rightItem.anchoredPosition.y));

        for (int i = _items.Count - 2; i >= 0; i--)
        {
            RectTransform current = _items[i];
            int newPosIndex = i + 1;

            Sequence shiftSeq = DOTween.Sequence();
            shiftSeq.Join(
                current.DOAnchorPos(_positions[newPosIndex], replacingDuration).SetEase(replacingEaseType)
            );

            float newScale = (newPosIndex == _centerIndex) ? focusedItemSize : sideItemsSize;
            shiftSeq.Join(
                current.DOScale(newScale, resizingDuration).SetEase(resizingEaseType)
            );
        }

        _items.RemoveAt(_items.Count - 1);

        RectTransform newItem = CreateItem(originalPrefab, new Vector2(offScreenLeftX, _positions[_leftmostIndex].y));

        Sequence newItemSeq = DOTween.Sequence();
        newItemSeq.Join(
            newItem.DOAnchorPos(_positions[_leftmostIndex], replacingDuration).SetEase(replacingEaseType)
        );
        newItem.localScale = Vector3.one * sideItemsSize;
        newItemSeq.Join(
            newItem.DOScale(sideItemsSize, resizingDuration).SetEase(resizingEaseType)
        );

        _items.Insert(0, newItem);

        CheckCenterAndSkip(() => ScrollRight());
    }

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

    void AnimateOutAndDestroy(RectTransform item, Vector2 targetPos)
    {
        Sequence seq = DOTween.Sequence();
        seq.Join(
            item.DOAnchorPos(targetPos, replacingDuration).SetEase(replacingEaseType)
        );
        seq.OnComplete(() => Destroy(item.gameObject));
    }

    void CheckCenterAndSkip(System.Action doAnotherScroll)
    {
        if (_skipCenterCheck) return;

        if (_items.Count > _centerIndex)
        {
            var centerItem = _items[_centerIndex];
            var centerPrefab = centerItem.GetComponent<ItemIdentity>().originalPrefab;

            if (centerPrefab == emptyPrefab)
            {
                _skipCenterCheck = true;
                doAnotherScroll.Invoke();
                _skipCenterCheck = false;
            }
        }
    }
}
