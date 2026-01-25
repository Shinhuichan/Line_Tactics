using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [Header("Target")]
    [SerializeField] private RectTransform targetRect; // 이동시킬 패널

    [Header("Drag Settings")]
    [SerializeField] private bool keepInScreen = true;
    [Range(0.1f, 1f)] [SerializeField] private float dragAlpha = 0.8f;

    [Header("Scale Settings")]
    [SerializeField] private float minimizedScale = 0.7f; // ScaleDown 시 비율
    [SerializeField] private float maximizedScale = 1.2f; // ScaleUp 시 비율
    
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalLocalPointerPosition;
    private Vector2 _originalPanelLocalPosition;
    
    // 🌟 [복구] 원래 크기 기억용 변수
    private Vector3 _defaultScale;

    private void Awake()
    {
        if (targetRect == null) targetRect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        
        // 최상위 캔버스 찾기
        if (_canvas != null && _canvas.rootCanvas != null) _canvas = _canvas.rootCanvas;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 시작 시 스케일 저장
        _defaultScale = targetRect.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetRect.SetAsLastSibling(); // 맨 앞으로 가져오기
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect.parent as RectTransform, 
            eventData.position, 
            _canvas.worldCamera, 
            out _originalLocalPointerPosition
        );
        
        _originalPanelLocalPosition = targetRect.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = dragAlpha;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetRect == null || _canvas == null) return;

        Vector2 localPointerPosition;
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect.parent as RectTransform,
            eventData.position,
            _canvas.worldCamera,
            out localPointerPosition
        ))
        {
            Vector2 offsetToOriginal = localPointerPosition - _originalLocalPointerPosition;
            targetRect.anchoredPosition = _originalPanelLocalPosition + offsetToOriginal;

            if (keepInScreen) ClampToWindow();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = 1.0f;
    }

    // =========================================================
    // 🌟 [기능 복구] Scale 관련 함수들
    // =========================================================

    public void ScaleDown()
    {
        targetRect.localScale = _defaultScale * minimizedScale;
        if (keepInScreen) ClampToWindow(); // 크기가 변했으니 위치 재조정
    }

    public void ScaleUp()
    {
        targetRect.localScale = _defaultScale * maximizedScale;
        if (keepInScreen) ClampToWindow();
    }

    public void ScaleReset()
    {
        targetRect.localScale = _defaultScale;
        if (keepInScreen) ClampToWindow();
    }

    // =========================================================

    // 🌟 [핵심 수정] 캔버스 사이즈 + 현재 스케일 고려하여 가두기
    private void ClampToWindow()
    {
        RectTransform parentRect = targetRect.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 size = targetRect.rect.size;
        Vector2 pivot = targetRect.pivot;
        
        // ⚠️ 스케일이 변하면 차지하는 실제 영역도 변하므로 localScale을 곱해줘야 함
        Vector3 currentScale = targetRect.localScale;
        
        Rect parentBounds = parentRect.rect;
        Vector2 pos = targetRect.anchoredPosition;

        // 스케일이 적용된 실제 너비/높이 계산
        float effectiveWidth = size.x * currentScale.x;
        float effectiveHeight = size.y * currentScale.y;

        // 좌표 제한 계산
        float minX = parentBounds.xMin + (effectiveWidth * pivot.x);
        float maxX = parentBounds.xMax - (effectiveWidth * (1 - pivot.x));
        float minY = parentBounds.yMin + (effectiveHeight * pivot.y);
        float maxY = parentBounds.yMax - (effectiveHeight * (1 - pivot.y));

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        targetRect.anchoredPosition = pos;
    }
}