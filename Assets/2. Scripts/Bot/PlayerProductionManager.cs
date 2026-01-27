using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlayerProductionManager : MonoBehaviour
{
    private PlayerBot brain;
    private Queue<BuildStep> buildQueue = new Queue<BuildStep>();
    
    private float spawnTimer = 0f;
    private const float SPAWN_INTERVAL = 2.0f; 
    private int myWorkerId = -1;

    private float economyTimer = 0f;
    private const float ECONOMY_CHECK_INTERVAL = 1.0f; 

    public void Initialize(PlayerBot bot)
    {
        this.brain = bot;
        buildQueue.Clear();
        IdentifyMyWorkerType();
    }

    // 🌟 [신규] 이벤트 구독 및 해제
    private void OnEnable()
    {
        BaseController.OnConstructionFinished += OnBaseBuiltHandler;
    }

    private void OnDisable()
    {
        BaseController.OnConstructionFinished -= OnBaseBuiltHandler;
    }

    // 🛑 [문제 해결 2] 건설 완료 시 호출됨 -> 플레이어 일꾼을 찾아 강제 채굴 명령
    private void OnBaseBuiltHandler(BaseController builtBase)
    {
        // 1. 내 기지가 아니면 무시
        if (!builtBase.CompareTag(brain.myTeamTag)) return;

        // 2. 방금 이 기지를 지은(혹은 아주 가까이 있는) 일꾼 찾기
        WorkerAbility builder = FindWorkerNearBase(builtBase);

        if (builder != null)
        {
            // WorkerAbility가 플레이어 유닛이라 'SetStateToIdle'을 했을 수 있으므로,
            // 여기서 강제로 'SetStateToMine'으로 덮어씌움 (Iron 우선)
            ResourceType targetRes = ResourceType.Iron;
            if (builtBase.currentTask == BaseTask.Oil) targetRes = ResourceType.Oil;

            builder.SetStateToMine(targetRes);
            Debug.Log($"🤖 [PlayerBot] Worker forced to mine {targetRes} at {builtBase.name}");
        }
    }

    WorkerAbility FindWorkerNearBase(BaseController baseCtrl)
    {
        float searchRadius = 3.0f; // 기지 중심에서 매우 가까운 일꾼
        Collider2D[] hits = Physics2D.OverlapCircleAll(baseCtrl.transform.position, searchRadius);
        
        foreach(var hit in hits)
        {
            WorkerAbility w = hit.GetComponent<WorkerAbility>();
            if (w != null && w.CompareTag(brain.myTeamTag))
            {
                // 건설 직후라 상태가 Building이거나 Idle일 가능성 높음
                return w;
            }
        }
        return null;
    }

    public void ClearQueue()
    {
        buildQueue.Clear();
        Debug.Log("[PlayerProduction] 🧹 Build Queue Cleared! (Strategy Switch)");
    }

    public void OnUpdate()
    {
        ProcessProductionQueue();
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

    private void ProcessProductionQueue()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer < SPAWN_INTERVAL) return;

        int reservedIron = 0;
        int reservedOil = 0;
        if (buildQueue.Count > 0)
        {
            CalculateStepCost(buildQueue.Peek(), out reservedIron, out reservedOil);
        }

        if (buildQueue.Count > 0)
        {
            BuildStep nextStep = buildQueue.Peek();
            bool isSuccess = false;
            string teamTag = brain.myTeamTag; 

            if (nextStep.stepType == BuildStepType.Unit)
            {
                if (CanAffordUnit((int)nextStep.unitType))
                {
                    if (TryPurchaseUnit((int)nextStep.unitType)) isSuccess = true;
                }
            }
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

                    if (!UpgradeManager.I.IsResearchable(nextStep.upgradeData, teamTag))
                    {
                         buildQueue.Dequeue(); // 선행 연구 미달 시 패스
                         return;
                    }

                    if (ResourceManager.I.CheckCost(nextStep.upgradeData.ironCost, nextStep.upgradeData.oilCost))
                    {
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

                UnitData outpostData = ConstructionManager.I.GetOutpostData(GameManager.I.playerRace);
                if (outpostData != null)
                {
                    if (ResourceManager.I.CheckCost(outpostData.ironCost, outpostData.oilCost))
                    {
                        bool built = ConstructionManager.I.TryBuildPlayerOutpost(brain.Strategy.expansionPolicy);
                        if (built) 
                        {
                            isSuccess = true;
                            // 확장 성공 시 즉시 전선 업데이트 (TacticsManager에서 RallyPoint 동기화 수행)
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
                return; 
            }
        }

        // 일꾼 자동 생산
        if (brain.IsOpeningFinished && NeedMoreWorkers())
        {
            UnitData workerData = SpawnManager.I.GetUnitDataByType((UnitType)myWorkerId);
            if (workerData != null)
            {
                int workerIron = workerData.ironCost;
                int workerOil = workerData.oilCost;

                bool hasSafeResources = false;
                if (ResourceManager.I != null)
                {
                    bool safeIron = ResourceManager.I.currentIron >= (workerIron + reservedIron);
                    bool safeOil = ResourceManager.I.currentOil >= (workerOil + reservedOil);
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
                UnitData data = ConstructionManager.I.GetOutpostData(GameManager.I.playerRace);
                if (data != null) { iron = data.ironCost; oil = data.oilCost; }
            }
        }
    }

    // 🔍 [신규] BotStateVisualizer용 디버그 함수 1: 다음 생산 아이템 이름
    public string GetNextItemName()
    {
        if (buildQueue.Count == 0) return "Empty";
        var next = buildQueue.Peek();
        if (next.stepType == BuildStepType.Unit) return next.unitType.ToString();
        if (next.stepType == BuildStepType.Upgrade) return next.upgradeData != null ? next.upgradeData.upgradeName : "Upgrade";
        if (next.stepType == BuildStepType.Expansion) return "Expansion";
        return "Unknown";
    }

    // 🔍 [신규] BotStateVisualizer용 디버그 함수 2: 부족한 자원 확인
    public ResourceType? GetMissingResourceForNextItem()
    {
        if (buildQueue.Count == 0) return null;

        BuildStep next = buildQueue.Peek();
        int ironCost = 0;
        int oilCost = 0;

        CalculateStepCost(next, out ironCost, out oilCost);

        if (ResourceManager.I != null)
        {
            if (ResourceManager.I.currentOil < oilCost) return ResourceType.Oil;
            if (ResourceManager.I.currentIron < ironCost) return ResourceType.Iron;
        }
        return null;
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

    bool CanAffordUnit(int unitId)
    {
        if (SpawnManager.I == null || ResourceManager.I == null) return false;
        UnitData data = SpawnManager.I.GetUnitDataByType((UnitType)unitId);
        if (data == null) return false;
        return ResourceManager.I.CheckCost(data.ironCost, data.oilCost);
    }

    bool NeedMoreWorkers()
    {
        if (brain.Strategy == null) return false;
        int currentWorkers = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit.CompareTag(brain.myTeamTag) && !unit.isDead)
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
        return SpawnManager.I.TrySpawnPlayerUnit(unitId);
    }

    public int GetQueueCount() => buildQueue.Count;
}