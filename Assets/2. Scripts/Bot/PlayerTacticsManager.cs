using UnityEngine;
using System.Collections.Generic;

public class PlayerTacticsManager : MonoBehaviour
{
    private PlayerBot brain;
    private float tacticsTimer = 0f;
    private float siegeCooldown = 0f;

    // 🌟 [핵심 수정] Visualizer가 참조할 수 있도록 public 변수 추가
    [Header("전선 관리")]
    public Vector3 playerFrontLinePos; 
    public BaseController currentFrontBase; // <-- 이 변수가 없어서 에러가 났었습니다.

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

    // 1. 현재 전선(가장 적과 가까운 아군 기지) 찾기 및 명령 하달
    void UpdateFrontline()
    {
        // 적(Enemy) 위치 파악 (없으면 적 본진)
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

        // 내 기지 중 적과 가장 가까운 곳(= 최전선) 찾기
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl == null) continue;
            // 건설 완료된 기지만 전선으로 취급 (건설 중인 곳으로 가면 위험할 수 있음, 혹은 건설 중인 곳을 보호하려면 포함 가능)
            // 여기서는 안전하게 '건설 완료'된 곳을 거점으로 삼음. (Outpost 건설 직후에는 완료 상태이므로 감지됨)
            if (!baseCtrl.CompareTag(brain.myTeamTag)) continue;
            if (!baseCtrl.isConstructed) continue; 

            float dst = Vector3.Distance(baseCtrl.transform.position, targetPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = baseCtrl;
            }
        }

        if (bestBase != null)
        {
            // 전선이 변경되었거나, 초기 상태라면
            if (currentFrontBase != bestBase)
            {
                currentFrontBase = bestBase;
                playerFrontLinePos = bestBase.transform.position;

                // 🌟 [핵심 수정] 유닛을 직접 조종하지 않고, 사령부(Manager)에 명령만 내림
                // "이 기지가 최전선이니 여기로 집결 지점을 변경하라"
                if (TacticalCommandManager.I != null && ConstructionManager.I != null)
                {
                    SyncRallyPointToFront(bestBase);
                }
            }
        }
        else
        {
            // 기지가 하나도 없으면 봇 위치를 전선으로
            playerFrontLinePos = transform.position; 
        }
    }

    // 📡 기지 위치에 해당하는 Tactical Point 인덱스를 찾아 사령부에 전달
    void SyncRallyPointToFront(BaseController baseCtrl)
    {
        int bestIndex = -1;
        float minDist = 5.0f; // 오차 범위 (건설 위치와 Tactical Point가 정확히 일치하지 않을 수 있음)

        // ConstructionManager의 포인트들을 뒤져서, 현재 기지랑 가장 가까운 포인트를 찾음
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
            // 🌟 사령관(TacticalCommandManager)에게 집결지 변경 명령
            // 유닛들은 Update()에서 TacticalCommandManager.currentRallyPoint를 보고
            // UnitData.defendDistance에 맞춰 알아서 예쁘게 이동함. (떨림 해결)
            TacticalCommandManager.I.SetRallyPointByIndex(bestIndex);
        }
    }

    // ⚡ 외부 호출용: 강제 전선 갱신 (건설 완료 시 호출됨)
    public void ForceUpdateFrontline()
    {
        UpdateFrontline();
        // RallyTroopsToFrontline() 호출 제거됨
        Debug.Log("⚔️ [PlayerTactics] Frontline Synced via Construction Event.");
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

        // 공격 중인데 힘이 빠지면 후퇴(Defend)
        if (currentState == TacticalState.Attack)
        {
            float myPower = CalculateMyCombatPower();
            if (myPower < 100f) 
            {
                TacticalCommandManager.I.SetState(TacticalState.Defend);
            }
            return;
        }

        // 본진이 공격받으면 방어(Defend)로 전환 (농성 중이 아닐 때만)
        bool underAttack = IsBaseUnderAttack();
        if (underAttack)
        {
            if(currentState != TacticalState.Defend && currentState != TacticalState.Siege)
                TacticalCommandManager.I.SetState(TacticalState.Defend);
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
                    power += GetUnitPower(unit);
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
                total += GetUnitPower(unit);
            }
        }
        return total;
    }

    float GetUnitPower(UnitController unit)
    {
        if (unit.unitType == UnitType.BaseArcher || unit.unitType == UnitType.BaseCorpse)
            return 0f;

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