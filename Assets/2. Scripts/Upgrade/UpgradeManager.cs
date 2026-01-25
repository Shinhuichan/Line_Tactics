using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; // Keys.ToList() 사용을 위해 추가

public class UpgradeManager : SingletonBehaviour<UpgradeManager>
{
    protected override bool IsDontDestroy() => false;

    private class FactionData
    {
        public HashSet<string> unlockedUpgradeIds = new HashSet<string>();
        public HashSet<string> activeAbilityKeys = new HashSet<string>();
        public Dictionary<UnitType, Dictionary<StatType, float>> activeStatBonuses = new Dictionary<UnitType, Dictionary<StatType, float>>();
        
        // ⏳ [신규] 연구 중인 항목 (UpgradeID -> 남은 시간)
        public Dictionary<string, float> researchTimers = new Dictionary<string, float>();

        public FactionData()
        {
            foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
            {
                activeStatBonuses[type] = new Dictionary<StatType, float>();
            }
        }
    }

    private Dictionary<string, FactionData> factionDatabase = new Dictionary<string, FactionData>();
    public List<UpgradeData> allUpgrades;
    public event Action<string> OnUpgradeCompleted;

    protected override void Awake()
    {
        base.Awake();
        factionDatabase["Player"] = new FactionData();
        factionDatabase["Enemy"] = new FactionData();
    }

    // 🕒 [신규] 연구 타이머 업데이트
    void Update()
    {
        foreach (var factionPair in factionDatabase)
        {
            string teamTag = factionPair.Key;
            FactionData faction = factionPair.Value;

            if (faction.researchTimers.Count > 0)
            {
                // 딕셔너리 변경 중 오류 방지를 위해 키 리스트 복사
                List<string> keys = faction.researchTimers.Keys.ToList();
                foreach (var id in keys)
                {
                    faction.researchTimers[id] -= Time.deltaTime;
                    if (faction.researchTimers[id] <= 0f)
                    {
                        // 연구 완료!
                        faction.researchTimers.Remove(id);
                        CompleteResearch(id, teamTag);
                    }
                }
            }
        }
    }

    public void PurchaseUpgrade(UpgradeData data, string teamTag)
    {
        if (!factionDatabase.ContainsKey(teamTag)) return;

        // 1. 유효성 검사 (이미 완료, 연구 중, 선행 부족, 자원 부족)
        if (IsUnlocked(data, teamTag)) return;
        if (IsResearching(data, teamTag)) return; // 이미 연구 중

        bool canAfford = false;
        if (teamTag == "Player") canAfford = ResourceManager.I.CheckCost(data.ironCost, data.oilCost);
        else if (teamTag == "Enemy") canAfford = EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost);

        if (!canAfford)
        {
            if (teamTag == "Player") UIManager.I.ShowToast("자원이 부족합니다!");
            return;
        }

        if (!IsResearchable(data, teamTag))
        {
            if (teamTag == "Player") UIManager.I.ShowToast("선행 연구가 필요합니다.");
            return;
        }

        // 2. 자원 소비 (선불)
        if (teamTag == "Player") ResourceManager.I.SpendResource(data.ironCost, data.oilCost);
        else if (teamTag == "Enemy") EnemyResourceManager.I.SpendResource(data.ironCost, data.oilCost);

        // 3. 연구 시작 (타이머 등록)
        if (data.researchTime > 0)
        {
            factionDatabase[teamTag].researchTimers[data.id] = data.researchTime;
            if (teamTag == "Player") UIManager.I.ShowToast($"{data.upgradeName} 연구 시작... ({data.researchTime}s)");
        }
        else
        {
            // 즉시 완료
            CompleteResearch(data.id, teamTag);
        }
    }

    // 연구 완료 처리 (내부 호출)
    private void CompleteResearch(string upgradeId, string teamTag)
    {
        UpgradeData data = allUpgrades.Find(u => u.id == upgradeId);
        if (data == null) return;

        ApplyUpgrade(data, teamTag);
    }

    private void ApplyUpgrade(UpgradeData data, string teamTag)
    {
        FactionData faction = factionDatabase[teamTag];
        faction.unlockedUpgradeIds.Add(data.id);

        if (data.effectType == UpgradeEffectType.StatBoost)
        {
            ApplyStatBoost(data, faction);
        }
        else if (data.effectType == UpgradeEffectType.UnlockAbility)
        {
            if (!string.IsNullOrEmpty(data.specialAbilityKey))
                faction.activeAbilityKeys.Add(data.specialAbilityKey);
        }

        if (teamTag == "Player") UIManager.I.ShowToast($"{data.upgradeName} 완료!");
        OnUpgradeCompleted?.Invoke(teamTag);
    }

    private void ApplyStatBoost(UpgradeData data, FactionData faction)
    {
        foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
        {
            if (IsTarget(data, type))
            {
                if (!faction.activeStatBonuses[type].ContainsKey(data.statType))
                    faction.activeStatBonuses[type][data.statType] = 0;

                faction.activeStatBonuses[type][data.statType] += data.value;
            }
        }
    }

    private bool IsTarget(UpgradeData data, UnitType type)
    {
        if (data.targetType == UpgradeTargetType.AllUnits) return true;
        if (data.targetType == UpgradeTargetType.SpecificUnit && data.specificUnit == type) return true;
        
        bool isRanged = (type == UnitType.Archer || type == UnitType.Mage || type == UnitType.Ballista || type == UnitType.BaseArcher);
        if (data.targetType == UpgradeTargetType.RangedUnits && isRanged) return true;
        if (data.targetType == UpgradeTargetType.MeleeUnits && !isRanged) return true;

        return false;
    }

    // --- 조회 함수들 ---

    public bool IsResearching(UpgradeData data, string teamTag)
    {
        if (factionDatabase.TryGetValue(teamTag, out FactionData faction))
        {
            return faction.researchTimers.ContainsKey(data.id);
        }
        return false;
    }

    public float GetStatBonus(UnitType type, StatType stat, string teamTag)
    {
        if (factionDatabase.TryGetValue(teamTag, out FactionData faction))
        {
            if (faction.activeStatBonuses.ContainsKey(type) && 
                faction.activeStatBonuses[type].TryGetValue(stat, out float value))
                return value;
        }
        return 0f;
    }

    public bool IsAbilityActive(string key, string teamTag)
    {
        if (factionDatabase.TryGetValue(teamTag, out FactionData faction))
            return faction.activeAbilityKeys.Contains(key);
        return false;
    }

    public bool IsUnlocked(UpgradeData data, string teamTag)
    {
        if (factionDatabase.TryGetValue(teamTag, out FactionData faction))
            return faction.unlockedUpgradeIds.Contains(data.id);
        return false;
    }

    public bool IsResearchable(UpgradeData data, string teamTag)
    {
        if (IsUnlocked(data, teamTag)) return false; 
        foreach (var req in data.preRequisites)
        {
            if (!IsUnlocked(req, teamTag)) return false; 
        }
        return true;
    }
}