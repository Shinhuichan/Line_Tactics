using UnityEngine;
using System.Collections.Generic;

public class FlagBearerAbility : UnitAbility
{
    [Header("기수병 오라 설정")]
    public float defenseBonus = 10f; 
    
    [Header("신규 능력: 가호 (Protection)")]
    public string protectionKey = "PROTECTION"; 
    public float shieldCooldown = 10.0f;        
    public float shieldRatio = 0.05f;           
    
    public GameObject protectionPrefab; 

    private List<UnitController> buffedUnits = new List<UnitController>();
    private Dictionary<int, float> shieldTimers = new Dictionary<int, float>();

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        buffedUnits.Clear();
        shieldTimers.Clear();
    }

    public override void OnUpdate()
    {
        UpdateAura();
    }

    void UpdateAura()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.attackRange);
        HashSet<UnitController> currentFrameUnits = new HashSet<UnitController>();

        bool isProtectionUnlocked = false;
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(protectionKey, owner.tag))
        {
            isProtectionUnlocked = true;
        }

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.CompareTag(owner.gameObject.tag))
            {
                if (hit.GetComponent<BaseController>() != null) continue;

                UnitController ally = hit.GetComponent<UnitController>();
                if (ally != null && ally.currentHP > 0)
                {
                    // 🚫 [수정] 노동병(Worker)은 전투 유닛이 아니므로 버프/보호 대상에서 완전히 제외
                    if (ally.unitType == UnitType.Worker) continue;

                    currentFrameUnits.Add(ally);
                    
                    if (!buffedUnits.Contains(ally))
                    {
                        ally.AddBonusDefense(defenseBonus);
                        buffedUnits.Add(ally);
                    }

                    if (isProtectionUnlocked)
                    {
                        TryApplyShield(ally);
                    }
                }
            }
        }

        for (int i = buffedUnits.Count - 1; i >= 0; i--)
        {
            UnitController u = buffedUnits[i];
            if (u == null || !u.gameObject.activeInHierarchy || !currentFrameUnits.Contains(u))
            {
                if (u != null && u.gameObject.activeInHierarchy)
                {
                    u.RemoveBonusDefense(defenseBonus);
                }
                buffedUnits.RemoveAt(i);
            }
        }
    }

    void TryApplyShield(UnitController ally)
    {
        int id = ally.gameObject.GetInstanceID();
        float lastTime = 0f;

        if (shieldTimers.TryGetValue(id, out lastTime))
        {
            if (Time.time < lastTime + shieldCooldown) return;
        }

        float shieldAmount = ally.maxHP * shieldRatio;
        
        ally.ApplyShield(shieldAmount, protectionPrefab);

        shieldTimers[id] = Time.time;
    }

    void OnDisable()
    {
        foreach (var u in buffedUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy)
            {
                u.RemoveBonusDefense(defenseBonus);
            }
        }
        buffedUnits.Clear();
        shieldTimers.Clear();
    }
}