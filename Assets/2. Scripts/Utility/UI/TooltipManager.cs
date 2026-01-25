using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TooltipManager : SingletonBehaviour<TooltipManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("UI Components")]
    public RectTransform tooltipRect;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI contentText;
    public CanvasGroup canvasGroup; 

    [Header("Settings")]
    public Vector2 offset = new Vector2(25f, 25f);

    private bool _isLocked = false;

    protected override void Awake()
    {
        base.Awake(); 

        if (tooltipRect != null)
        {
            // 1. 일단 켭니다. (이 순간 화면에 보일 수 있음)
            if (!tooltipRect.gameObject.activeSelf)
                tooltipRect.gameObject.SetActive(true);

            // 2. CanvasGroup 세팅
            if (canvasGroup == null)
                canvasGroup = tooltipRect.GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
                canvasGroup = tooltipRect.gameObject.AddComponent<CanvasGroup>();

            // 🌟 [핵심 수정] 켜자마자 바로 투명하게 만듭니다. (Start까지 기다리지 않음)
            canvasGroup.alpha = 0f;
            
            // 🌟 [추가] 투명할 때 마우스 클릭을 가로채지 않도록 설정 (중요)
            canvasGroup.blocksRaycasts = false; 
            canvasGroup.interactable = false;
        }
    }

    private void Start()
    {
        // Awake에서 이미 숨겼으므로 Start는 비워도 되지만, 
        // 확실하게 하기 위해 남겨둬도 상관없습니다.
        Hide();
    }

    private void Update()
    {
        if (canvasGroup == null) return;

        if (canvasGroup.alpha > 0)
        {
            Vector2 mousePos = Input.mousePosition;
            
            float pivotX = mousePos.x / Screen.width;
            float pivotY = mousePos.y / Screen.height;
            tooltipRect.pivot = new Vector2(pivotX, pivotY);

            float offsetX = (pivotX < 0.5f) ? offset.x : -offset.x;
            float offsetY = (pivotY < 0.5f) ? offset.y : -offset.y;

            tooltipRect.transform.position = mousePos + new Vector2(offsetX, offsetY);
        }
    }

    public void Show(string content, string header = "", bool lockTooltip = false)
    {
        if (_isLocked) return;
        if (canvasGroup == null) return;

        // 혹시 꺼져있다면 켭니다
        if (!tooltipRect.gameObject.activeSelf)
            tooltipRect.gameObject.SetActive(true);

        _isLocked = lockTooltip;

        tooltipRect.transform.SetAsLastSibling();

        if (string.IsNullOrEmpty(header))
        {
            headerText.gameObject.SetActive(false);
        }
        else
        {
            headerText.gameObject.SetActive(true);
            headerText.text = header;
        }

        contentText.text = content;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        
        canvasGroup.alpha = 1f; // 보이게 설정
        // 툴팁은 보통 클릭되지 않으므로 blocksRaycasts는 false 유지하는 게 좋지만,
        // 필요하다면 여기서 true로 바꿀 수 있습니다. (보통은 false 권장)
    }

    public void Hide()
    {
        if (_isLocked) return;
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f; // 안 보이게 설정
    }
    
    public void ForceHide()
    {
        _isLocked = false;
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
    }
}   