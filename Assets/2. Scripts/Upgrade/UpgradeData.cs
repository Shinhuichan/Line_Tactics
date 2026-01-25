using UnityEngine;
using System.Collections.Generic;

// 적용 대상 그룹
public enum UpgradeTargetType
{
    AllUnits,       // 모든 유닛
    MeleeUnits,     // 근거리
    RangedUnits,    // 원거리
    SpecificUnit,   // 특정 유닛
    Base,           // 기지
}

// 업그레이드 종류
public enum UpgradeEffectType
{
    StatBoost,      // 수치 증가
    UnlockAbility,  // 기능 해금
    UnlockUnit      // 유닛 해금
}

public enum StatType
{
    None,
    AttackDamage,
    Defense,
    MaxHP,
    MoveSpeed,
    AttackRange,
    WorkSpeed
}

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Game Data/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("기본 정보")]
    public string id; 
    public string upgradeName;
    public Sprite icon;
    [TextArea] public string description;

    // 🧬 [복구] 종족 필터링을 위한 필수 데이터
    [Header("종족 조건")]
    public UnitRace raceRequirement; // 어느 종족 전용인가?
    public bool isCommonUpgrade;     // 체크하면 종족 상관없이 모두 표시

    [Header("비용 및 조건")]
    public int ironCost;
    public int oilCost;
    public float researchTime; 
    public List<UpgradeData> preRequisites; 

    [Header("효과 설정")]
    public UpgradeTargetType targetType;
    public UnitType specificUnit; 
    public UpgradeEffectType effectType;
    
    [Header("능력 해금 (UnlockAbility용)")]
    public string specialAbilityKey; 

    [Header("스탯 부스트 (StatBoost용)")]
    public StatType statType;
    public float value;
}