using UnityEngine;
using System.Collections; // Coroutine 사용
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

    private void OnEnable()
    {
        BaseController.OnConstructionFinished += OnBaseBuiltHandler;
    }

    private void OnDisable()
    {
        BaseController.OnConstructionFinished -= OnBaseBuiltHandler;
    }

    // 🛑 [문제 해결] 적군(Enemy) 일꾼도 건설 후 멈춤 방지
    // PlayerProductionManager와 동일한 로직 적용
    private void OnBaseBuiltHandler(BaseController builtBase)
    {
        if (!builtBase.CompareTag(brain.myTeamTag)) return;

        StartCoroutine(AssignWorkerToMineRoutine(builtBase));
    }

    // 🌟 1프레임 지연 후 강제 채굴 명령
    IEnumerator AssignWorkerToMineRoutine(BaseController builtBase)
    {
        yield return null; 

        WorkerAbility builder = FindWorkerNearBase(builtBase);

        if (builder != null)
        {
            ResourceType targetRes = ResourceType.Iron;
            if (builtBase.currentTask == BaseTask.Oil) targetRes = ResourceType.Oil;

            builder.SetStateToMine(targetRes);
            Debug.Log($"🤖 [EnemyBot] Worker forced to mine {targetRes} at {builtBase.name} (Delayed Fix)");
        }
    }

    WorkerAbility FindWorkerNearBase(BaseController baseCtrl)
    {
        float searchRadius = 5.0f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(baseCtrl.transform.position, searchRadius);
        
        foreach(var hit in hits)
        {
            WorkerAbility w = hit.GetComponent<WorkerAbility>();
            if (w != null && w.CompareTag(brain.myTeamTag))
            {
                if (w.currentState == WorkerState.Idle) return w;
            }
        }
        return hits.Select(h => h.GetComponent<WorkerAbility>())
                   .FirstOrDefault(w => w != null && w.CompareTag(brain.myTeamTag));
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

    private void ProcessProductionQueue()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < SPAWN_INTERVAL) return;

        bool queueSuccess = false;
        int reservedIron = 0; 
        int reservedOil = 0;

        if (buildQueue.Count > 0)
        {
            BuildStep nextStep = buildQueue.Peek();
            
            CalculateStepCost(nextStep, out reservedIron, out reservedOil);

            bool isSuccess = false;
            string teamTag = brain.myTeamTag; 

            if (nextStep.stepType == BuildStepType.Unit)
            {
                if (nextStep.unitType == UnitType.BaseArcher || nextStep.unitType == UnitType.BaseCorpse)
                {
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
            else if (nextStep.stepType == BuildStepType.Upgrade)
            {
                if (nextStep.upgradeData != null)
                {
                    // 1. 이미 완료했거나 연구 중이면 패스
                    if (UpgradeManager.I.IsUnlocked(nextStep.upgradeData, teamTag) ||
                        UpgradeManager.I.IsResearching(nextStep.upgradeData, teamTag))
                    {
                        buildQueue.Dequeue();
                        return;
                    }

                    // 🛑 [수정] 선행 연구 미충족 시 큐에서 제거 (PlayerBot과 동일 로직)
                    // 기존에는 이 체크가 없어서 큐가 막히거나 순서가 꼬임
                    if (!UpgradeManager.I.IsResearchable(nextStep.upgradeData, teamTag))
                    {
                         buildQueue.Dequeue(); 
                         return;
                    }

                    // 2. 자원 확인 및 구매
                    if (EnemyResourceManager.I.CheckCost(nextStep.upgradeData.ironCost, nextStep.upgradeData.oilCost))
                    {
                        // 위에서 IsResearchable을 확인했으므로 바로 구매
                        UpgradeManager.I.PurchaseUpgrade(nextStep.upgradeData, teamTag);
                        isSuccess = true;
                    }
                }
                else
                {
                    buildQueue.Dequeue();
                    return;
                }
            }
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
                        if (built) 
                        {
                            isSuccess = true;
                            // 🌟 확장 성공 시 전술 업데이트
                            if (brain.tactics != null)
                            {
                                brain.tactics.ForceUpdateFrontline();
                            }
                        }
                        else { buildQueue.Dequeue(); return; } 
                    }
                }
            }

            if (isSuccess)
            {
                buildQueue.Dequeue();
                spawnTimer = 0f;
                queueSuccess = true;
                return; 
            }
        }

        // ... (이하 일꾼 자동 생산 로직 기존과 동일) ...
        if (brain.IsOpeningFinished && NeedMoreWorkers())
        {
            UnitData workerData = SpawnManager.I.GetUnitDataByType((UnitType)myWorkerId);
            if (workerData != null)
            {
                int workerIron = workerData.ironCost;
                int workerOil = workerData.oilCost;

                bool hasSafeResources = false;
                if (EnemyResourceManager.I != null)
                {
                    bool safeIron = EnemyResourceManager.I.currentIron >= (workerIron + reservedIron);
                    bool safeOil = EnemyResourceManager.I.currentOil >= (workerOil + reservedOil);
                    hasSafeResources = safeIron && safeOil;
                }

                if (hasSafeResources && buildQueue.Count < 3)
                {
                    if (TryPurchaseUnit(myWorkerId))
                    {
                        spawnTimer = 0f;
                    }
                }
            }
        }
    }

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

        CalculateStepCost(next, out ironCost, out oilCost); 

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

    public List<string> GetBuildQueueNames()
    {
        return buildQueue.Select(step => 
        {
            if (step.stepType == BuildStepType.Unit) return $"Unit: {step.unitType}";
            if (step.stepType == BuildStepType.Upgrade) return $"Up: {(step.upgradeData != null ? step.upgradeData.upgradeName : "Unknown")}";
            return ">> EXPANSION <<";
        }).ToList();
    }
}