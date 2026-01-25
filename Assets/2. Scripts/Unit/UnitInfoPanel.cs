using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text; 
using System.Collections;

public class UnitInfoPanel : SingletonBehaviour<UnitInfoPanel>
{
    protected override bool IsDontDestroy() => false;

    [Header("패널 제어")]
    public GameObject panelRoot;
    public CanvasGroup canvasGroup; 
    public float fadeSpeed = 10f;

    [Header("UI 요소 연결")]
    public Image unitIcon;           
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI statsText;
    public TextMeshProUGUI costText; 

    [Header("디자인 설정 (Hex Color)")]
    private string colorLabel = "#A0A0A0"; 
    private string colorValue = "#000000ff"; 
    private string colorIron  = "#C0C0C0"; 
    private string colorOil   = "#ff0000ff"; 

    private Coroutine fadeCoroutine;

    void Start()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ========================================================================
    // 1️⃣ 기존 기능 유지: 유닛 데이터 표시
    // ========================================================================
    public void ShowUnitInfo(UnitData data)
    {
        if (data == null) return;

        // 비용 텍스트 생성
        string ironStr = $"<color={colorIron}>철재: {data.ironCost}</color>";
        string oilStr = data.oilCost > 0 ? $"   <color={colorOil}>기름: {data.oilCost}</color>" : "";
        string costInfo = $"{ironStr}{oilStr}";

        // 스탯 텍스트 생성
        StringBuilder sb = new StringBuilder();
        sb.Append(FormatStat("HP", data.hp));
        sb.Append("   "); 
        sb.Append(FormatStat("DEF", data.defense));
        sb.AppendLine();

        sb.Append(FormatStat("ATK", data.attackDamage));
        sb.Append("   ");
        sb.Append(FormatStat("SPD", data.moveSpeed));
        sb.AppendLine();

        sb.Append(FormatStat("RNG", data.attackRange));
        sb.Append("   ");
        sb.Append(FormatStat("CD", data.attackCooldown, "s"));

        // 공용 함수 호출
        ShowGenericInfo(data.unitName, data.icon, sb.ToString(), costInfo);
    }

    // ========================================================================
    // 2️⃣ [신규] 자원 노드 정보 표시
    // ========================================================================
    public void ShowResourceInfo(ResourceNode node)
    {
        if (node == null) return;

        // 아이콘이 없으면 표시하지 않거나 기본값 처리 (여기선 예외처리)
        if (node.icon == null) 
        {
            // Debug.LogWarning($"ResourceNode {node.nodeName} has no icon assigned!");
        }

        StringBuilder sb = new StringBuilder();
        
        // 자원 타입에 따라 색상 다르게
        string amountColor = (node.resourceType == ResourceType.Oil) ? colorOil : colorIron;
        
        sb.AppendLine($"<size=90%>Type: {node.resourceType}</size>");
        sb.AppendLine(); // 줄바꿈
        sb.Append($"Amount: <color={amountColor}><b>{node.currentAmount}</b></color> / {node.maxAmount}");

        // 비용 정보는 없음
        ShowGenericInfo(node.nodeName, node.icon, sb.ToString(), "");
    }

    // ========================================================================
    // 3️⃣ [신규] 기지(Base/Outpost) 정보 표시
    // ========================================================================
    public void ShowBaseInfo(BaseController baseCtrl)
    {
        if (baseCtrl == null) return;

        StringBuilder sb = new StringBuilder();

        // 1. 상태 표시 (건설중 / 완료)
        if (!baseCtrl.isConstructed)
        {
            sb.AppendLine($"<color=orange>Constructing...</color>");
            sb.AppendLine($"Progress: <color=white>{(baseCtrl.currentProgress * 100):F0}%</color>");
        }
        else
        {
            // 2. 체력 표시
            sb.Append(FormatStat("HP", baseCtrl.currentHP, $" / {baseCtrl.maxHP}"));
            sb.AppendLine();

            // 3. 작업 상태 or 주둔 병력
            if (baseCtrl.garrisonedUnits.Count > 0)
            {
                 sb.AppendLine($"<color=#FFA500>Garrison: {baseCtrl.garrisonedUnits.Count} units</color>");
            }
            else
            {
                sb.AppendLine($"State: {baseCtrl.currentTask}");
            }
        }
        
        // 4. 할당된 일꾼 수
        sb.AppendLine($"Workers: <color=white>{baseCtrl.assignedWorkers.Count}</color>");

        ShowGenericInfo(baseCtrl.baseName, baseCtrl.icon, sb.ToString(), "");
    }


    // ========================================================================
    // 🛠️ [공용] 내부 정보 갱신 및 패널 활성화
    // ========================================================================
    private void ShowGenericInfo(string title, Sprite icon, string mainText, string bottomText)
    {
        // 1. 패널 활성화 및 페이드 인
        if (panelRoot != null) panelRoot.SetActive(true);
        if (canvasGroup != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        // 2. 데이터 바인딩
        if (nameText != null) nameText.text = title;
        if (unitIcon != null)
        {
            unitIcon.sprite = icon;
            unitIcon.enabled = (icon != null);
        }

        // 스탯 부분 (HTML 태그가 포함된 문자열 그대로 적용)
        if (statsText != null) statsText.text = mainText;

        // 하단 텍스트 (비용 등)
        if (costText != null) costText.text = bottomText;
    }

    // 헬퍼: 스탯 문자열 포맷팅
    string FormatStat(string label, float value, string suffix = "")
    {
        return $"<color={colorLabel}>{label}</color> <color={colorValue}>{value}{suffix}</color>";
    }

    public void HideInfo()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    IEnumerator FadeIn()
    {
        canvasGroup.blocksRaycasts = false; 
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fadeSpeed;
            canvasGroup.alpha = t;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }
}