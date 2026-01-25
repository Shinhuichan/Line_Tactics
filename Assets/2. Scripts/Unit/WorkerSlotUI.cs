using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorkerSlotUI : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI baseNameText;
    public TextMeshProUGUI workerCountText; 
    public Button minusBtn;
    public Button plusBtn;
    public Button ironBtn;
    public Button oilBtn;
    public Button idleBtn;
    public Button repairBtn; // 🛠️ [신규] 수리 버튼 추가 (Inspector에서 연결 필요)

    [Header("색상 설정")]
    public Color activeColor = Color.green;
    public Color inactiveColor = Color.white;
    public Color disabledColor = Color.gray;

    private BaseController targetBase;

    public void Setup(BaseController baseCtrl)
    {
        targetBase = baseCtrl;
        baseNameText.text = baseCtrl.isOutpost ? "Outpost" : "Main Base";
        
        // 🛠️ [디버그] 버튼 연결 시 로그 출력
        // Debug.Log($"[UI] 슬롯 생성됨: {baseCtrl.name}");

        // 버튼 리스너 연결 (람다식으로 연결하여 안전하게 처리)
        SetupButton(minusBtn, () => WorkerDashboardManager.I.OnMinusClick(targetBase), "(-)");
        SetupButton(plusBtn, () => WorkerDashboardManager.I.OnPlusClick(targetBase), "(+)");
        SetupButton(ironBtn, () => WorkerDashboardManager.I.OnTaskChange(targetBase, BaseTask.Iron), "Iron");
        SetupButton(oilBtn, () => WorkerDashboardManager.I.OnTaskChange(targetBase, BaseTask.Oil), "Oil");
        SetupButton(idleBtn, () => WorkerDashboardManager.I.OnTaskChange(targetBase, BaseTask.Idle), "Idle");
        // 🛠️ [신규] 수리 버튼 연결
        if (repairBtn != null)
        {
            SetupButton(repairBtn, () => WorkerDashboardManager.I.OnRepairClick(targetBase), "Repair");
        }
    }

    void SetupButton(Button btn, UnityEngine.Events.UnityAction action, string btnName)
    {
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => 
        {
            Debug.Log($"🖱️ [클릭] {targetBase.name}의 {btnName} 버튼 눌림!"); // 👈 클릭되면 이 로그가 떠야 함
            action.Invoke();
        });
    }

    public void Refresh()
    {
        if (targetBase == null) return;

        // 1. 인원 수 표시
        int arrived = 0;
        int total = targetBase.assignedWorkers.Count;
        foreach(var w in targetBase.assignedWorkers)
        {
            if (w != null && Vector3.Distance(w.transform.position, targetBase.transform.position) <= 5.0f) 
                arrived++; 
        }
        int incoming = total - arrived;
        workerCountText.text = incoming > 0 ? $"{arrived} (+{incoming})" : $"{arrived}";

        // 2. 버튼 상태 갱신
        // 🌟 [수정] 대문자 Property 사용! (HasIronNear)
        // 이제 리스트에 자원이 1개라도 있으면 무조건 버튼이 활성화됩니다.
        SetButtonState(ironBtn, BaseTask.Iron, targetBase.HasIronNear);
        SetButtonState(oilBtn, BaseTask.Oil, targetBase.HasOilNear);
        SetButtonState(idleBtn, BaseTask.Idle, true);
        // 🛠️ [신규] 수리 버튼 활성화 로직
        // 조건: 
        // 1. 체력이 50 이상 깎였는가?
        // 2. 자원(Iron 5)이 충분한가?
        // 3. 이미 수리 중인 노동자가 없는가?
        // 4. 농성 중(Siege)이 아닌가?
        if (repairBtn != null)
        {
            bool hpCondition = (targetBase.currentHP <= targetBase.maxHP - 50f);
            
            bool resourceCondition = false;
            if (ResourceManager.I != null) resourceCondition = ResourceManager.I.CheckCost(5, 0);

            bool notRepairing = !targetBase.IsBeingRepaired;
            
            bool notSiege = true;
            if (TacticalCommandManager.I != null && TacticalCommandManager.I.currentState == TacticalState.Siege)
                notSiege = false;

            // 최종 활성화 여부
            bool canRepair = hpCondition && resourceCondition && notRepairing && notSiege;
            
            repairBtn.interactable = canRepair;
            repairBtn.image.color = canRepair ? inactiveColor : disabledColor;
        }
    }

    void SetButtonState(Button btn, BaseTask taskType, bool isAvailable)
    {
        // 1. 클릭 가능 여부 설정
        btn.interactable = isAvailable;

        // 2. 색상 설정 (현재 선택된 태스크면 초록색)
        if (!isAvailable)
        {
            btn.image.color = disabledColor;
        }
        else
        {
            btn.image.color = (targetBase.currentTask == taskType) ? activeColor : inactiveColor;
        }
    }

    
}