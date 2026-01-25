using UnityEngine;
using TMPro;

public class TacticalCommandManager : SingletonBehaviour<TacticalCommandManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("현재 전술 상태")]
    public TacticalState currentState = TacticalState.Defend; 

    [Header("집결지 제어 (Rally Point)")]
    public int currentRallyIndex = 0; // 0 = 내 기지
    public Transform currentRallyPoint; // 실제 목표 Transform

    [Header("UI 연결")]
    public TextMeshProUGUI statusText; 

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        UpdateRallyPoint();
        UpdateUI();
    }

    // ▶ [UI 버튼] 전진
    public void OrderAdvance()
    {
        if (ConstructionManager.I == null) return;
        
        if (currentRallyIndex < ConstructionManager.I.tacticalPoints.Count - 1)
        {
            currentRallyIndex++;
            currentState = TacticalState.Defend; // 이동 시 기본 상태는 Defend
            UpdateRallyPoint();
            ShowMessage("전군 전진!");
        }
        else
        {
            ShowMessage("더 이상 전진할 수 없습니다!");
        }
    }

    // ◀ [UI 버튼] 후퇴
    public void OrderRetreat()
    {
        if (currentRallyIndex > 0)
        {
            currentRallyIndex--;
            currentState = TacticalState.Defend;
            UpdateRallyPoint();
            ShowMessage("전군 후퇴!");
        }
    }

    // 🏰 [UI 버튼] 농성 토글
    public void ToggleSiegeMode()
    {
        if (currentState == TacticalState.Defend || currentState == TacticalState.Attack)
        {
            currentState = TacticalState.Siege;
            ShowMessage("현재 지역에서 농성 모드 돌입!");
        }
        else
        {
            currentState = TacticalState.Defend;
            ShowMessage("농성 해제! 진형을 유지합니다.");
        }
        UpdateUI();
    }

    // 🤖 [신규] 봇 전용 강제 명령 함수 (이게 없어서 오류가 났습니다)
    public void SetState(TacticalState newState)
    {
        // 상태 변경
        currentState = newState;

        // (옵션) 봇이 '공격' 명령을 내리면, 자동으로 다음 거점으로 전진하게 할 수도 있습니다.
        // 현재는 단순히 상태값만 바꾸고 UI를 갱신합니다.
        
        UpdateUI();
    }

    void UpdateRallyPoint()
    {
        if (ConstructionManager.I != null && ConstructionManager.I.tacticalPoints.Count > 0)
        {
            currentRallyPoint = ConstructionManager.I.tacticalPoints[currentRallyIndex];
        }
        UpdateUI();
    }

    void UpdateUI()
    {
        if (statusText != null)
        {
            string locName = (currentRallyPoint != null) ? currentRallyPoint.name : "Unknown";
            
            string stateStr = "";
            switch (currentState)
            {
                case TacticalState.Defend: stateStr = "<color=green>이동/대기</color>"; break;
                case TacticalState.Siege: stateStr = "<color=orange>농성 중</color>"; break;
                case TacticalState.Attack: stateStr = "<color=red>공격(Bot)</color>"; break;
            }

            statusText.text = $"목표: {locName}\n상태: {stateStr}";
        }
    }

    void ShowMessage(string msg)
    {
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(Vector3.zero, msg, Color.white, 40);
        Debug.Log(msg);
    }
}