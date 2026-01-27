using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum TacticalState { Defend, Attack, Siege } 

public class EnemyTacticsManager : MonoBehaviour
{
    private EnemyBot brain;

    [Header("전술 상태")]
    public TacticalState currentState = TacticalState.Defend;
    
    public Vector3 enemyFrontLinePos; 
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    public void Initialize(EnemyBot bot)
    {
        this.brain = bot;
        currentState = TacticalState.Defend;
    }

    public void OnUpdate()
    {
        if (siegeCooldown > 0) siegeCooldown -= Time.deltaTime;

        tacticsTimer += Time.deltaTime;
        if (tacticsTimer >= 0.5f) 
        {
            tacticsTimer = 0f;
            DecideTacticalState();
        }
    }

    public bool TryTriggerWave(AttackWave wave)
    {
        if (wave.requiredUnits != null && wave.requiredUnits.Count > 0)
        {
            foreach (var pair in wave.requiredUnits)
            {
                int currentCount = CountMyUnit(pair.unitType);
                if (currentCount < pair.count) return false; 
            }
        }

        if (wave.requiredPowerRatio > 0)
        {
            if (brain.scout.enemyTotalPower <= 0) return false;

            float myPower = CalculateMyCombatPower();
            float ratio = myPower / brain.scout.enemyTotalPower;

            if (ratio < wave.requiredPowerRatio) return false; 
        }

        LaunchAllOutAttack();
        return true;
    }

    public void LaunchAllOutAttack()
    {
        Debug.Log("⚔️ [EnemyBot] All-Out Attack Triggered!");
        currentState = TacticalState.Attack;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
            {
                if (unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave)
                {
                    Vector3 target = brain.scout.primaryTargetPos;
                    unit.SetStateToAttackMove(target);
                }
            }
        }
    }

    void DecideTacticalState()
    {
        if (currentState == TacticalState.Attack)
        {
            float myPower = CalculateMyCombatPower();
            if (myPower < 100f) 
            {
                currentState = TacticalState.Defend;
            }
            return;
        }

        bool underAttack = IsBaseUnderAttack();

        if (underAttack)
        {
            currentState = TacticalState.Defend;
        }
        else
        {
            currentState = TacticalState.Defend;
        }
    }

    bool IsBaseUnderAttack()
    {
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl.CompareTag(brain.myTeamTag))
            {
                if (CalculateLocalEnemyPower(baseCtrl.transform.position, 15f) > 0)
                    return true;
            }
        }
        return false;
    }

    float CalculateLocalEnemyPower(Vector3 center, float radius)
    {
        float power = 0f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player")) 
            {
                UnitController unit = hit.GetComponent<UnitController>();
                if (unit != null && !unit.isDead)
                    power += GetUnitPower(unit); // ⚡ 헬퍼 사용
            }
        }
        return power;
    }

    float CalculateLocalMyPower(Vector3 center, float radius)
    {
        float power = 0f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(brain.myTeamTag))
            {
                UnitController unit = hit.GetComponent<UnitController>();
                if (unit != null && !unit.isDead && unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave)
                    power += GetUnitPower(unit); // ⚡ 헬퍼 사용
            }
        }
        return power;
    }

    public float CalculateMyCombatPower()
    {
        float total = 0f;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
            {
                if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave) continue;
                total += GetUnitPower(unit); // ⚡ 헬퍼 사용
            }
        }
        return total;
    }

    // ⚡ [신규] 전투력 계산 헬퍼 함수 (성채 유닛 완전 제외)
    float GetUnitPower(UnitController unit)
    {
        // 성채 장궁병, 성채 시체병은 형세 판단에서 투명 인간 취급 (0점)
        if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse)
        {
            return 0f;
        }

        float power = unit.currentHP * 0.1f + unit.attackDamage;
        return power;
    }

    int CountMyUnit(UnitType type)
    {
        int count = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead && unit.unitType == type)
            {
                count++;
            }
        }
        return count;
    }
}