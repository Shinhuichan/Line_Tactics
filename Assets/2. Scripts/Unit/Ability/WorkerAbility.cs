using UnityEngine;

public enum WorkerState
{
    Idle,           
    MovingToResource, 
    Mining,         
    ReturningToBase,
    Attack,
    Building,
    Repairing
}

public class WorkerAbility : UnitAbility
{
    [Header("노동자 상태")]
    public WorkerState currentState = WorkerState.Idle;
    public ResourceType targetResourceType = ResourceType.Iron;

    [Header("소속 관리")]
    public BaseController assignedBase; 

    [Header("채집 설정")]
    public float miningDuration = 0.5f; 
    public int ironMiningPower = 5; 
    public int oilMiningPower = 3; 

    [Header("능력치")]
    public int currentMaxCapacity; 
    public float interactionRange = 1.5f; 

    [Header("상태 정보")]
    public int currentLoad = 0; 
    private float miningTimer = 0f;
    private Transform targetNodeTransform; 
    private ResourceNode targetNodeScript; 

    [SerializeField]
    private ResourceType heldResourceType; 
    
    private ResourceType? pendingResourceType = null;

    public BaseController targetConstructionSite;

    // 🔧 [신규] 자원 반납 후 수리하러 갈 타겟 저장용
    private BaseController pendingRepairTarget = null;

    // 수리 관련
    private float repairTimer = 0f;
    
    // AI 설정
    [Header("AI 설정")]
    public bool isBotMode = false;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        currentMaxCapacity = 10; 

        if (owner.CompareTag("Enemy"))
        {
            isBotMode = true;
        }

        FindAndJoinNearestBase();
    }

    void FindAndJoinNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach(var b in bases)
        {
            if (!b.CompareTag(owner.tag)) continue;
            if (!b.isConstructed) continue; 

            float d = Vector3.Distance(transform.position, b.transform.position);
            if(d < minDst)
            {
                minDst = d;
                bestBase = b;
            }
        }

        if (bestBase != null)
        {
            JoinBase(bestBase);
            switch (bestBase.currentTask)
            {
                case BaseTask.Iron: SetStateToMine(ResourceType.Iron); break;
                case BaseTask.Oil: SetStateToMine(ResourceType.Oil); break;
                default: SetStateToIdle(); break;
            }
        }
        else
        {
            assignedBase = null;
            SetStateToIdle();
        }
        
        if (WorkerDashboardManager.I != null) 
            WorkerDashboardManager.I.RebuildSlotList(); 
    }

    void JoinBase(BaseController baseCtrl)
    {
        assignedBase = baseCtrl;
        if (!baseCtrl.assignedWorkers.Contains(this))
        {
            baseCtrl.assignedWorkers.Add(this);
        }
    }

    void OnEnable()
    {
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted += OnWorkerUpgradeHandler;
    }

    void OnDisable()
    {
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted -= OnWorkerUpgradeHandler;
    }

    private void OnWorkerUpgradeHandler(string teamTag)
    {
        if (gameObject.CompareTag(teamTag)) RecalculateWorkerStats();
    }

    public override bool OnDie()
    {
        if (assignedBase != null)
        {
            if (assignedBase.assignedWorkers.Contains(this))
            {
                assignedBase.assignedWorkers.Remove(this);
            }
        }
        targetConstructionSite = null;
        currentState = WorkerState.Idle;
        return false; 
    }

    public void RecalculateWorkerStats()
    {
        if (UpgradeManager.I == null) return;

        int ironCap = 15;
        int oilCap = 5;
        string myTag = gameObject.tag;

        if (UpgradeManager.I.IsAbilityActive("MINING_2", myTag))
        {
            ironCap = 30;
            oilCap = 10;
        }
        else if (UpgradeManager.I.IsAbilityActive("MINING_1", myTag))
        {
            ironCap = 24;
            oilCap = 8;
        }

        UpdateCurrentCapacity(ironCap, oilCap);
    }

    void UpdateCurrentCapacity(int ironCap, int oilCap)
    {
        ResourceType typeToCheck = (currentLoad > 0) ? heldResourceType : targetResourceType;
        if (typeToCheck == ResourceType.Iron) currentMaxCapacity = ironCap;
        else currentMaxCapacity = oilCap;
    }

    public override void OnUpdate()
    {
        switch (currentState)
        {
            case WorkerState.Idle:
                break;
            case WorkerState.MovingToResource:
                ProcessMoveToResource();
                break;
            case WorkerState.Mining:
                ProcessMining();
                break;
            case WorkerState.ReturningToBase:
                ProcessReturnToBase(); 
                break;
            case WorkerState.Building:
                ProcessBuilding();
                break;
            case WorkerState.Repairing:
                ProcessRepairing();
                break;
            case WorkerState.Attack:
                break;
        }
    }

    // 🔧 [수정] 수리 명령 (Q2: 자원 있으면 반납 후 수리)
    public void SetStateToRepair(BaseController baseTarget)
    {
        if (baseTarget == null) return;

        owner.isManualMove = true; 

        // 1. 자원을 들고 있다면? -> 반납하러 간다 (B안)
        if (currentLoad > 0)
        {
            pendingRepairTarget = baseTarget; // 반납 후 갈 곳 예약
            currentState = WorkerState.ReturningToBase;
            return;
        }

        // 2. 빈손이라면 -> 바로 수리하러 간다
        targetConstructionSite = baseTarget; 
        currentState = WorkerState.Repairing;
        pendingRepairTarget = null;
    }

    void ProcessRepairing()
    {
        // 타겟이 없거나, 이미 풀피가 되었다면? -> 작업 종료 및 복귀
        if (targetConstructionSite == null || targetConstructionSite.currentHP >= targetConstructionSite.maxHP)
        {
            targetConstructionSite = null;
            
            // 🔄 [Q3: B안] 수리 종료 후, 현재 소속된 기지의 태세(Task)에 따라 복귀
            if (assignedBase != null)
            {
                BaseTask task = assignedBase.currentTask;
                if (task == BaseTask.Iron) SetStateToMine(ResourceType.Iron);
                else if (task == BaseTask.Oil) SetStateToMine(ResourceType.Oil);
                else SetStateToIdle();
            }
            else
            {
                // 소속 기지가 없다면 그냥 가장 가까운 기지 찾아서 합류 시도
                FindAndJoinNearestBase();
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, targetConstructionSite.transform.position);
        
        if (dist > interactionRange)
        {
            MoveTowards(targetConstructionSite.transform.position);
        }
        else
        {
            // 수리 진행 (건설과 동일한 로직 사용 가능하지만 Repair 호출)
            float repairAmount = 100f * Time.deltaTime; // 수리 속도 조절 가능
            targetConstructionSite.Repair(repairAmount);
        }
    }

    void ProcessMoveToResource()
    {
        if (targetNodeTransform == null)
        {
            if (assignedBase != null) FindResourceNearBase(assignedBase);
            else FindNearestResourceGlobal();

            if (targetNodeTransform == null)
            {
                CheckSmartMigrationOrIdle(); 
                return;
            }
        }

        float distToEdge = GetDistanceToTargetEdge(targetNodeTransform);

        if (distToEdge <= interactionRange)
        {
            currentState = WorkerState.Mining;
            miningTimer = 0f;
        }
        else
        {
            MoveTowards(targetNodeTransform.position);
        }
    }

    void FindNearestResourceGlobal()
    {
        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        float closestDist = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var node in allNodes)
        {
            if (node.resourceType == targetResourceType)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestNode = node;
                }
            }
        }
        SetTargetNode(bestNode);
    }

    void FindResourceNearBase(BaseController baseCtrl)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(baseCtrl.transform.position, baseCtrl.resourceScanRange);
        float closestDist = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == targetResourceType)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestNode = node;
                }
            }
        }
        SetTargetNode(bestNode);
    }

    void SetTargetNode(ResourceNode node)
    {
        if (node != null)
        {
            targetNodeTransform = node.transform;
            targetNodeScript = node;
        }
        else
        {
            targetNodeTransform = null;
            targetNodeScript = null;
        }
    }

    void ProcessReturnToBase()
    {
        BaseController targetBase = (assignedBase != null && assignedBase.isConstructed) ? assignedBase : FindNearestBase();

        if (targetBase == null) return;

        float dist = GetDistanceToTargetEdge(targetBase.transform);
        if (dist > interactionRange)
        {
            MoveTowards(targetBase.transform.position);
        }
        else
        {
            DepositResource();
        }
    }

    BaseController FindNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var b in bases)
        {
            if (!b.CompareTag(owner.gameObject.tag)) continue;
            if (!b.isConstructed) continue; 

            float dst = GetDistanceToTargetEdge(b.transform);
            if (dst < minDst)
            {
                minDst = dst;
                bestBase = b;
            }
        }
        return bestBase;
    }

    float GetDistanceToTargetEdge(Transform target)
    {
        if (target == null) return Mathf.Infinity;
        Collider2D targetCol = target.GetComponent<Collider2D>();
        if (targetCol != null)
        {
            Vector3 closestPoint = targetCol.ClosestPoint(transform.position);
            return Vector3.Distance(transform.position, closestPoint);
        }
        return Vector3.Distance(transform.position, target.position);
    }

    void ProcessMining()
    {
        if (targetNodeScript == null || targetNodeScript.currentAmount <= 0) 
        {
            if (currentLoad > 0) 
            {
                currentState = WorkerState.ReturningToBase;
            }
            else 
            {
                AttemptFindNewResourceOrMigrate();
            }
            return;
        }

        if (currentLoad >= currentMaxCapacity)
        {
            currentState = WorkerState.ReturningToBase;
            return;
        }

        miningTimer += Time.deltaTime;
        if (miningTimer >= miningDuration)
        {
            miningTimer = 0f;
            int power = (targetResourceType == ResourceType.Iron) ? ironMiningPower : oilMiningPower;
            int spaceLeft = currentMaxCapacity - currentLoad;
            int amountToMine = Mathf.Min(power, spaceLeft);
            int harvested = targetNodeScript.Harvest(amountToMine);
            
            if (harvested > 0)
            {
                currentLoad += harvested;
                heldResourceType = targetResourceType;
                
                if (currentLoad >= currentMaxCapacity) currentState = WorkerState.ReturningToBase;
            }
            else
            {
                if (currentLoad > 0) 
                {
                    currentState = WorkerState.ReturningToBase;
                }
                else 
                {
                    AttemptFindNewResourceOrMigrate();
                }
            }
        }
    }

    void AttemptFindNewResourceOrMigrate()
    {
        if (assignedBase != null)
        {
            FindResourceNearBase(assignedBase);
            if (targetNodeTransform != null)
            {
                currentState = WorkerState.MovingToResource;
                return;
            }
        }
        CheckSmartMigrationOrIdle();
    }

    void CheckSmartMigrationOrIdle()
    {
        if (isBotMode)
        {
            BaseController newHome = BaseController.FindBaseWithResource(targetResourceType, owner.tag);

            if (newHome != null && newHome != assignedBase)
            {
                TransferBase(newHome);
                SetStateToMine(targetResourceType);
                return;
            }
        }
        SetStateToIdle();
    }

    void MoveTowards(Vector3 targetPos)
    {
        if (owner != null)
        {
            owner.MoveToPosition(targetPos);
        }
    }

    // 💰 [수정] 자원 반납 로직 (수리 예약 확인)
    void DepositResource()
    {
        if (owner.CompareTag("Player"))
        {
            if (ResourceManager.I != null) 
            {
                if(heldResourceType == ResourceType.Iron) ResourceManager.I.AddResource(currentLoad, 0);
                else ResourceManager.I.AddResource(0, currentLoad);
            }
        }
        else if (owner.CompareTag("Enemy") && EnemyResourceManager.I != null)
        {
             if(heldResourceType == ResourceType.Iron) EnemyResourceManager.I.AddResource(currentLoad, 0);
             else EnemyResourceManager.I.AddResource(0, currentLoad);
        }
        
        ShowDepositText();
        
        currentLoad = 0; 

        // 🔧 [신규] 수리를 위해 반납하러 온 경우라면? -> 바로 수리하러 이동!
        if (pendingRepairTarget != null)
        {
            SetStateToRepair(pendingRepairTarget);
            return;
        }

        // 기존 로직 (자원 전환 or 계속 채집)
        if (pendingResourceType.HasValue)
        {
            ResourceType next = pendingResourceType.Value;
            pendingResourceType = null; 
            SetStateToMine(next);       
            return;
        }
        
        if (assignedBase == null)
        {
            SetStateToIdle();
            return;
        }

        BaseTask baseOrder = assignedBase.currentTask;
        if (baseOrder == BaseTask.Idle)
        {
            SetStateToIdle(); 
            return;
        }

        ResourceType nextType = (baseOrder == BaseTask.Iron) ? ResourceType.Iron : ResourceType.Oil;
        SetStateToMine(nextType);
    }

    void ShowDepositText()
    {
        if (FloatingTextManager.I != null)
             FloatingTextManager.I.ShowText(transform.position, $"+{currentLoad}", Color.cyan, 20);
    }

    public void SetStateToMine(ResourceType type)
    {
        if (currentState == WorkerState.Building) return;

        owner.isManualMove = true;

        if (currentLoad > 0)
        {
            if (heldResourceType != type)
            {
                pendingResourceType = type; 
                currentState = WorkerState.ReturningToBase;
                return;
            }
            currentState = WorkerState.ReturningToBase;
            targetResourceType = type; 
            return;
        }

        targetResourceType = type;
        pendingResourceType = null;

        if (assignedBase == null) { SetStateToIdle(); return; }

        ResourceNode node = assignedBase.GetAvailableResource(type);

        if (node != null)
        {
            targetNodeTransform = node.transform;
            targetNodeScript = node;
            currentState = WorkerState.MovingToResource;
            RecalculateWorkerStats();
        }
        else
        {
            AttemptFindNewResourceOrMigrate();
        }
    }

    public void SetStateToAttack()
    {
        if (currentState == WorkerState.Building) return;

        owner.isManualMove = false; 
        currentState = WorkerState.Attack;
        pendingResourceType = null; 
        pendingRepairTarget = null; // 예약 취소
    }

    public void SetStateToBuild(BaseController site)
    {
        targetConstructionSite = site;
        currentState = WorkerState.Building;
        owner.isManualMove = true; 
        pendingRepairTarget = null; // 예약 취소
    }

    // 🏗️ [수정] 건설 로직: 건설 완료 후 행동 분기 처리
    private void ProcessBuilding()
    {
        if (targetConstructionSite == null)
        {
            currentState = WorkerState.Idle;
            return;
        }

        // 건설이 완료되었는가?
        if (targetConstructionSite.isConstructed)
        {
            // 1. 소속 변경 (내 기지가 됨)
            TransferBase(targetConstructionSite);
            BaseController constructedBase = targetConstructionSite; 

            // 타겟 초기화 (더 이상 건설할 게 없음)
            targetConstructionSite = null;

            // 🌟 [핵심 수정] 봇일 경우에만 자동으로 Iron 채굴 시작
            // 플레이어는 "자동 기능이 적용되지 말아야" 하므로 Idle 상태로 둠
            if (isBotMode)
            {
                // 기획: "새로 지어진 Outpost는 Iron 상태여야 하며, 무조건 Iron을 채굴하러 가야 한다"
                Debug.Log($"🤖 [BotWorker] {constructedBase.name} 건설 완료! 즉시 Iron 채굴 시작.");
                SetStateToMine(ResourceType.Iron);
            }
            else
            {
                // 플레이어는 수동 조작 대기
                Debug.Log($"👤 [PlayerWorker] {constructedBase.name} 건설 완료. 명령 대기 중 (Idle).");
                SetStateToIdle();
            }
            return;
        }

        // --- 기존 건설 진행 로직 유지 ---
        float dist = Vector3.Distance(transform.position, targetConstructionSite.transform.position);
        
        if (dist > interactionRange)
        {
            MoveTowards(targetConstructionSite.transform.position);
        }
        else
        {
            targetConstructionSite.Construct(Time.deltaTime);
        }
    }

    public void TransferBase(BaseController newBase)
    {
        if (newBase == null) return;

        if (assignedBase != null && assignedBase.assignedWorkers.Contains(this))
        {
            assignedBase.assignedWorkers.Remove(this);
        }

        assignedBase = newBase;
        if (!assignedBase.assignedWorkers.Contains(this))
        {
            assignedBase.assignedWorkers.Add(this);
        }
    }

    public void SetStateToIdle()
    {
        currentState = WorkerState.Idle;
        owner.isManualMove = false;
        pendingRepairTarget = null;
    }
}