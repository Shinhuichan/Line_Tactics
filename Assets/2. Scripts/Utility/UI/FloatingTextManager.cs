using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingTextManager : SingletonBehaviour<FloatingTextManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("Settings")]
    public GameObject textPrefab; 
    public Transform floatingPointTransform; // Canvas (RectTransform)
    public float floatSpeed = 50f; 
    public float duration = 1.0f;

    public void ShowMoneyPopup(Vector3 position, long amount)
    {
        string text = amount > 0 ? $"+{NumberUtils.ToCurrencyString(amount)}원" : $"{NumberUtils.ToCurrencyString(amount)}원";
        Color color = amount > 0 ? Color.red : Color.blue; 
        ShowText(position, text, color);
    }

    public void ShowText(Vector3 worldPosition, string content, Color color, int fontSize = 0)
    {
        if (textPrefab == null) return;

        // 부모 Transform 결정 (Canvas)
        Transform parent = floatingPointTransform != null ? floatingPointTransform : transform;
        
        GameObject obj = Instantiate(textPrefab, parent);
        RectTransform rectTransform = obj.GetComponent<RectTransform>();

        // 🌟 [핵심 수정] 월드 좌표 -> 스크린 좌표 -> 캔버스 로컬 좌표로 변환
        // 이 방식을 써야 해상도, 캔버스 모드(Overlay/Camera), 스케일러 설정에 상관없이 정확한 위치에 찍힙니다.
        if (Camera.main != null && rectTransform != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            
            // 카메라 뒤쪽(Z < 0)에 있는 경우 텍스트 표시 안 함
            if (screenPos.z < 0) 
            {
                Destroy(obj);
                return;
            }

            // 부모가 Canvas(RectTransform)라고 가정하고 로컬 좌표 구하기
            RectTransform parentRect = parent.GetComponent<RectTransform>();
            Vector2 localPos;
            
            // Overlay 모드일 경우 카메라는 null을 넣어야 함
            // (혹시 나중에 Camera 모드로 바꿔도 작동하도록 분기 처리 가능하지만, 현재 Overlay이므로 null 권장)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, 
                screenPos, 
                null, // Screen Space - Overlay 모드라면 null 필수!
                out localPos
            );

            rectTransform.anchoredPosition = localPos;
            
            // 🌟 Z축을 0으로 완벽 고정 (가장 중요)
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);
        }
        else
        {
            // 예외 상황: 그냥 월드 좌표 대입
            obj.transform.position = worldPosition;
        }

        // 스케일 초기화 (1,1,1)
        obj.transform.localScale = Vector3.one;

        // 맨 앞으로 가져오기
        obj.transform.SetAsLastSibling();

        TextMeshProUGUI tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (tmp != null)
        {
            tmp.text = content;
            tmp.color = color;
            
            if (fontSize > 0) tmp.fontSize = fontSize;

            StartCoroutine(AnimateText(obj, tmp));
        }
        else
        {
            Destroy(obj);
        }
    }

    IEnumerator AnimateText(GameObject obj, TextMeshProUGUI tmp)
    {
        float elapsed = 0f;
        
        // 이동을 위해 anchoredPosition 사용
        RectTransform rt = obj.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            if (obj == null) yield break;

            elapsed += Time.unscaledDeltaTime; 
            
            // 위로 이동 (Y축 증가)
            rt.anchoredPosition = startPos + new Vector2(0f, floatSpeed * elapsed);
            
            // 투명도 감소
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        if (obj != null) Destroy(obj); 
    }
}