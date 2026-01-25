using UnityEngine;

[System.Serializable]
public class CombatSlot
{
    public UnitType requiredType;   // 플레이어가 원한 병과
    public UnitController assignedUnit; // 실제 배속된 유닛
    public bool IsFilled => assignedUnit != null;

    public CombatSlot(UnitType type)
    {
        requiredType = type;
        assignedUnit = null;
    }
}