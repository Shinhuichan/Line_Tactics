using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UniversalHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Settings")]
    public bool isUI = true;
    [Tooltip("이 ID값은 호버 이벤트 발생 시 매개변수로 전달됩니다. (예: Humanic, Demonic)")]
    public string id; 

    [Header("Visual Effects")]
    [SerializeField] private float hoverScale = 1.1f;
    private Vector3 originalScale;
    
    // UI용
    private Image targetImage;
    private Color originalColor;
    [SerializeField] private Color hoverColor = Color.white;

    // 3D용
    private Renderer rend;
    private Material originalMaterial;
    [SerializeField] private Material outlineMaterial;

    [Header("Events")]
    // 🌟 [핵심] 호버 시 ID(문자열)를 보냅니다. RaceSelectionUI와 연결하기 위함입니다.
    public UnityEvent<string> onHoverEnter; 
    public UnityEvent onHoverExit;
    public UnityEvent<string> onClick;

    void Start()
    {
        if (isUI)
        {
            targetImage = GetComponent<Image>();
            originalScale = transform.localScale;
            if (targetImage != null) originalColor = targetImage.color;
        }
        else
        {
            rend = GetComponent<Renderer>();
            if (rend != null) originalMaterial = rend.material;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 1. 시각 효과
        if (isUI)
        {
            transform.localScale = originalScale * hoverScale;
            if (targetImage != null) targetImage.color = hoverColor;
        }
        else if (rend != null && outlineMaterial != null)
        {
            rend.material = outlineMaterial;
        }

        // 2. 이벤트 발생 (ID 전달)
        onHoverEnter?.Invoke(id);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 1. 원상 복구
        if (isUI)
        {
            transform.localScale = originalScale;
            if (targetImage != null) targetImage.color = originalColor;
        }
        else if (rend != null)
        {
            rend.material = originalMaterial;
        }

        // 2. 이벤트 발생
        onHoverExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭 이벤트 발생
        onClick?.Invoke(id);
    }
}