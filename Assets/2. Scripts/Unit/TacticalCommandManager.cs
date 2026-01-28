using UnityEngine;
using TMPro;

// 🌟 [핵심 수정] Enum 정의를 이곳으로 이동 (모든 스크립트에서 참조 가능하도록)
public enum TacticalState { Defend, Attack, Siege }

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

    // 🤖 [신규] 봇 전용: 특정 거점 인덱스로 즉시 이동 명령
    // Bot이 "아, 저기 Outpost가 지어졌으니 저기로 집결하자"라고 판단할 때 사용
    public void SetRallyPointByIndex(int index)
    {
        if (ConstructionManager.I == null) return;
        if (index < 0 || index >= ConstructionManager.I.tacticalPoints.Count) return;

        // 이미 거기가 목표라면 무시 (중복 명령 방지)
        if (currentRallyIndex == index) return;

        currentRallyIndex = index;
        // 봇은 이동 시 기본적으로 Defend(진형 유지 이동) 상태를 유지
        if (currentState != TacticalState.Siege) 
        {
            currentState = TacticalState.Defend;
        }
        
        UpdateRallyPoint();
        Debug.Log($"🤖 Bot Command: Rally Point Moved to Index {index}");
    }

    // 🤖 [신규] 봇 전용 강제 명령 함수
    public void SetState(TacticalState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
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