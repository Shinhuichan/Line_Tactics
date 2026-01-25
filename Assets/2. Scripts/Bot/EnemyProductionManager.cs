using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnemyProductionManager : MonoBehaviour
{
    private EnemyBot brain;
    private Queue<BuildStep> buildQueue = new Queue<BuildStep>();
    
    private float spawnTimer = 0f;
    private const float SPAWN_INTERVAL = 2.0f; 
    private int myWorkerId = -1;

    private float economyTimer = 0f;
    private const float ECONOMY_CHECK_INTERVAL = 1.0f; 

    [Header("🔍 디버깅용 (Read Only)")]
    public string currentGoalDebug = "None"; 
    public string missingResourceDebug = "None"; 

    public void Initialize(EnemyBot bot)
    {
        this.brain = bot;
        buildQueue.Clear();
        IdentifyMyWorkerType();
    }

    public void ClearQueue()
    {
        buildQueue.Clear();
        Debug.Log("[EnemyProduction] 🧹 Build Queue Cleared! (Strategy Switch)");
    }

    public void OnUpdate()
    {
        ProcessProductionQueue();
        UpdateDebugInfo();
        ProcessEconomyBalancing();
    }

    void IdentifyMyWorkerType()
    {
        myWorkerId = (int)UnitType.Worker;
        if (brain.Strategy != null && brain.Strategy.openingBuildOrder.Count > 0)
        {
            if ((int)brain.Strategy.openingBuildOrder[0].unitType >= 100)
                myWorkerId = (int)UnitType.Slave;
        }
    }

    public void EnqueueStep(BuildStep step)
    {
        buildQueue.Enqueue(step);
    }

    void UpdateDebugInfo()
    {
        if (buildQueue.Count > 0)
        {
            BuildStep step = buildQueue.Peek();
            if (step.stepType == BuildStepType.Unit) 
                currentGoalDebug = $"Unit: {step.unitType}";
            else if (step.stepType == BuildStepType.Upgrade)
                currentGoalDebug = $"Upgrade: {(step.upgradeData != null ? step.upgradeData.upgradeName : "Null")}";
            else 
                currentGoalDebug = "🏗️ EXPANSION (Base)";

            ResourceType? missing = GetMissingResourceForNextItem();
            missingResourceDebug = missing.HasValue ? missing.Value.ToString() : "Ready";
        }
        else
        {
            currentGoalDebug = "Idle";
            missingResourceDebug = "None";
        }
    }

    // 🌟 [핵심 수정] 생산 우선순위 및 자원 보존 로직 개선
    private void ProcessProductionQueue()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < SPAWN_INTERVAL) return;

        // 1. 전략 큐(Build Queue) 최우선 처리
        bool queueSuccess = false;
        int reservedIron = 0; // 큐 아이템을 위해 남겨둬야 할 자원
        int reservedOil = 0;

        if (buildQueue.Count > 0)
        {
            BuildStep nextStep = buildQueue.Peek();
            
            // 다음 목표의 예상 비용 계산 (자원 보존을 위해)
            CalculateStepCost(nextStep, out reservedIron, out reservedOil);

            bool isSuccess = false;
            string teamTag = brain.myTeamTag; 

            // A. 유닛 생산
            if (nextStep.stepType == BuildStepType.Unit)
            {
                // 방어 유닛(성채 장궁병/시체병) 처리
                if (nextStep.unitType == UnitType.BaseArcher || nextStep.unitType == UnitType.BaseCorpse)
                {
                    // SpawnManager에서 방어 유닛 전용 로직(비용 증가 등)이 있다면 TrySpawnBaseArcher 등을 호출해야 할 수도 있음
                    // 여기서는 일반 유닛처럼 처리하되, CanAffordUnit이 비용을 체크함
                    if (CanAffordUnit((int)nextStep.unitType))
                    {
                        if (TryPurchaseUnit((int)nextStep.unitType)) isSuccess = true;
                    }
                }
                else
                {
                    if (CanAffordUnit((int)nextStep.unitType))
                    {
                        if (TryPurchaseUnit((int)nextStep.unitType)) isSuccess = true;
                    }
                }
            }
            // B. 업그레이드
            else if (nextStep.stepType == BuildStepType.Upgrade)
            {
                if (nextStep.upgradeData != null)
                {
                    if (UpgradeManager.I.IsUnlocked(nextStep.upgradeData, teamTag) ||
                        UpgradeManager.I.IsResearching(nextStep.upgradeData, teamTag))
                    {
                        buildQueue.Dequeue();
                        return;
                    }

                    if (EnemyResourceManager.I.CheckCost(nextStep.upgradeData.ironCost, nextStep.upgradeData.oilCost))
                    {
                        if (UpgradeManager.I.IsResearchable(nextStep.upgradeData, teamTag))
                        {
                            UpgradeManager.I.PurchaseUpgrade(nextStep.upgradeData, teamTag);
                            isSuccess = true;
                        }
                    }
                }
                else
                {
                    buildQueue.Dequeue();
                    return;
                }
            }
            // C. 확장
            else if (nextStep.stepType == BuildStepType.Expansion)
            {
                if (ConstructionManager.I == null || GameManager.I == null) 
                {
                    buildQueue.Dequeue(); 
                    return; 
                }

                UnitData enemyOutpostData = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                if (enemyOutpostData != null)
                {
                    if (EnemyResourceManager.I.CheckCost(enemyOutpostData.ironCost, enemyOutpostData.oilCost))
                    {
                        bool built = ConstructionManager.I.TryBuildEnemyOutpost(brain.Strategy.expansionPolicy);
                        if (built) isSuccess = true;
                        else { buildQueue.Dequeue(); return; } // 자리 없으면 스킵
                    }
                }
            }

            if (isSuccess)
            {
                buildQueue.Dequeue();
                spawnTimer = 0f;
                queueSuccess = true;
                return; // 큐 아이템 생산 성공 시, 이번 턴에는 일꾼 생산 안 함 (자원 보호)
            }
        }

        // 2. 일꾼 자동 생산 (큐 처리 실패 혹은 큐가 비었을 때 수행)
        // 🌟 조건: [오프닝 종료] AND [일꾼 부족] AND [큐 아이템 비용을 제외하고도 자원이 남을 때]
        if (brain.IsOpeningFinished && NeedMoreWorkers())
        {
            // 현재 일꾼 수가 너무 적으면(예: 3마리 미만) 큐 무시하고 긴급 생산 (옵션)
            // 여기서는 자원 보존 법칙을 따름
            
            UnitData workerData = SpawnManager.I.GetUnitDataByType((UnitType)myWorkerId);
            if (workerData != null)
            {
                int workerIron = workerData.ironCost;
                int workerOil = workerData.oilCost;

                // 🌟 [핵심] 현재 자원이 (일꾼 비용 + 큐 예약 비용)보다 많은가?
                bool hasSafeResources = false;
                if (EnemyResourceManager.I != null)
                {
                    bool safeIron = EnemyResourceManager.I.currentIron >= (workerIron + reservedIron);
                    bool safeOil = EnemyResourceManager.I.currentOil >= (workerOil + reservedOil);
                    hasSafeResources = safeIron && safeOil;
                }

                // 큐가 비어있다면 예약 비용은 0이므로 자연스럽게 통과
                if (hasSafeResources && buildQueue.Count < 3) // 생산 대기열 꽉 참 방지
                {
                    if (TryPurchaseUnit(myWorkerId))
                    {
                        spawnTimer = 0f;
                        // Debug.Log("[Production] 일꾼 추가 생산 (여유 자원 활용)");
                    }
                }
            }
        }
    }

    // 🧮 예약 비용 계산 헬퍼 함수
    void CalculateStepCost(BuildStep step, out int iron, out int oil)
    {
        iron = 0;
        oil = 0;
        
        if (step.stepType == BuildStepType.Unit)
        {
            if (SpawnManager.I != null)
            {
                UnitData data = SpawnManager.I.GetUnitDataByType(step.unitType);
                if (data != null) { iron = data.ironCost; oil = data.oilCost; }
            }
        }
        else if (step.stepType == BuildStepType.Upgrade && step.upgradeData != null)
        {
            iron = step.upgradeData.ironCost;
            oil = step.upgradeData.oilCost;
        }
        else if (step.stepType == BuildStepType.Expansion)
        {
            if (ConstructionManager.I != null && GameManager.I != null)
            {
                UnitData data = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                if (data != null) { iron = data.ironCost; oil = data.oilCost; }
            }
        }
    }

    private void ProcessEconomyBalancing()
    {
        economyTimer += Time.deltaTime;
        if (economyTimer < ECONOMY_CHECK_INTERVAL) return;
        economyTimer = 0f;

        ResourceType? missing = GetMissingResourceForNextItem();
        
        List<WorkerAbility> myWorkers = GetMyWorkers();
        if (myWorkers.Count == 0) return;

        List<WorkerAbility> ironMiners = myWorkers.Where(w => w.targetResourceType == ResourceType.Iron && IsMiningOrMoving(w)).ToList();
        List<WorkerAbility> oilMiners = myWorkers.Where(w => w.targetResourceType == ResourceType.Oil && IsMiningOrMoving(w)).ToList();

        if (missing == ResourceType.Oil)
        {
            if (ironMiners.Count > 0)
            {
                WorkerAbility worker = ironMiners[0];
                worker.SetStateToMine(ResourceType.Oil);
            }
        }
        else if (missing == ResourceType.Iron)
        {
            if (oilMiners.Count > 1) 
            {
                WorkerAbility worker = oilMiners[0];
                worker.SetStateToMine(ResourceType.Iron);
            }
        }
    }

    bool IsMiningOrMoving(WorkerAbility w)
    {
        return w.currentState == WorkerState.Mining || 
               w.currentState == WorkerState.MovingToResource || 
               w.currentState == WorkerState.ReturningToBase ||
               w.currentState == WorkerState.Idle;
    }

    List<WorkerAbility> GetMyWorkers()
    {
        List<WorkerAbility> list = new List<WorkerAbility>();
        WorkerAbility[] all = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach(var w in all)
        {
            if (w.CompareTag(brain.myTeamTag) && !w.owner.isDead)
            {
                list.Add(w);
            }
        }
        return list;
    }

    public ResourceType? GetMissingResourceForNextItem()
    {
        if (buildQueue.Count == 0) return null;

        BuildStep next = buildQueue.Peek();
        int ironCost = 0;
        int oilCost = 0;

        CalculateStepCost(next, out ironCost, out oilCost); // 코드 재사용

        if (EnemyResourceManager.I != null)
        {
            if (EnemyResourceManager.I.currentOil < oilCost) return ResourceType.Oil;
            if (EnemyResourceManager.I.currentIron < ironCost) return ResourceType.Iron;
        }
        return null;
    }

    bool CanAffordUnit(int unitId)
    {
        if (SpawnManager.I == null || EnemyResourceManager.I == null) return false;
        UnitData data = SpawnManager.I.GetUnitDataByType((UnitType)unitId);
        if (data == null) return false;
        return EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost);
    }

    bool NeedMoreWorkers()
    {
        if (brain.Strategy == null) return false;
        int currentWorkers = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(gameObject.tag) && !unit.isDead)
            {
                if (unit.unitType == UnitType.Worker || unit.unitType == UnitType.Slave)
                    currentWorkers++;
            }
        }
        return currentWorkers < brain.Strategy.idealWorkerCount;
    }

    public bool TryPurchaseUnit(int unitId)
    {
        if (SpawnManager.I == null) return false;
        return SpawnManager.I.TrySpawnEnemyUnit(unitId);
    }

    public int GetQueueCount() => buildQueue.Count;
    
    public string GetNextItemName()
    {
        if (buildQueue.Count == 0) return "Empty";
        var next = buildQueue.Peek();
        if (next.stepType == BuildStepType.Unit) return next.unitType.ToString();
        if (next.stepType == BuildStepType.Expansion) return "EXPANSION"; 
        return next.upgradeData != null ? next.upgradeData.upgradeName : "Null Upgrade";
    }
}