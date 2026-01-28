using UnityEngine;

// 🌟 [신규] 적군(Enemy) 전용 전술 지휘 매니저 (Player의 TacticalCommandManager와 동일 구조)
public class EnemyCommandManager : SingletonBehaviour<EnemyCommandManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("적군 전술 상태")]
    public TacticalState currentState = TacticalState.Defend;

    [Header("디버그 정보")]
    public string debugStatus;

    void Update()
    {
        debugStatus = currentState.ToString();
    }

    // 🤖 Bot 로직에서 상태를 변경할 때 호출
    public void SetState(TacticalState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        Debug.Log($"⚔️ [EnemyCommandManager] State Changed: {currentState}");

        // (확장 가능) 상태 변경 시 이벤트 호출 등
    }
}