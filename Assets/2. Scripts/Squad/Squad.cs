using UnityEngine;
using System.Collections.Generic;

public enum SquadState
{
    Drafting, // 편성 중 (유닛 징집 안 함)
    Active    // 활동 중 (유닛 징집 및 이동)
}

[System.Serializable]
public class Squad
{
    public int squadID;
    public string squadName;
    public List<CombatSlot> slots = new List<CombatSlot>();
    
    // 🌟 [신규] 분대 상태
    public SquadState state = SquadState.Drafting;

    // 현재 분대의 목표 지점
    public Vector3? currentCommandTarget = null;

    public Squad(int id)
    {
        squadID = id;
        squadName = $"Squad {id + 1}";
        state = SquadState.Drafting; // 처음엔 편성 모드
    }

    public void AddSlot(UnitType type)
    {
        slots.Add(new CombatSlot(type));
    }

    // 🌟 [신규] 출동 명령 (UI 버튼에서 호출)
    public void ActivateSquad()
    {
        state = SquadState.Active;
        Debug.Log($"{squadName} 출동! 유닛 모집 시작.");
    }

    public void CommandMove(Vector3 target)
    {
        // 활동 중일 때만 명령 가능
        if (state != SquadState.Active) return;

        currentCommandTarget = target;
        foreach (var slot in slots)
        {
            if (slot.IsFilled)
            {
                slot.assignedUnit.isManualMove = true;
                // 이동 로직 호출...
            }
        }
    }
}