using UnityEngine;
using System.Collections.Generic;

public class PlayerTacticsManager : MonoBehaviour
{
    private PlayerBot brain;
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
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

    void LaunchAllOutAttack()
    {
        Debug.Log("⚔️ [PlayerBot] All-Out Attack Triggered!");
        if (TacticalCommandManager.I != null)
        {
            TacticalCommandManager.I.SetState(TacticalState.Attack);
        }
    }

    void DecideTacticalState()
    {
        if (TacticalCommandManager.I == null) return;
        TacticalState currentState = TacticalCommandManager.I.currentState;

        if (currentState == TacticalState.Attack)
        {
            float myPower = CalculateMyCombatPower();
            if (myPower < 100f) 
            {
                TacticalCommandManager.I.SetState(TacticalState.Defend);
            }
            return;
        }

        bool underAttack = IsBaseUnderAttack();

        if (underAttack)
        {
            if(currentState != TacticalState.Defend && currentState != TacticalState.Siege)
                TacticalCommandManager.I.SetState(TacticalState.Defend);
        }
        else
        {
            // 평시 유지
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
            if (hit.CompareTag("Enemy")) 
            {
                UnitController unit = hit.GetComponent<UnitController>();
                if (unit != null && !unit.isDead)
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