using UnityEngine;
using System.Collections.Generic;

public class PlayerTacticsManager : MonoBehaviour
{
    private PlayerBot brain;
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    // ➕ [추가] 전선 관리를 위한 변수 선언
    [Header("전선 관리")]
    public Vector3 playerFrontLinePos; 
    public BaseController currentFrontBase; // 현재 최전선 기지

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
        // 초기화 시 전선 한번 설정
        UpdateFrontline();
    }

    public void OnUpdate()
    {
        if (siegeCooldown > 0) siegeCooldown -= Time.deltaTime;

        tacticsTimer += Time.deltaTime;
        if (tacticsTimer >= 0.5f) 
        {
            tacticsTimer = 0f;
            DecideTacticalState();
            UpdateFrontline(); // 🔄 주기적으로 전선 위치 갱신
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

    // =================================================================================
    // ➕ [추가] 누락되었던 전선 갱신 및 병력 집결 메서드 구현
    // =================================================================================

    // 1. 현재 전선(가장 적과 가까운 아군 기지) 찾기
    // 🌟 [핵심 수정] 전선을 갱신하면서 Global Rally Point도 함께 동기화
    private void UpdateFrontline()
    {
        Vector3 targetPos = Vector3.zero;
        if (brain.scout != null && brain.scout.primaryTargetPos != Vector3.zero)
        {
            targetPos = brain.scout.primaryTargetPos;
        }
        else
        {
            GameObject enemyBase = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyBase != null) targetPos = enemyBase.transform.position;
        }

        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.CompareTag(brain.myTeamTag)) continue;

            float dst = Vector3.Distance(baseCtrl.transform.position, targetPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = baseCtrl;
            }
        }

        if (bestBase != null)
        {
            // 전선이 변경되었는지 확인
            bool isNewFront = (currentFrontBase != bestBase);

            currentFrontBase = bestBase;
            playerFrontLinePos = bestBase.transform.position;

            // 🛑 [문제 해결 1] Bot이 생각하는 전선과 Global Command(유닛 기본 AI)를 일치시킴
            // 전선 기지가 바뀔 때마다 TacticalCommandManager의 Rally Point를 해당 기지로 설정
            if (isNewFront && TacticalCommandManager.I != null && ConstructionManager.I != null)
            {
                SyncRallyPointToFront(bestBase);
            }
        }
        else
        {
            playerFrontLinePos = transform.position;
        }
    }

    // 건설된 기지와 일치하는 Tactical Point 인덱스를 찾아 설정
    void SyncRallyPointToFront(BaseController baseCtrl)
    {
        // ConstructionManager의 tacticalPoints 리스트에서 해당 기지와 가까운 위치의 인덱스를 찾음
        int bestIndex = -1;
        float minDist = 2.0f; // 오차 범위

        for (int i = 0; i < ConstructionManager.I.tacticalPoints.Count; i++)
        {
            Transform point = ConstructionManager.I.tacticalPoints[i];
            if (point == null) continue;

            float dist = Vector3.Distance(point.position, baseCtrl.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                bestIndex = i;
            }
        }

        if (bestIndex != -1)
        {
            // 인덱스 강제 조정 -> 유닛들이 Global 명령을 따라도 같은 곳으로 오게 됨
            TacticalCommandManager.I.currentRallyIndex = bestIndex;
            
            // 실제 반영을 위해 UpdateRallyPoint 로직이 필요할 수 있으나, 
            // 변수를 직접 바꾸고 UI 갱신 등을 위해 OrderAdvance 등을 모방하거나 직접 할당
            TacticalCommandManager.I.currentRallyPoint = ConstructionManager.I.tacticalPoints[bestIndex];
            
            Debug.Log($"🤖 [PlayerBot] Rally Point Synced to Index {bestIndex} ({baseCtrl.name})");
        }
    }

    // 2. 병력을 전선으로 집결시키기 (보조)
    void RallyTroopsToFrontline()
    {
        if (currentFrontBase == null) return;

        foreach (var unit in UnitController.activeUnits)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(brain.myTeamTag)) continue;
            
            if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave) continue;
            if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse) continue;

            float distToFront = Vector3.Distance(unit.transform.position, playerFrontLinePos);
            
            if (distToFront > 8.0f) 
            {
                Vector3 rallyPoint = playerFrontLinePos + (Vector3)Random.insideUnitCircle * 4.0f;
                unit.SetStateToAttackMove(rallyPoint);
            }
        }
    }

    // =================================================================================

    // ⚡ [신규] 외부(Bot)에서 호출하여 즉시 전선을 갱신하고 병력을 이동시킴
    public void ForceUpdateFrontline()
    {
        UpdateFrontline();
        RallyTroopsToFrontline();
        Debug.Log("⚔️ [PlayerTactics] Frontline Force Updated via Construction Event.");
    }
}