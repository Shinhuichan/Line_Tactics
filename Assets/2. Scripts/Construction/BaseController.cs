using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public enum BaseTask
{
    Iron, // 0 (Default)
    Oil,  // 1
    Idle  // 2
}

public class BaseController : MonoBehaviour
{
    // 📋 맵에 존재하는 모든 기지를 관리하는 정적 리스트
    public static List<BaseController> activeBases = new List<BaseController>();

    [Header("기지 종족 설정")]
    // 🧬 [신규] 초기화 시 UnitData에서 설정됨 (기본값 Humanic)
    public UnitRace buildingRace = UnitRace.Humanic;

    [Header("기지 설정")]
    public float maxHP = 10000f; 
    public float currentHP;

    // 🌟 [신규] UI 표시용 데이터 (Inspector에서 설정 필수)
    [Header("UI Info (Mouse Hover)")]
    public string baseName = "Base"; // 예: "Command Center", "Outpost"
    public Sprite icon;              // UI에 띄울 아이콘 이미지
    
    [Header("명령 상태")]
    public BaseTask currentTask = BaseTask.Iron; 

    [Header("방어 설정")]
    public float detectRange = 15.0f; 

    [Header("건설 설정 (Outpost 전용)")]
    public bool isOutpost = false;      
    public bool isConstructed = true;   
    public float constructionTime = 10f; 
    
    [Header("건설 상태 (Read Only)")]
    public float currentProgress = 0f;  

    [Header("건설 구역 참조")]
    public ConstructionSpot linkedSpot; 

    [Header("소속 노동자")]
    public List<WorkerAbility> assignedWorkers = new List<WorkerAbility>();
    public float resourceScanRange = 10.0f;

    [Header("농성/방어 병력 (Garrison)")]
    // 🏰 [신규] 기지 내부에 주둔한 유닛 리스트
    public List<UnitController> garrisonedUnits = new List<UnitController>();

    [Header("UI 연결")]
    public Slider hpSlider;
    public Slider constructionSlider; 
    public Image hpFillImage; 
    public Color colorHigh = Color.green;
    public Color colorMedium = Color.yellow;
    public Color colorLow = new Color(1f, 0.5f, 0f); 
    public Color colorCritical = Color.red;

    public Transform hitPoint;

    // 🧬 [신규] 종족 특성 관리 변수 (데모닉 재생용)
    private float raceTraitTimer = 0f;
    private const float DEMONIC_REGEN_INTERVAL = 5.0f;
    private const float DEMONIC_REGEN_AMOUNT = 5.0f;

    public bool IsBeingRepaired
    {
        get
        {
            foreach (var w in assignedWorkers)
            {
                if (w.currentState == WorkerState.Repairing) return true;
            }
            return false;
        }
    }

    // 🌟 UI(WorkerSlotUI) 연동 프로퍼티
    public bool HasIronNear => GetAvailableResource(ResourceType.Iron) != null;
    public bool HasOilNear => GetAvailableResource(ResourceType.Oil) != null;

    void Awake()
    {
        activeBases.Add(this);
        currentHP = isConstructed ? maxHP : 100f; 
    }

    void OnDestroy()
    {
        activeBases.Remove(this);
        if (linkedSpot != null)
        {
            linkedSpot.FreeSpot();
        }
    }

    // 🔍 [기존] 특정 자원을 가진 아군 기지를 찾는 정적 함수 (단순 검색)
    public static BaseController FindBaseWithResource(ResourceType type, string teamTag)
    {
        foreach (var baseCtrl in activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.isConstructed) continue;
            if (!baseCtrl.CompareTag(teamTag)) continue;

            if (baseCtrl.GetNearestResourceNode(type) != null)
            {
                return baseCtrl;
            }
        }
        return null; // 없음
    }

    // 🌟 [신규] 특정 위치에서 '가장 가까운' 자원 보유 기지 찾기 (Bot 명령용)
    public static BaseController FindNearestBaseWithResource(ResourceType type, string teamTag, Vector3 fromPos)
    {
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var baseCtrl in activeBases)
        {
            if (baseCtrl == null) continue;
            if (!baseCtrl.isConstructed) continue;
            if (!baseCtrl.CompareTag(teamTag)) continue;

            // 해당 기지 주변에 요청한 자원이 있는지 확인
            if (baseCtrl.GetNearestResourceNode(type) != null)
            {
                float dst = Vector3.Distance(fromPos, baseCtrl.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestBase = baseCtrl;
                }
            }
        }
        return bestBase;
    }

    public ResourceNode GetNearestResourceNode(ResourceType type)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        float minDst = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type && node.currentAmount > 0)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    bestNode = node;
                }
            }
        }
        return bestNode;
    }

    // 🌟 [신규] 기지 주변의 해당 자원 총량을 계산하는 함수 (스마트 확장용)
    public int GetSurroundingResourceAmount(ResourceType type)
    {
        int totalAmount = 0;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        
        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type)
            {
                totalAmount += node.currentAmount;
            }
        }
        return totalAmount;
    }

    // 🌟 [수정] 데이터 주입 함수 (체력 동기화 로직 추가)
    public void Initialize(UnitData data, string teamTag)
    {
        if (data == null) return;

        // 1. 기본 스펙 적용
        this.baseName = data.unitName;
        this.maxHP = data.hp;
        this.constructionTime = data.constructionTime; 
        this.buildingRace = data.race;
        this.icon = data.icon; 

        // 2. 인게임 외형(Sprite) 교체
        if (data.worldSprite != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = data.worldSprite;
            }
        }

        // 3. 태그 설정
        this.gameObject.tag = teamTag;

        // 🌟 [핵심] 이미 건설된 기지(시작 기지)라면 체력과 UI를 새 데이터에 맞게 갱신
        if (isConstructed)
        {
            currentHP = maxHP; // 체력 꽉 채우기
            currentProgress = 1f;
            
            // 슬라이더 최대값 갱신이 중요함 (10000 -> 8000 등으로 변경 시)
            if (hpSlider != null) hpSlider.maxValue = maxHP;
            
            UpdateUI();
        }
    }

    void Start()
    {
        currentTask = BaseTask.Iron;

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = isOutpost ? "Outpost" : "Command Center";
        }

        // Initialize가 Start보다 늦게 호출될 수도 있으므로, 여기서도 체크
        if (isConstructed)
        {
            currentHP = maxHP;
            currentProgress = 1f;
            if (constructionSlider != null) constructionSlider.gameObject.SetActive(false);
        }
        else
        {
            currentHP = maxHP * 0.1f; 
            currentProgress = 0f;
            if (constructionSlider != null) 
            {
                constructionSlider.gameObject.SetActive(true);
                constructionSlider.value = 0f;
            }
        }
        
        // 슬라이더 초기값 설정
        if (hpSlider != null) hpSlider.maxValue = maxHP;
        UpdateUI();
    }

    void Update()
    {
        // 건설 완료된 상태에서만 특성 발동
        if (isConstructed)
        {
            HandleRaceTraits();
        }
    }

    // 🧬 [신규] 종족별 패시브 (건물용)
    void HandleRaceTraits()
    {
        if (buildingRace == UnitRace.Demonic)
        {
            // 데모닉: 자가 재생 (Repairing 중이 아닐 때도 발동)
            if (currentHP < maxHP)
            {
                raceTraitTimer += Time.deltaTime;
                if (raceTraitTimer >= DEMONIC_REGEN_INTERVAL)
                {
                    raceTraitTimer = 0f;
                    // 텍스트 없이 조용히 회복
                    Repair(DEMONIC_REGEN_AMOUNT);
                }
            }
            else
            {
                raceTraitTimer = 0f;
            }
        }
    }

    // 🏰 [신규] 유닛 주둔 (Garrison)
    // 유닛이 기지로 대피할 때 호출됩니다.
    public void GarrisonUnit(UnitController unit)
    {
        if (!garrisonedUnits.Contains(unit))
        {
            garrisonedUnits.Add(unit);
            
            // 유닛을 비활성화하여 숨김 처리 (벙커/커맨드센터 들어간 효과)
            unit.gameObject.SetActive(false);
            
            // (옵션) 체력 회복 로직 등을 여기서 추가 가능
        }
    }

    // 🏰 [신규] 주둔 유닛 모두 해방
    // 농성이 풀리거나 기지가 파괴될 때 호출됩니다.
    public void ReleaseAllGarrisoned()
    {
        for (int i = garrisonedUnits.Count - 1; i >= 0; i--)
        {
            UnitController unit = garrisonedUnits[i];
            if (unit != null)
            {
                unit.gameObject.SetActive(true);
                // 기지 주변 랜덤 위치로 배치 (겹치지 않게)
                unit.transform.position = transform.position + (Vector3)Random.insideUnitCircle * 3.0f;
                
                // 유닛에게 "나왔다"고 알려줄 필요가 있다면 여기서 호출
                // 예: unit.StopMoving();
            }
        }
        garrisonedUnits.Clear();
    }

    public void Construct(float workAmount)
    {
        if (isConstructed) return;

        float progressIncrease = workAmount / constructionTime;
        currentProgress += progressIncrease;

        float hpIncrease = maxHP * progressIncrease;
        currentHP += hpIncrease;
        if (currentHP > maxHP) currentHP = maxHP;

        if (currentProgress >= 1.0f)
        {
            currentProgress = 1.0f;
            isConstructed = true;
            OnConstructionComplete();
        }

        UpdateUI();
    }

    void OnConstructionComplete()
    {
        if (constructionSlider != null) constructionSlider.gameObject.SetActive(false);
        if (currentTask == BaseTask.Idle) currentTask = BaseTask.Iron;

        Debug.Log($"{gameObject.name} 건설 완료! 현재 명령: {currentTask}");

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "건설 완료!", Color.cyan, 30);
    }

    public void Repair(float amount)
    {
        if (currentHP >= maxHP) return;

        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;

        UpdateUI();

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, $"+{Mathf.RoundToInt(amount)}", Color.green, 25);
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        ShowDamageText(damage);
        UpdateUI();

        if (currentHP <= 0)
        {
            DestroyBase();
        }
    }

    void DestroyBase()
    {
        // 1. 일꾼 해방
        for (int i = assignedWorkers.Count - 1; i >= 0; i--)
        {
            if (assignedWorkers[i] != null)
            {
                assignedWorkers[i].assignedBase = null;
                assignedWorkers[i].SetStateToIdle();
            }
        }
        assignedWorkers.Clear();

        // 2. [신규] 농성 병력 해방 (기지가 터지면 쏟아져 나옴)
        ReleaseAllGarrisoned(); 

        Debug.Log($"{gameObject.name} 파괴됨!");
        Destroy(gameObject);
    }

    void UpdateUI()
    {
        if (hpSlider != null)
        {
            // MaxValue가 바뀌었을 수 있으므로 안전하게 다시 할당
            // (최적화를 원하면 Initialize나 Start에서만 해도 되지만, 안전을 위해 유지)
            hpSlider.maxValue = maxHP; 
            hpSlider.value = currentHP;
        }

        if (constructionSlider != null && !isConstructed)
        {
            constructionSlider.value = currentProgress;
        }
        UpdateHealthColor();
    }

    void UpdateHealthColor()
    {
        if (hpFillImage == null) return;
        float ratio = currentHP / maxHP;

        if (ratio > 0.75f) hpFillImage.color = colorHigh;
        else if (ratio > 0.5f) hpFillImage.color = colorMedium;
        else if (ratio > 0.25f) hpFillImage.color = colorLow;
        else hpFillImage.color = colorCritical;
    }

    void ShowDamageText(float damage)
    {
        if (FloatingTextManager.I == null) return;
        Vector3 spawnPos = hitPoint != null ? hitPoint.position : transform.position + Vector3.up * 1.5f;
        FloatingTextManager.I.ShowText(spawnPos, $"-{Mathf.RoundToInt(damage)}", Color.red, 20);
    }

    public ResourceNode GetAvailableResource(ResourceType type)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, resourceScanRange);
        float minDst = Mathf.Infinity;
        ResourceNode bestNode = null;

        foreach (var hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node != null && node.resourceType == type && node.currentAmount > 0)
            {
                float d = Vector3.Distance(transform.position, node.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    bestNode = node;
                }
            }
        }
        return bestNode;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, resourceScanRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }

    // ==================================================================================
    // 🖱️ [신규] 마우스 호버 시 정보창 표시 (Collider2D 필요)
    // ==================================================================================
    private void OnMouseEnter()
    {
        // 1. UI 패널이 있고, 아이콘이 설정되어 있을 때만
        if (UnitInfoPanel.I != null && icon != null)
        {
            UnitInfoPanel.I.ShowBaseInfo(this);
        }
    }

    private void OnMouseExit()
    {
        if (UnitInfoPanel.I != null)
        {
            UnitInfoPanel.I.HideInfo();
        }
    }
}