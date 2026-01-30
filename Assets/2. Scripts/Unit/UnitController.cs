using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum UnitType
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Humanic Units ///
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    Swordsman = 0, 
    Archer,    
    Shielder,
    Cavalry,
    Worker,
    Healer, // 🚑 [추가] 치유병
    Mage, // 🔮 [추가] 마법병
    Assassin, // 🗡️ [추가] 암살병
    BaseArcher, // 🏰 [추가] 성채 장궁병
    Balloon, // 🎈 [신규] 열기구 추가
    FlagBearer, // 🚩 [신규] 기수병 추가
    Spearman, // 🔱 [신규] 장창병
    Ballista, // 🏹 [신규] 노포병
    None, // 🚫 [신규] '없음' 상태를 나타내기 위해 맨 끝에 추가 (기존 순서 유지 필수!)


    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Demonic Units ///
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    Skirmisher = 100,
    Bomber = 101,
    Corpse = 102,
    Gluttony = 103,
    Harpooner = 104,
    Succubus = 105,
    Necromancer = 106,
    Skeleton = 107,
    Medusa = 108,
    Trumpeter = 109,
    Gargoyle = 110,
    Giant = 111,
    BaseCorpse = 112,
    // ⛓️ [신규] 노예병 (데모닉 전용 일꾼)
    // WorkerAbility를 공유하지만, 독립적인 데이터와 풀링을 가집니다.
    Slave = 113
}

public class UnitController : MonoBehaviour
{
    // 📋 [신규] 맵에 존재하는 모든 유닛을 관리하는 정적 리스트 (최적화용)
    public static List<UnitController> activeUnits = new List<UnitController>();

    [Header("현재 상태 (Read Only)")]
    [SerializeField] public UnitType unitType;
    // 🌟 [수정] 기본 스탯(Base)과 실제 스탯 변수 분리
    // Inspector에서는 초기값 확인용으로만 보입니다.
    [Header("기본 스탯 (Base)")]
    [SerializeField] private float baseMaxHP;
    [SerializeField] private float baseDefense;     
    [SerializeField] private float baseMoveSpeed;
    [SerializeField] private float baseAttackDamage;
    [SerializeField] private float baseAttackCooldown; // 🌟 쿨타임 원본 저장용 추가

    [Header("분대 정보")]
    public Squad assignedSquad; // 내가 소속된 분대 (null이면 무소속)

    [Header("버프 상태 (Buffs)")]
    // 🎺 [신규] 나팔병 전용 데미지 버프 (곱연산)
    // 0.1f = 10% 증가. 이 값은 다른 승수들과 별개로 최종 단계에서 계산됨.
    private float trumpeterBuffVal = 0f;
    private float trumpeterBuffTimer = 0f;
    // 🩸 [신규] 살육의 나팔(업그레이드) 적용 여부
    private bool isSlaughterBuffActive = false;

    public bool HasTrumpeterBuff => trumpeterBuffTimer > 0;
    // ⚡ [신규] 일시적 공속 버프 변수 (작살병 시너지 등)
    private float tempAttackSpeedBuffVal = 0f;
    private float tempAttackSpeedBuffTimer = 0f;
    
    [Header("현재 스탯 (Calculated)")]
    public float maxHP;
    public float defense;
    public float moveSpeed;
    public float attackDamage;
    public float currentHP; // 현재 체력

    [Header("설정")]
    public float attackRange;
    public float detectRange = 6.0f;
    public float attackCooldown;

    [Header("버프 승수 (Multipliers)")]
    // 1.0f가 기본값. 1.25f면 25% 증가.
    public float multiplierAttack = 1.0f;
    public float multiplierMoveSpeed = 1.0f;
    public float multiplierCooldown = 1.0f; // 나누기 연산에 사용 (속도 증가 = 쿨타임 감소)

    public bool isRangedUnit;
    public bool isFlyingUnit;
    public bool isStealthed = false;
    public bool isManualMove = false;
    // 데이터 캐싱용 (Initialize에서 설정)
    public bool isMechanical { get; private set; }

    [Header("상태")]
    private float lastAttackTime;
    public bool isDead = false;
    private float bonusDefenseBuff;

    [Header("공격 이동 (Attack Move)")]
    private bool isAttackMoving = false;
    private Vector3 attackMoveTarget;

    [Header("참조")]
    public string enemyTag; 
    public string targetBaseTag;
    // 🏠 [신규] 내 기지 태그 (복귀용)
    public string myBaseTag;

    [Header("UI 연결")]
    public Slider hpSlider; 
    public Image hpFillImage; 

    [Header("체력바 색상")]
    public Color colorHigh = Color.green;       
    public Color colorMedium = Color.yellow;    
    public Color colorLow = new Color(1f, 0.5f, 0f); 
    public Color colorCritical = Color.red;     

    private Transform myTransform;
    private UnitAbility myAbility;

    // 🛡️ [수정] 수비 시 위치 잡기용 변수
    public float defendDistance; // 데이터에서 받아올 거리
    private float randomOffsetX;  // 🎲 수비 시 좌우 랜덤 배치용 (-2.5 ~ 2.5)
    private float siegeRandomX;   // 🎲 농성 시 내부 랜덤 배치용
    private float siegeRandomY;   // 🎲 농성 시 내부 랜덤 배치용

    // 🚩 [신규] 기수병 이동 계산용 타이머 (매 프레임 계산은 무거우므로)
    private float aiThinkTimer = 0f;
    private Vector3 currentBestBuffPos;

    // 🔥 [신규] 화상 데이터 상수화 (모든 유닛 공통 적용)
    // const는 컴파일 시점에 값이 결정되므로, 여기서 바꾸면 이걸 쓰는 모든 곳이 바뀝니다.
    public const float BURN_DAMAGE_PER_SEC = 5.0f;
    public const float BURN_DURATION = 3.0f;

    // ☠️ [신규] 독 상수 (1초당 1데미지, 받는 피해 5% 증가)
    public const float POISON_DAMAGE_PER_SEC = 1.0f;
    public const float POISON_AMP_RATIO = 0.05f; // 5%
    // [신규] 감전 상수
    public const float SHOCK_DAMAGE = 1.0f;
    public const float SHOCK_INTERVAL = 0.5f;
    [Header("보호막 (Shield)")]
    public float currentShield = 0f;
    private GameObject shieldInstance; // 생성된 보호막 프리팹 인스턴스
    private GameObject racialShieldPrefab; // 🛡️ 데이터에서 받아올 프리팹 저장

    [Header("둔화 (Slow)")]
    public bool isSlowed = false; // 외부 확인용 public
    private float slowTimer = 0f;
    private float currentSlowIntensity = 0f; // 0.2 = 20% 느려짐
    private const float SLOW_DURATION_FIXED = 3.0f; // 고정 3초

    // ☠️ [신규] 상태 이상 확인용 프로퍼티 (기존에 추가된 것들에 둔화 추가)
    public bool IsSlowed => isSlowed;

    [Header("상태 이상 (Debuffs)")]
    private float burnTimer = 0f;       // 화상 남은 시간
    private float currentBurnDps = 0f; // 현재 적용중인 화상 데미지
    private float burnTickTimer = 0f;   // 1초마다 데미지 주기 위한 타이머
    private bool isBurning = false;

    // ☠️ [신규] 독 관련
    public bool isPoisoned = false; // 독은 시간 제한 없이 상태로 관리
    private float poisonTickTimer = 0f;

    // ⚡ [신규] 감전(Shock) 관련
    public bool isShocked = false;
    private float shockTimer = 0f;      // 남은 감전 시간
    private float shockTickTimer = 0f;  // 데미지 주기 체크

    [Header("상태 이상: 제어 불가 (CC기)")]
    public bool isStunned = false; // 기절 (행동 불가)
    public bool isForcedMoving = false; // 넉백/당겨짐 중 (이동/공격 불가)
    private float stunTimer = 0f;
    // 💤 [신규] 수면 상태
    public bool isSleeping = false;
    // 🗿 [신규] 석화 상태 (행동 불가 + 사망 대기)
    public bool isPetrified = false;
    // 🐍 [신규] 회복 불가 상태 (치유 차단)
    public bool isUnhealable = false;
    private float unhealableTimer = 0f;

    // 🛑 행동 불가 체크에 석화 추가
    public bool IsCrowdControlled => isStunned || isForcedMoving || isSleeping || isPetrified;

    // ☠️ [신규] 상태 이상 확인용 프로퍼티 (외부 접근용)
    public bool IsBurning => isBurning;
    public bool IsPoisoned => isPoisoned;
    public bool IsShocked => isShocked; // 외부 접근용 프로퍼티
    

    [Header("종족 특성 (Race Traits)")]
    public UnitRace unitRace; // 🧬 Initialize에서 설정됨
    private float raceTraitTimer = 0f; // 특성 발동용 타이머

    // 데모닉 특성 상수 (나중에 업그레이드로 변동 가능하게 변수화 추천)
    private const float DEMONIC_REGEN_INTERVAL = 5.0f;
    private const float DEMONIC_REGEN_AMOUNT = 5.0f;

    // ⏱️ [신규] 비전투 감지용 타이머
    private float lastDamageTime = 0f;
    private const float OUT_OF_COMBAT_TIME = 5.0f; // 5초간 안 맞으면 비전투로 간주
    private const float SHIELD_REGEN_RATE = 10.0f; // 초당 재생량 (적절히 조절)

    // 🌟 [신규] 호버 시 보여줄 원본 데이터 저장용 변수
    private UnitData _linkedData;



    [Header("물리 및 이동 설정 (Physics & Steering)")]
    private Rigidbody2D rb;
    private CircleCollider2D col;

    [Header("레이어 설정 (Ghost Mode)")]
    // GhostMode 관련 변수는 이제 불필요하므로 제거해도 되지만, 
    // 기존 코드와의 호환성을 위해 남겨두되 로직은 비워둡니다.
    public bool isGhost { get; private set; }

    // ⚙️ [수정] 겹침을 허용하므로 분리 힘(Separation)은 제거하거나 매우 약하게 둡니다.
    // 완전히 겹쳐지는 것을 원하시므로 Weight를 0으로 설정합니다.
    public float separationWeight = 0f; 
    public float separationRadius = 1.0f;
    
    // 이동 벡터 계산용
    private Vector2 currentVelocity;
    private Vector2 smoothDampVelocity;



    void Awake()
    {
        myTransform = transform;
        myAbility = GetComponent<UnitAbility>();
        
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) 
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = 5f;
        }

        col = GetComponent<CircleCollider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
        }

        // 🚀 [해결책] 물리 충돌 끄기 (겹침 허용)
        // isTrigger를 켜면 물리적인 밀어내기가 사라집니다.
        // 마우스 클릭 감지나 트리거 이벤트는 여전히 작동합니다.
        col.isTrigger = true; 
    }

    // 👻 GhostMode 함수: 이제 물리 충돌을 Trigger로 해결했으므로 기능이 필요 없습니다.
    // 호출 오류 방지를 위해 빈 함수로 남겨둡니다.
    public void SetGhostMode(bool enable)
    {
        isGhost = enable;
        // 아무것도 하지 않음 (항상 겹침 허용)
    }

    // 🌟 [수정] 활성화 시 리스트 등록
    void OnEnable()
    {
        activeUnits.Add(this);
        
        // (기존 OnEnable 내용이 있다면 여기에 유지, 현재 보내주신 파일엔 없어서 추가함)
        if (hpSlider != null) hpSlider.gameObject.SetActive(true);
        UpdateHealthColor();
    }

    void OnDisable()
    {
        activeUnits.Remove(this);

        // ✅ [수정] 핸들러 함수 해제
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted -= OnUpgradeCompletedHandler;
    }

    // 🌟 [신규] 이벤트 핸들러: 내 팀의 업그레이드일 때만 반응
    private void OnUpgradeCompletedHandler(string teamTag)
    {
        // 업그레이드를 완료한 진영이 나와 같은 태그(Player 또는 Enemy)일 때만 스탯 재계산
        if (gameObject.CompareTag(teamTag))
        {
            RecalculateStats();
        }
    }

    public void Initialize(UnitData data, string myTag)
    {
        // 🌟 [신규] 데이터 캐싱
        this._linkedData = data;
        this.unitType = data.type;
        this.unitRace = data.race;
        this.racialShieldPrefab = data.racialShieldPrefab;
        this.baseMaxHP = data.hp;
        this.baseDefense = data.defense;
        this.baseMoveSpeed = data.moveSpeed;
        this.baseAttackDamage = data.attackDamage;
        this.baseAttackCooldown = data.attackCooldown;
        this.isMechanical = data.isMechanical;
        
        this.attackRange = data.attackRange;
        this.detectRange = data.detectRange;
        this.attackCooldown = data.attackCooldown;
        this.isRangedUnit = data.isRangedUnit;
        this.isFlyingUnit = data.isFlyingUnit;
        
        this.defendDistance = data.defendDistance;
        this.randomOffsetX = Random.Range(-2.5f, 2.5f);
        this.siegeRandomX = Random.Range(-0.5f, 0.5f);
        this.siegeRandomY = Random.Range(-0.5f, 0.5f);

        this.raceTraitTimer = 0f;

        this.gameObject.tag = myTag;
        if (myTag == "Player")
        {
            enemyTag = "Enemy"; targetBaseTag = "Enemy"; myBaseTag = "Player";
            myTransform.rotation = Quaternion.identity;
        }
        else
        {
            enemyTag = "Player"; targetBaseTag = "Player"; myBaseTag = "Enemy";
            myTransform.rotation = Quaternion.Euler(0, 0, 180);
        }

        if (unitRace == UnitRace.Angelic && racialShieldPrefab != null)
        {
            ApplyShield(maxHP * 0.2f, racialShieldPrefab);
        }

        // 초기 승수 리셋
        multiplierAttack = 1.0f;
        multiplierMoveSpeed = 1.0f;
        multiplierCooldown = 1.0f;

        this.isDead = false;
        InitUI(); // UI 슬라이더 연결

        // 🛑 [버그 수정] Ability 초기화 전에 현재 스탯에 기본값을 채워넣습니다.
        // 이유: CavalryAbility 등이 Initialize 시점에 owner.moveSpeed를 캐싱하는데,
        // 이때 값이 0이면 돌진 속도도 0이 되어 움직이지 않는 버그가 발생함.
        this.moveSpeed = this.baseMoveSpeed;
        this.attackDamage = this.baseAttackDamage;
        this.maxHP = this.baseMaxHP; // RecalculateStats에서 다시 덮어씌워지므로 안전함

        // 🛠️ [핵심 수정 1] Ability 초기화를 스탯 계산보다 '먼저' 해야 함!
        // 그래야 GiantAbility가 owner를 알고 있는 상태에서 UpdateGiantStats를 수행할 수 있음.
        if (myAbility != null) myAbility.Initialize(this);

        // 스탯 계산 (이제 Ability가 owner를 아는 상태이므로 안전함)
        // 여기서 실제 업그레이드 등이 반영된 최종 스탯이 계산됨
        RecalculateStats();
        
        // 초기화 시점에는 체력을 가득 채움 (RecalculateStats 이후에 설정)
        this.currentHP = this.maxHP;
        if (hpSlider != null) hpSlider.value = currentHP;
    }

    // ⚔️ [신규] 공격 이동 명령 (EnemyTacticsManager에서 호출)
    public void SetStateToAttackMove(Vector3 target)
    {
        isAttackMoving = true;
        attackMoveTarget = target;
        isManualMove = false; // 봇 제어이므로 수동 조작 해제
    }

    // 🌟 [핵심 수정] 스탯 재계산 로직
    public void RecalculateStats()
    {
        // 🛠️ [핵심 수정 2] 기존 유닛 업그레이드 대응을 위한 체력 비율 저장
        float oldMaxHP = maxHP;
        float hpRatio = (oldMaxHP > 0 && currentHP > 0) ? (currentHP / oldMaxHP) : 1.0f;

        if (UpgradeManager.I == null)
        {
            // 매니저 없을 때 기본값 로직 (기존과 동일)
            maxHP = baseMaxHP;
            defense = baseDefense;
            
            float slowFactor = isSlowed ? (1.0f - currentSlowIntensity) : 1.0f;
            moveSpeed = (baseMoveSpeed * multiplierMoveSpeed) * slowFactor;

            float damageBuffMultiplier = 1.0f + trumpeterBuffVal;
            attackDamage = (baseAttackDamage * multiplierAttack) * damageBuffMultiplier;
            
            attackCooldown = baseAttackCooldown / multiplierCooldown;
            
            // 매니저가 없어도 체력 변동 시 비율 유지 적용
            if (oldMaxHP > 0 && maxHP != oldMaxHP)
            {
                 currentHP = maxHP * hpRatio;
            }
            return;
        }

        string myTag = gameObject.tag; 

        // 1. 기본 업그레이드 매니저 스탯
        float hpBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.MaxHP, myTag);
        float defBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.Defense, myTag);
        float spdBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.MoveSpeed, myTag);
        float atkBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.AttackDamage, myTag);

        // 2. 척후병 전용 업그레이드 체크
        float skirmisherSpeedMult = 1.0f;
        float skirmisherAtkSpdMult = 1.0f;

        // 🦶 거인병 거대화 배율
        float giantGrowthMultiplier = 1.0f;

        if (unitType == UnitType.Skirmisher && UpgradeManager.I.IsAbilityActive("SKIRMISHER_FRENZY", myTag))
        {
            skirmisherSpeedMult = 1.25f;
            skirmisherAtkSpdMult = 1.25f; 
        }

        // B. 거인병 로직 (거대화 I, II)
        if (unitType == UnitType.Giant)
        {
            if (UpgradeManager.I.IsAbilityActive("GIANT_GROWTH_2", myTag))
            {
                giantGrowthMultiplier = 1.5f; 
            }
            else if (UpgradeManager.I.IsAbilityActive("GIANT_GROWTH_1", myTag))
            {
                giantGrowthMultiplier = 1.25f;
            }

            // 시각적 크기 변경
            transform.localScale = Vector3.one * giantGrowthMultiplier;

            // 광역 공격 범위 연동 (이제 안전함)
            GiantAbility giantAbility = GetComponent<GiantAbility>();
            if (giantAbility != null)
            {
                giantAbility.UpdateGiantStats(giantGrowthMultiplier);
            }
        }

        // 3. 최종 스탯 계산
        maxHP = (baseMaxHP + hpBonus) * giantGrowthMultiplier;
        defense = baseDefense + defBonus + bonusDefenseBuff; 
        
        float rangeBase = (_linkedData != null) ? _linkedData.attackRange : attackRange;
        float rangeBonus = 0f;
        if (unitType == UnitType.Harpooner && UpgradeManager.I.IsAbilityActive("ENHANCED_HARPOON", myTag)) rangeBonus = 1.0f;
        
        attackRange = (rangeBase * giantGrowthMultiplier) + rangeBonus;

        float finalSlowFactor = isSlowed ? (1.0f - currentSlowIntensity) : 1.0f;
        moveSpeed = ((baseMoveSpeed + spdBonus) * multiplierMoveSpeed * skirmisherSpeedMult) * finalSlowFactor;

        float finalDamageBuff = 1.0f + trumpeterBuffVal;
        attackDamage = ((baseAttackDamage + atkBonus) * multiplierAttack) * finalDamageBuff * giantGrowthMultiplier;

        float slaughterSpeedMult = (HasTrumpeterBuff && isSlaughterBuffActive) ? 1.1f : 1.0f;
        float totalCooldownMult = multiplierCooldown * skirmisherAtkSpdMult * (1.0f + tempAttackSpeedBuffVal);
        attackCooldown = baseAttackCooldown / totalCooldownMult;

        // UI 갱신
        if (hpSlider != null) hpSlider.maxValue = maxHP;

        // 🛠️ [핵심 수정 3] 이미 소환된 유닛도 업그레이드 시 체력이 비율대로 늘어나야 함
        // (예: 체력 50/100 상태에서 MaxHP가 150이 되면 -> 75/150이 됨)
        // 단, 새로 생성되는 순간(Initialize)에는 currentHP가 초기화되기 전이므로 적용하지 않음 (Initialize 마지막에 maxHP로 덮어씌움)
        if (oldMaxHP > 0 && Mathf.Abs(oldMaxHP - maxHP) > 0.1f) 
        {
            currentHP = maxHP * hpRatio;
        }

        // 체력바 UI 색상 등 갱신
        UpdateHealthColor();
    }

    // 🛠️ 외부(Ability)에서 버프/디버프 걸 때 호출
    public void SetMultipliers(float atkMult, float spdMult, float cdMult)
    {
        multiplierAttack = atkMult;
        multiplierMoveSpeed = spdMult;
        multiplierCooldown = cdMult;
        RecalculateStats(); // 즉시 반영
    }

    void Update()
    {
        if (isDead) return;

        // 1. 상태 이상 및 버프 관리
        HandleBurnStatus();
        HandlePoisonStatus();
        HandleShockStatus();
        HandleSlowStatus();
        HandleStunStatus();
        HandleUnhealableStatus();
        HandleTrumpeterBuff();
        HandleAttackSpeedBuff();

        if (isPetrified) return;
        if (IsCrowdControlled || isShocked) 
        {
            StopMoving();
            return;
        }

        // 2. 종족 특성
        HandleRaceTraits();

        // 3. 농성(Siege) 명령 최우선 처리
        if (CheckAndProcessSiege()) return;

        // 4. 스킬(Ability) 업데이트
        if (myAbility != null)
        {
            myAbility.OnUpdate();
            if (myAbility.IsBusy) 
            {
                StopMoving();
                return;
            }
        }

        // 5. 메인 행동 결정 (전투 및 이동)
        // ❌ 기존 Update 내의 중복된 타겟팅/이동 로직을 모두 삭제하고 이 함수 하나로 통합합니다.
        ProcessMainBehavior();
    }

    // 🌟 [핵심] 행동 결정 메인 함수 (수정됨)
    void ProcessMainBehavior()
    {
        // A. 특수 유닛 이동 (전투 안 함)
        if (unitType == UnitType.FlagBearer || unitType == UnitType.Trumpeter)
        {
            if (!isManualMove) 
            {
                if (unitType == UnitType.FlagBearer) MoveToBestBuffPosition();
                else MoveToAlly(); 
            }
            return;
        }

        // B. 전투 로직
        bool isSiegeMode = false;
        if (gameObject.CompareTag("Player") && TacticalCommandManager.I != null)
             isSiegeMode = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        else if (gameObject.CompareTag("Enemy"))
             isSiegeMode = (EnemyBot.enemyState == TacticalState.Siege);

        // 치유병 로직
        if (unitType == UnitType.Healer)
        {
            if (!isManualMove) ProcessHealerMove();
            return;
        }

        // 공격 가능 여부 체크
        bool canAttack = true;
        if (isSiegeMode && !isRangedUnit && !IsStaticUnit) canAttack = false;

        GameObject validTarget = null;
        
        if (canAttack) validTarget = FindBestTarget(); 

        // C. 최종 행동 실행 (공격 vs 이동)
        if (validTarget != null)
        {
            // 적이 있으면 공격 (Attack Move 중이라도 멈춰서 공격함)
            RotateTowards(validTarget.transform.position);
            AttemptAttack(validTarget);
            StopMoving(); 
        }
        else if (isAttackMoving) // 🌟 [추가] 공격 이동 상태 확인
        {
            // 적이 없으면 목표 지점으로 이동
            MoveToPosition(attackMoveTarget);
            
            // 목표 도달 시 Attack Move 해제 (선택 사항)
            if (Vector3.Distance(transform.position, attackMoveTarget) < 1.0f)
            {
                isAttackMoving = false;
            }
        }
        else if (!isManualMove) 
        {
            if (IsStaticUnit) 
            {
                StopMoving();
                return;
            }
            ProcessTacticalMove(); // 기본 전술 이동 (Attack Move가 아닐 때만)
        }
        else
        {
            // 수동 이동 중일 때는 아무것도 안 함 (MoveToPosition이 외부에서 호출됨)
        }
    }

    // 🎯 [핵심 수정] 최적의 타겟을 찾는 함수
    GameObject FindBestTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        GameObject bestTarget = null;

        if (unitType == UnitType.Assassin)
        {
            GameObject rangedInReach = null;
            GameObject meleeInReach = null;

            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                GameObject obj = hit.gameObject;

                if (obj.CompareTag(enemyTag) || obj.CompareTag(targetBaseTag))
                {
                    UnitController targetUnit = obj.GetComponent<UnitController>();
                    
                    if (targetUnit != null && targetUnit.isStealthed) continue;

                    // ✈️ [핵심] 암살자도 근거리(isRangedUnit == false)라면 공중 공격 불가
                    if (!this.isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

                    if (targetUnit != null)
                    {
                        if (targetUnit.isRangedUnit) rangedInReach = obj; 
                        else meleeInReach = obj; 
                    }
                    else meleeInReach = obj; 
                }
            }

            if (rangedInReach != null) bestTarget = rangedInReach;
            else if (meleeInReach != null)
            {
                GameObject globalTarget = FindNearestTarget(enemyTag);
                UnitController globalUnit = globalTarget != null ? globalTarget.GetComponent<UnitController>() : null;
                
                if (globalUnit != null && globalUnit.isRangedUnit) return null; 
                
                bestTarget = meleeInReach;
            }
        }
        else 
        {
            float closestDistSqr = Mathf.Infinity;

            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                GameObject target = hit.gameObject;
                
                if (target.CompareTag(enemyTag) || target.CompareTag(targetBaseTag))
                {
                    UnitController targetUnit = target.GetComponent<UnitController>();
                    
                    if (targetUnit != null && targetUnit.isStealthed) continue;

                    // ✈️ [핵심] 근거리 유닛은 비행 유닛 완전 무시
                    if (!this.isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) 
                    {
                        continue; 
                    }

                    float distSqr = (target.transform.position - transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        bestTarget = target;
                    }
                }
            }
        }
        return bestTarget;
    }

    // 🏰 [신규] 농성 상태 확인 및 처리 함수
    bool CheckAndProcessSiege()
    {
        // 건물형 유닛(BaseArcher 등)은 이동 불가하므로 농성 로직 제외
        if (IsStaticUnit) return false;

        bool isSiege = false;

        // 플레이어 확인
        if (CompareTag("Player") && TacticalCommandManager.I != null)
        {
            isSiege = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        }
        // 적군(AI) 확인
        else if (CompareTag("Enemy"))
        {
            // EnemyBot 스크립트에 접근 가능하다고 가정 (static이거나 싱글톤)
            // 컴파일 에러 방지를 위해 실제 프로젝트 구조에 맞춰주세요.
            // 여기서는 기존 코드 스타일을 따릅니다.
             isSiege = (EnemyBot.enemyState == TacticalState.Siege);
        }

        if (isSiege)
        {
            // 1. 가장 가까운 아군 기지 찾기
            BaseController nearestBase = FindNearestBase();

            if (nearestBase != null)
            {
                // 2. 기지 중심부로 이동 및 진입 시도
                if (TryEnterGarrison(nearestBase.transform.position, nearestBase.transform))
                {
                    // 진입 성공! (BaseController에서 SetActive(false) 해줌)
                    return true;
                }

                // 3. 아직 못 들어갔으면 계속 이동
                MoveToHideInPoint(nearestBase.transform.position);
            }
            return true; // 농성 처리 했으므로 true 반환 (Update의 다른 로직 중단)
        }

        return false;
    }

    // 🧬 [신규] 종족별 패시브 로직 관리
    void HandleRaceTraits()
    {
        switch (unitRace)
        {
            case UnitRace.Humanic:
                // 휴머닉은 별도 패시브가 없거나, 추후 구현
                break;

            case UnitRace.Demonic:
                // 매 5초마다 HP 5 회복
                // (풀피가 아닐 때만 타이머가 돔 -> 불필요한 연산 방지)
                if (currentHP < maxHP)
                {
                    raceTraitTimer += Time.deltaTime;
                    if (raceTraitTimer >= DEMONIC_REGEN_INTERVAL)
                    {
                        raceTraitTimer = 0f;
                        // showText: false로 설정하여 화면 도배 방지
                        Heal(DEMONIC_REGEN_AMOUNT, false); 
                    }
                }
                else
                {
                    raceTraitTimer = 0f; // 풀피면 타이머 리셋 (피격 시 0초부터 다시 카운트)
                }
                break;

            case UnitRace.Angelic:
                // 🛡️ 천상의 보호막 (Divine Barrier)
                // 1. 마지막으로 맞은 지 5초가 지났는지 확인
                if (Time.time - lastDamageTime >= OUT_OF_COMBAT_TIME)
                {
                    float maxShield = maxHP * 0.2f; // 최대 보호막 = 체력의 20%

                    // 2. 보호막이 최대치보다 적으면 재생
                    if (currentShield < maxShield)
                    {
                        // 초당 일정량 회복
                        float regen = SHIELD_REGEN_RATE * Time.deltaTime;
                        currentShield += regen;

                        // 최대치 초과 방지
                        if (currentShield > maxShield) currentShield = maxShield;

                        // 3. 시각 효과 켜기 (재생 시작되면 다시 보여야 함)
                        if (racialShieldPrefab != null)
                        {
                            UpdateShieldVisual(true, racialShieldPrefab);
                        }
                    }
                }
                break;
        }
    }

    // 🐍 [신규] 회복 불가 상태 관리
    void HandleUnhealableStatus()
    {
        if (isUnhealable)
        {
            unhealableTimer -= Time.deltaTime;
            if (unhealableTimer <= 0)
            {
                isUnhealable = false;
            }
        }
    }

    // 🐍 [신규] 회복 불가 부여 (메두사 등에서 호출)
    public void ApplyUnhealable(float duration)
    {
        // 기계 유닛도 회복 불가(수리 불가) 상태는 걸릴 수 있다고 가정 (필요 시 isMechanical 체크 추가)
        isUnhealable = true;
        unhealableTimer = duration;

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Rotting...", new Color(0.5f, 0f, 0.5f), 20);
    }

    // ⚡ [신규] 공속 버프 부여 함수
    public void ApplyTemporaryAttackSpeedBuff(float percent, float duration)
    {
        // 더 높은 수치거나, 수치가 같으면 시간 갱신
        if (percent >= tempAttackSpeedBuffVal)
        {
            tempAttackSpeedBuffVal = percent;
            tempAttackSpeedBuffTimer = duration;
            RecalculateStats();
        }
    }

    // ⚡ [신규] 공속 버프 타이머
    void HandleAttackSpeedBuff()
    {
        if (tempAttackSpeedBuffTimer > 0)
        {
            tempAttackSpeedBuffTimer -= Time.deltaTime;
            if (tempAttackSpeedBuffTimer <= 0)
            {
                tempAttackSpeedBuffTimer = 0f;
                tempAttackSpeedBuffVal = 0f;
                RecalculateStats();
            }
        }
    }

    // 🎺 [수정] 나팔 버프 적용 함수 (살육 모드 플래그 추가)
    public void ApplyTrumpeterBuff(float percent, float duration, bool isSlaughterMode = false)
    {
        trumpeterBuffVal = percent;
        trumpeterBuffTimer = duration;
        isSlaughterBuffActive = isSlaughterMode; // 🩸 모드 설정
        
        RecalculateStats();

        if (FloatingTextManager.I != null)
        {
            string msg = isSlaughterMode ? "Slaughter!" : "+DMG!";
            Color color = isSlaughterMode ? new Color(1f, 0.2f, 0.2f) : Color.red;
            FloatingTextManager.I.ShowText(transform.position + Vector3.up, msg, color, 20);
        }
    }

    // 🎺 [신규] 버프 타이머 체크
    void HandleTrumpeterBuff()
    {
        if (trumpeterBuffTimer > 0)
        {
            trumpeterBuffTimer -= Time.deltaTime;
            if (trumpeterBuffTimer <= 0)
            {
                trumpeterBuffTimer = 0f;
                trumpeterBuffVal = 0f;
                isSlaughterBuffActive = false; // 🩸 버프 끝나면 해제
                RecalculateStats(); 
            }
        }
    }

    // =========================================================
    // 🗿 석화 (Petrify) 시스템 - 메두사 전용 (오브젝트 없이 구현)
    // =========================================================
    public void ApplyPetrify(float durationBeforeBreak = 1.5f)
    {
        if (isDead || isPetrified) return; // 이미 죽었거나 돌이면 무시

        StartCoroutine(PetrifyRoutine(durationBeforeBreak));
    }

    private IEnumerator PetrifyRoutine(float duration)
    {
        isPetrified = true; // 제어권 박탈

        // 1. 시각 효과: 회색으로 변색 (돌 느낌)
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray; 
        }

        // 2. 애니메이션 정지 (굳어버림)
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.speed = 0f; 
        }

        // 3. 물리 정지 (밀리지 않음)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; // 넉백 등 물리력 무시
        }

        // 4. 피드백
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Stone...", Color.gray, 30);

        // 5. 부서질 때까지 대기
        yield return new WaitForSeconds(duration);

        // 6. 사망 처리 (부서짐)
        // FinishDeath를 호출하여 깔끔하게 제거
        FinishDeath(); 
    }

    // =========================================================
    // 💤 수면 (Sleep) 시스템
    // =========================================================
    public void ApplySleep()
    {
        if (isDead) return;

        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 수면 면역
        if (isMechanical) return;

        if (!isSleeping)
        {
            isSleeping = true;
            
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Zzz...", new Color(0.5f, 0.7f, 1f), 30);
        }
    }

    public void CureSleep()
    {
        if (!isSleeping) return;

        isSleeping = false;
        // (선택) 수면 이펙트 제거
    }

    // =========================================================
    // ⚡ 기절 (Stun) 시스템
    // =========================================================
    public void ApplyStun(float duration)
    {
        if (isDead) return;

        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 기절 면역
        if (isMechanical) return;
        
        if (duration > stunTimer)
        {
            stunTimer = duration;
        }
        
        if (!isStunned)
        {
            isStunned = true;
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Stunned!", Color.yellow, 25);
        }
    }

    // 1. 기절 해제
    public void CureStun()
    {
        if (!isStunned) return;

        isStunned = false;
        stunTimer = 0f;
        
        // (선택) 기절 이펙트 끄기 등
    }

    void HandleStunStatus()
    {
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                // (선택) 기절 이펙트 비활성화
            }
        }
    }

    // =========================================================
    // 💨 강제 이동 시스템 (넉백 & 당기기)
    // =========================================================
    
    // 1. 넉백 (밀쳐내기)
    public void ApplyKnockback(Vector3 pushDirection, float distance, float duration = 0.2f)
    {
        if (isDead) return;
        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 감전 면역
        if (isMechanical) return;
        StartCoroutine(ForcedMoveRoutine(pushDirection.normalized, distance, duration));
    }

    // 2. 당기기 (Pull) - 작살병 전용
    public void ApplyPull(Vector3 pullSourcePos, float distance, float duration = 0.5f)
    {
        if (isDead) return;
        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 감전 면역
        if (isMechanical) return;
        // 나 -> 적 방향 (당겨지는 방향)
        Vector3 pullDir = (pullSourcePos - transform.position).normalized;
        
        // 당겨지는 동안은 기절 상태로 만듦 (사용자 요청 B안)
        ApplyStun(duration); 
        
        StartCoroutine(ForcedMoveRoutine(pullDir, distance, duration));
    }

    // 🐢 [신규] 둔화 상태 관리
    void HandleSlowStatus()
    {
        if (isSlowed)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                CureSlow(); // 시간 종료 시 해제
            }
        }
    }

    // 🐢 [신규] 둔화 적용 (강한 효과 우선 법칙)
    public void ApplySlow(float intensity)
    {
        if (isDead) return;

        // 1. 처음 걸릴 때
        if (!isSlowed)
        {
            isSlowed = true;
            currentSlowIntensity = intensity;
            slowTimer = SLOW_DURATION_FIXED;
            
            // 텍스트 출력
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Slow!", Color.gray, 20);

            RecalculateStats(); // 🌟 속도 즉시 갱신
        }
        // 2. 이미 걸려있을 때
        else
        {
            // 더 강하거나 같은 둔화가 들어오면 -> 갱신
            if (intensity >= currentSlowIntensity)
            {
                currentSlowIntensity = intensity; // 덮어쓰기
                slowTimer = SLOW_DURATION_FIXED;  // 시간 리셋
                RecalculateStats(); // (수치가 달라졌을 수 있으므로 갱신)
            }
            // 더 약한 둔화 -> 무시 (시간 갱신도 안 함)
        }
    }

    // 통합 이동 코루틴
    private IEnumerator ForcedMoveRoutine(Vector3 direction, float distance, float duration)
    {
        // 중복 실행 방지
        if (isForcedMoving) yield break; 
        isForcedMoving = true;

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (direction * distance);

        while (elapsed < duration)
        {
            if (isDead) break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // 부드러운 이동 (Lerp)
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        isForcedMoving = false;
    }

    // 🐢 [신규] 둔화 해제
    public void CureSlow()
    {
        if (!isSlowed) return;

        isSlowed = false;
        currentSlowIntensity = 0f;
        slowTimer = 0f;
        
        RecalculateStats(); // 🌟 속도 원상복구
    }

    // ⚡ [신규] 감전 상태 관리
    void HandleShockStatus()
    {
        if (isShocked)
        {
            // 타이머 갱신
            shockTimer -= Time.deltaTime;
            shockTickTimer += Time.deltaTime;

            // 0.5초마다 데미지
            if (shockTickTimer >= SHOCK_INTERVAL)
            {
                shockTickTimer = 0f;
                TakeDamage(SHOCK_DAMAGE, true); // 고정 피해
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Zzzt!", Color.yellow, 20);
            }

            // 지속시간 종료 체크
            if (shockTimer <= 0)
            {
                isShocked = false;
                shockTickTimer = 0f;
            }
        }
    }

    // ⚡ [신규] 감전 부여 (노포병 등에서 호출)
    public void ApplyShock(float duration)
    {
        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 감전 면역
        if (isMechanical) return;

        isShocked = true;
        shockTimer = duration;
        shockTickTimer = 0f; 

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Shocked!", Color.yellow, 25);
    }

    // 🚑 [신규] 감전 해제 (치유병 호출용)
    public void CureShock()
    {
        if (isShocked)
        {
            isShocked = false;
            shockTimer = 0f;
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Grounding!", Color.green, 20); // 접지(해제)
        }
    }

    // 🛡️ [수정] 보호막 부여 시 프리팹도 같이 받음
    public void ApplyShield(float amount, GameObject visualPrefab)
    {
        currentShield = amount;

        // 시각 효과 켜기 (프리팹 전달)
        UpdateShieldVisual(true, visualPrefab);

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "+Shield", Color.cyan, 25);
    }

    // 🛡️ [수정] 프리팹 기반 비주얼 관리
    void UpdateShieldVisual(bool isActive, GameObject prefab = null)
    {
        if (isActive)
        {
            // 1. 이미 보호막 오브젝트가 있다면 켜기만 함
            if (shieldInstance != null)
            {
                shieldInstance.SetActive(true);
            }
            // 2. 없다면 프리팹을 사용하여 생성 (자식으로 등록)
            else if (prefab != null)
            {
                shieldInstance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
                shieldInstance.name = "Shield_Effect";
                
                // (옵션) 크기나 위치 미세 조정이 필요하면 여기서
                shieldInstance.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            // 보호막 꺼짐
            if (shieldInstance != null) shieldInstance.SetActive(false);
        }
    }

    // 🚑 [신규] 치유병 전용 이동 로직
    void ProcessHealerMove()
    {
        GameObject target = FindBestHealTarget();
        if (target == null) target = FindNearestAlly();
        if (target == null) { MoveToBase(); return; }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > attackRange * 0.8f)
        {
            MoveToPosition(target.transform.position); // 🌟 변경
        }
        else
        {
            StopMoving();
        }
    }

    // 🚑 [신규] 감지 범위 내에서 "가장 체력 비율이 낮은" 아군 찾기
    GameObject FindBestHealTarget()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectRange);
        UnitController bestCandidate = null;
        float minHpRatio = 1.0f; // 100% 미만인 애들만 찾음

        foreach (var col in colliders)
        {
            // 아군 확인
            if (!col.CompareTag(this.tag)) continue;
            if (col.gameObject == gameObject) continue; // 나 자신 제외

            // 건물(Base, Tower 등)은 제외 (선택사항, 필요하면 포함 가능)
            if (col.GetComponent<BaseController>() != null) continue;

            UnitController ally = col.GetComponent<UnitController>();
            if (ally == null || ally.unitType == UnitType.BaseArcher) continue; // 성채 장궁병 제외

            // 체력이 꽉 찼으면 패스
            if (ally.currentHP >= ally.maxHP) continue;

            // 비율 계산
            float ratio = ally.currentHP / ally.maxHP;

            // 더 위급한 환자 발견!
            if (ratio < minHpRatio)
            {
                minHpRatio = ratio;
                bestCandidate = ally;
            }
        }

        return bestCandidate != null ? bestCandidate.gameObject : null;
    }


    // 🔥 [신규] 화상 치료 (HealerAbility에서 호출)
    public void CureBurn()
    {
        if (isBurning)
        {
            isBurning = false;
            burnTimer = 0f;
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Cure!", Color.green, 20);
        }
    }

    // ☠️ [신규] 독 상태 관리 (무한 지속)
    void HandlePoisonStatus()
    {
        if (isPoisoned)
        {
            poisonTickTimer += Time.deltaTime;

            if (poisonTickTimer >= 1.0f)
            {
                poisonTickTimer = 0f;
                // 독 데미지 (방어 무시)
                TakeDamage(POISON_DAMAGE_PER_SEC, true);

                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Poison", new Color(0.5f, 0f, 1f), 20); // 보라색
            }
        }
    }

    // ☠️ 외부에서 독을 걸 때 호출
    public void ApplyPoison()
    {
        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 중독 면역
        if (isMechanical) return;

        if (!isPoisoned)
        {
            isPoisoned = true;
            poisonTickTimer = 0f; 
        }

        if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Poison", new Color(0.5f, 0f, 1f), 20); // 보라색
    }

    // 🚑 [신규] 독 해제 (힐러 등이 호출 예정)
    public void CurePoison()
    {
        isPoisoned = false;
        // (선택) 독 이펙트 비활성화
    }

    // 🔥 [신규] 화상 상태 관리 함수
    void HandleBurnStatus()
    {
        if (burnTimer > 0)
        {
            burnTimer -= Time.deltaTime;
            burnTickTimer += Time.deltaTime;

            // 1초마다 데미지
            if (burnTickTimer >= 1.0f)
            {
                burnTickTimer = 0f;
                // 방어 무시(True Damage)로 데미지 적용
                TakeDamage(currentBurnDps, true);
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Burn", new Color(1f, 0.5f, 0f), 20);
            }
        }
        else
        {
            isBurning = false;
            // (화상 이펙트 끄기)
        }
    }

    // 🔥 [수정] 매개변수 제거 (상수 사용)
    public void ApplyBurn()
    {
        isBurning = true;
        burnTimer = BURN_DURATION; // 상수값 3.0f 사용
        
        // 🏗️ [수정] 기계/건물 속성(isMechanical)은 화상 피해 3배 적용
        if (isMechanical)
        {
            currentBurnDps = BURN_DAMAGE_PER_SEC * 3.0f;
        }
        else
        {
            currentBurnDps = BURN_DAMAGE_PER_SEC; // 기본값 5.0f
        }

        if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Burn", new Color(1f, 0.5f, 0f), 20);
    }

    // 🛡️ 기수병 오라 같은 일시적 버프 처리용 수정
    public void AddBonusDefense(float amount)
    {
        bonusDefenseBuff += amount;
        RecalculateStats(); // 재계산 트리거
    }

    public void RemoveBonusDefense(float amount)
    {
        bonusDefenseBuff = Mathf.Max(0, bonusDefenseBuff - amount);
        RecalculateStats();
    }

    // UI 초기화 분리
    void InitUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
            if (hpFillImage == null && hpSlider.fillRect != null)
                hpFillImage = hpSlider.fillRect.GetComponent<Image>();
            hpSlider.gameObject.SetActive(true);
        }
    }

    // 🌟 [수정] 감지 범위 내 적 찾기 (근거리 유닛은 공중 유닛 무시)
    GameObject FindEnemyInDetectRange()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectRange);
        GameObject nearest = null;
        float minDistanceSqr = Mathf.Infinity;

        foreach (var col in colliders)
        {
            if (col.CompareTag(enemyTag) || col.CompareTag(targetBaseTag))
            {
                UnitController targetUnit = col.GetComponent<UnitController>();
                if (targetUnit != null)
                {
                    // 1. 은신 체크
                    if (targetUnit.isStealthed) continue;
                    // ✈️ [핵심] 근거리는 공중 유닛 무시
                    if (!this.isRangedUnit && targetUnit.isFlyingUnit) continue;
                }

                float distSqr = (col.transform.position - transform.position).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    nearest = col.gameObject;
                }
            }
        }
        return nearest;
    }

    // -----------------------------------------------------------
    // 👇 [수정 2] 파일 맨 아래쪽에 이 함수 추가 (FindEnemyInDetectRange 근처)
    // -----------------------------------------------------------
    
    // 🔍 [신규] 외부(Ability)에서 감지 범위 내 적 확인용
    public bool HasEnemyInDetectRange()
    {
        return FindEnemyInDetectRange() != null;
    }

    void LateUpdate()
    {
        // 체력바 회전 고정
        if (hpSlider != null)
        {
            hpSlider.transform.rotation = Quaternion.identity;
        }
    }

    // --- 이동 로직 ---

    void ProcessTacticalMove()
    {
        // 1. 특수 유닛 예외
        if (unitType == UnitType.FlagBearer) { MoveToBestBuffPosition(); return; }
        if (unitType == UnitType.Healer) { MoveToAlly(); return; }
        
        // 2. 전술 상태 확인
        bool isSiege = false;
        
        if (gameObject.CompareTag("Player") && TacticalCommandManager.I != null)
             isSiege = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        else if (gameObject.CompareTag("Enemy"))
             isSiege = (EnemyBot.enemyState == TacticalState.Siege);

        // 노동병 로직
        if (unitType == UnitType.Worker)
        {
            if (!isSiege) return; 
        }

        // --------------------------------------------------------
        // 🤖 적군(AI) 로직 - 🌟 [핵심 수정: Player의 RallyPoint 시스템과 동일화]
        // --------------------------------------------------------
        if (gameObject.CompareTag("Enemy"))
        {
            if (EnemyBot.enemyState == TacticalState.Attack) 
            {
                MoveToEnemy();
            }
            else if (EnemyBot.enemyState == TacticalState.Siege) 
            {
                // 적군 전선(최전방 기지) 근처라면 Garrison 진입 시도
                float distToFront = Vector3.Distance(transform.position, EnemyBot.enemyFrontLinePos);
                
                if (distToFront < 20.0f)
                {
                    TryEnterGarrison(EnemyBot.enemyFrontLinePos); 
                }
                else
                {
                    // 전선이 멀면 일단 그쪽으로 이동
                    BaseController frontBase = EnemyBot.enemyFrontLineBase;
                    if (frontBase != null) MoveToRallyPoint(frontBase.transform);
                    else MoveToBase(); // fallback
                }
            }
            else // Defend (기본 상태)
            {
                if (CheckIntercept()) return; 

                // 🛑 [수정] 기존 MoveToBase() 제거 -> TacticsManager가 찍어준 전선 기지로 집결
                // 건설 중인 Outpost도 TacticsManager가 frontBase로 지정하므로, 모든 유닛이 거기로 몰려갑니다.
                BaseController targetBase = EnemyBot.enemyFrontLineBase;
                if (targetBase != null)
                {
                    MoveToRallyPoint(targetBase.transform);
                }
                else
                {
                    // 만약 전선 기지가 없다면(파괴됨 등), 기존 로직대로 가장 가까운 기지로
                    MoveToBase(); 
                }
            }
            return;
        }

        /// --------------------------------------------------------
        // 👤 아군(Player) 전술 로직
        // --------------------------------------------------------
        if (TacticalCommandManager.I == null) { MoveToEnemy(); return; }
        Transform rallyPoint = TacticalCommandManager.I.currentRallyPoint;
        if (rallyPoint == null) return;

        // 1. 농성(Siege) 우선 처리 (기존 유지)
        if (isSiege)
        {
            float distToRally = Vector3.Distance(transform.position, rallyPoint.position);
            if (distToRally < 20.0f)
            {
                if (TryEnterGarrison(rallyPoint.position, rallyPoint)) return; 
                MoveToHideInPoint(rallyPoint.position);
                return; 
            }
            else
            {
                MoveToRallyPoint(rallyPoint);
                return;
            }
        }

        // 2. [추가] 공격(Attack) 상태일 때 전역 추적 로직 추가
        // PlayerBot이 웨이브를 발동하여 상태를 Attack으로 바꾸면, 집결지를 무시하고 적을 찾아 진격합니다.
        if (TacticalCommandManager.I.currentState == TacticalState.Attack)
        {
            MoveToEnemy(); // 전역에서 가장 가까운 적을 찾아 이동
            return;
        }

        // 3. 방어(Defend) 상태일 때 감지 범위 내 교전 (기존 유지)
        if (HasEnemyInDetectRange()) 
        {
            GameObject target = FindEnemyInDetectRange();
            if (target != null) 
            {
                RotateTowards(target.transform.position);
                transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
            }
            return; 
        }

        // 4. 일반 이동 (진형 유지) - Defend 상태일 때 적용됨 (기존 유지)
        MoveToRallyPoint(rallyPoint);
    }

    // 🌟 [신규] 건물 진입 시도 함수
    // targetTransform이 있으면 BaseController를 찾아보고, 없으면 위치 기준으로 찾습니다.
    bool TryEnterGarrison(Vector3 targetPos, Transform targetTransform = null)
    {
        float dist = Vector3.Distance(transform.position, targetPos);
        
        // 거리가 0.5 이내면 진입
        if (dist <= 0.5f)
        {
            BaseController baseCtrl = null;

            if (targetTransform != null)
            {
                baseCtrl = targetTransform.GetComponent<BaseController>();
            }
            else
            {
                Collider2D col = Physics2D.OverlapPoint(targetPos);
                if (col != null) baseCtrl = col.GetComponent<BaseController>();
            }

            if (baseCtrl != null && baseCtrl.CompareTag(gameObject.tag))
            {
                baseCtrl.GarrisonUnit(this); // BaseController가 유닛 리스트에 넣고 SetActive(false) 함
                return true; 
            }
        }
        return false; 
    }

    // 🚩 [핵심 수정] 테두리 기준 배치 로직
    void MoveToRallyPoint(Transform target)
    {
        // 1. 목표 지점의 테두리(Edge) 찾기
        Vector3 edgePos = target.position;
        Collider2D targetCol = target.GetComponent<Collider2D>();

        // Player는 위(+Y)로, Enemy는 아래(-Y)로
        Vector3 forwardDir = (gameObject.CompareTag("Player")) ? Vector3.up : Vector3.down;

        if (targetCol != null)
        {
            // Player면 건물의 위쪽 끝(Max Y), Enemy면 건물의 아래쪽 끝(Min Y)
            float edgeY = (gameObject.CompareTag("Player")) ? targetCol.bounds.max.y : targetCol.bounds.min.y;
            edgePos = new Vector3(targetCol.bounds.center.x, edgeY, 0);
        }

        // 2. 테두리로부터 Defend Distance만큼 떨어지기 + 랜덤 X 분산
        // (단, 스팟이 비어있으면 그냥 중심에서 계산됨)
        Vector3 destPos = edgePos + (forwardDir * defendDistance);
        destPos.x += randomOffsetX; // 좌우 랜덤 배치

        // 3. 도착 판정 (이제 기지 중심이 아니라, 기지 앞마당 좌표와 비교함)
        float dist = Vector3.Distance(transform.position, destPos);
        
        if (dist <= 0.2f) 
        {
            StopMoving(); // 🌟 변경
            Quaternion lookRotation = (gameObject.CompareTag("Player")) ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
            transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            return; 
        }

        MoveToPosition(destPos); // 🌟 변경
    }

    // 🛠️ [보조] Vector3 위치로 숨는 함수 (기존 유지하되 도달 체크는 위에서 함)
    void MoveToHideInPoint(Vector3 targetPos)
    {
        RotateTowards(targetPos);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
    }

    // 🚩 [핵심] 기수병 최적 위치 이동 로직
    void MoveToBestBuffPosition()
    {
        aiThinkTimer += Time.deltaTime;

        // 0.5초마다 최적 위치 갱신 (성능 최적화)
        if (aiThinkTimer >= 0.5f)
        {
            aiThinkTimer = 0f;
            currentBestBuffPos = CalculateBestBuffPos();
        }

        // 계산된 위치로 이동
        float dist = Vector3.Distance(transform.position, currentBestBuffPos);
        if (dist > 0.5f)
        {
            RotateTowards(currentBestBuffPos);
            transform.position = Vector3.MoveTowards(transform.position, currentBestBuffPos, moveSpeed * Time.deltaTime);
        }
    }

    // 🚩 [핵심 수정] 기수병 최적 위치 이동 로직 (노동병 제외)
    Vector3 CalculateBestBuffPos()
    {
        // 1. 모든 아군 찾기
        GameObject[] allies = GameObject.FindGameObjectsWithTag(gameObject.tag);
        
        Vector3 bestPos = transform.position; // 기본값은 현재 위치
        float maxScore = -1f;

        // 2. 각 아군의 위치를 '후보지'로 가정하고 점수 매기기
        foreach (GameObject candidate in allies)
        {
            // 기지나 자기 자신 위치는 제외
            UnitController candidateUnit = candidate.GetComponent<UnitController>();
            if (candidateUnit == null) continue; 
            
            // 🚫 [수정] 노동병의 위치는 후보지로 고려하지 않음 (전투에 도움 안됨)
            if (candidateUnit.unitType == UnitType.Worker) continue;

            // 후보 위치 (약간의 랜덤 오차를 줘서 완벽하게 겹치지 않게 함)
            Vector3 testPos = candidate.transform.position;

            // 점수 계산
            float score = 0f;
            
            // 이 위치(testPos)에서 내 버프 범위(attackRange) 안에 들어오는 아군들의 가치 합산
            foreach (GameObject ally in allies)
            {
                if (ally.GetComponent<BaseController>() != null) continue; 

                float d = Vector3.Distance(testPos, ally.transform.position);
                if (d <= attackRange)
                {
                    UnitController u = ally.GetComponent<UnitController>();
                    if (u != null)
                    {
                        // 🚫 [수정] 주변에 노동병이 있어도 점수에 포함시키지 않음 (유인 효과 제거)
                        if (u.unitType == UnitType.Worker) continue;

                        score += GetUnitValue(u.unitType);
                    }
                }
            }

            // 최고 점수 갱신
            if (score > maxScore)
            {
                maxScore = score;
                bestPos = testPos;
            }
        }
        
        // 3. 만약 주변에 아무도 없다면(혹은 노동병만 있다면)? 기지 앞으로 이동
        if (maxScore <= 0)
        {
            GameObject myBase = GameObject.FindGameObjectWithTag(myBaseTag);
            if (myBase != null)
            {
                Vector3 forward = (myBaseTag == "Player") ? Vector3.up : Vector3.down;
                return myBase.transform.position + forward * 3.0f;
            }
        }

        return bestPos;
    }

    // 💰 유닛 가치 평가 함수
    float GetUnitValue(UnitType type)
    {
        switch (type)
        {
            case UnitType.Swordsman:
            case UnitType.Archer:
            case UnitType.Shielder:
            case UnitType.Cavalry:
            case UnitType.Mage:
            case UnitType.Assassin:
            case UnitType.Balloon:
                return 1.5f; // 전투 유닛 (비지원형)

            case UnitType.Worker:
            case UnitType.Healer:
            case UnitType.FlagBearer:
                return 0.5f; // 지원형 유닛

            default:
                return 1.0f;
        }
    }

    // ⚔️ [신규] 요격 판단 로직
    // 감지 범위 내에 적이 있으면 true 반환하고 적에게 이동
    bool CheckIntercept()
    {
        // 1. 감지 범위(detectRange) 내의 적 찾기
        GameObject nearbyEnemy = FindEnemyInDetectRange();

        // 2. 적이 있으면 -> 공격 모드(MoveToEnemy)로 전환
        if (nearbyEnemy != null)
        {
            // MoveToEnemy는 전맵에서 가장 가까운 적을 찾지만, 
            // 감지 범위 내에 적이 있다면 그 적이 가장 가까울 확률이 매우 높음
            MoveToEnemy(); 
            return true; // 요격 행동을 했음을 알림
        }

        return false; // 요격할 적 없음 -> 원래 위치로 이동
    }

    // 🔍 [수정] 타겟팅 로직 (디버그 로그 포함)
    void MoveToEnemy()
    {
        GameObject target = FindNearestTarget(enemyTag);
        if (target != null)
        {
            MoveToPosition(target.transform.position); // 🌟 변경
        }
        else
        {
            StopMoving();
        }
    }

    // 🛡️ [수정됨] 수비 로직: 기지 앞 거리 + 랜덤 X축
    void MoveToBase()
    {
        GameObject myBase = GameObject.FindGameObjectWithTag(myBaseTag);

        if (myBase != null)
        {
            // 1. 방향 및 기준점 설정
            Vector3 forwardDir = (myBaseTag == "Player") ? Vector3.up : Vector3.down;
            
            Vector3 baseEdgePos = myBase.transform.position;
            Collider2D baseCol = myBase.GetComponent<Collider2D>();

            // 2. 테두리(앞쪽) 찾기
            if (baseCol != null)
            {
                // Player(위가 전진): 기지의 윗변(Max Y) 기준
                // Enemy(아래가 전진): 기지의 아랫변(Min Y) 기준
                float yEdge = (myBaseTag == "Player") ? baseCol.bounds.max.y : baseCol.bounds.min.y;
                baseEdgePos = new Vector3(baseCol.bounds.center.x, yEdge, 0);
            }

            // 3. 목표 위치: [중심X + 랜덤X] , [테두리Y + (앞쪽 * 거리)]
            Vector3 targetPos = baseEdgePos + (forwardDir * defendDistance);
            targetPos.x += randomOffsetX; // 🎲 랜덤 오프셋 적용

            // 4. 이동
            float dist = Vector3.Distance(transform.position, targetPos);
            
            if (dist > 0.1f)
            {
                RotateTowards(targetPos);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }
            else
            {
                Quaternion lookRotation = (myBaseTag == "Player") ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
                transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }
        }
    }

    // 🏰 [수정됨] 농성 로직: 기지 내부(Collider 안)로 숨기
    void MoveToSiege()
    {
        GameObject myBase = GameObject.FindGameObjectWithTag(myBaseTag);
        if (myBase != null)
        {
            // 1. 기지의 정중앙 찾기
            Vector3 centerPos = myBase.transform.position;
            Collider2D baseCol = myBase.GetComponent<Collider2D>();

            if (baseCol != null)
            {
                centerPos = baseCol.bounds.center;
            }
            
            // 2. 약간의 랜덤 오프셋 (너무 한 점에 뭉치지 않게)
            Vector3 targetPos = centerPos + new Vector3(siegeRandomX, siegeRandomY, 0);

            // 3. 이동
            float dist = Vector3.Distance(transform.position, targetPos);
            
            // 도착 판정 거리를 매우 짧게 하여 안으로 쑥 들어가게 함
            if (dist > 0.2f)
            {
                RotateTowards(targetPos);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }
            else
            {
                // 도착 후 적을 바라보거나 정면 보기
                GameObject target = FindNearestTarget();
                if (target != null)
                {
                    RotateTowards(target.transform.position);
                }
                else
                {
                    Quaternion lookRotation = (myBaseTag == "Player") ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
                    transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
                }
            }
        }
    }

    // 🌟 [수정됨] 가장 가까운 적을 찾아 그쪽으로 회전하며 이동
    void Move()
    {
        // 1. 가장 가까운 적 찾기
        GameObject target = FindNearestTarget();

        // 2. 타겟이 있다면 그 방향으로 회전
        if (target != null)
        {
            Vector3 dir = target.transform.position - transform.position;
            // atan2를 이용해 각도 계산 (스프라이트가 위쪽(Up)을 보고 있다고 가정하여 -90도 보정)
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // 부드럽게 회전 (Lerp)
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // 3. 내 몸이 바라보는 방향(Up)으로 전진
        myTransform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
    }

    // 🚑 [신규] 아군 추적 이동 로직
    void MoveToAlly()
    {
        GameObject target = FindNearestAlly();
        if (target == null) { MoveToBase(); return; }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > attackRange * 0.8f)
        {
            MoveToPosition(target.transform.position); // 🌟 변경
        }
        else
        {
            StopMoving();
        }
    }

    // 🔍 가장 가까운 아군 기지 찾기
    BaseController FindNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var b in bases)
        {
            // 내 팀이고 + 건설 완료된 기지만
            if (b.CompareTag(gameObject.tag) && b.isConstructed)
            {
                float dst = Vector3.Distance(transform.position, b.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestBase = b;
                }
            }
        }
        return bestBase;
    }

    // 🚑 [수정] 아군 찾기 로직 강화 (나팔병은 나팔병/노동병/성채시체병 무시)
    GameObject FindNearestAlly()
    {
        GameObject[] allies = GameObject.FindGameObjectsWithTag(gameObject.tag);
        
        GameObject closestNonHealer = null; 
        float distNonHealer = Mathf.Infinity;

        GameObject closestHealer = null;    
        float distHealer = Mathf.Infinity;

        Vector3 currentPos = transform.position;

        foreach (GameObject ally in allies)
        {
            if (ally == gameObject) continue; 
            if (ally.GetComponent<BaseController>() != null) continue;

            UnitController allyUnit = ally.GetComponent<UnitController>();
            if (allyUnit == null) continue;

            // 🛑 고정형 유닛(건물 취급)은 따라가지 않음 (기본 로직)
            if (allyUnit.IsStaticUnit) continue;

            // 기존 노동병 수동 이동 체크 로직 유지
            if (allyUnit.unitType == UnitType.Worker && allyUnit.isManualMove) continue;

            // 🚫 [신규] 내가 나팔병(Trumpeter)이라면, 불필요한 대상을 따라가지 않음
            if (this.unitType == UnitType.Trumpeter)
            {
                if (allyUnit.unitType == UnitType.Trumpeter) continue;
                if (allyUnit.unitType == UnitType.Worker || allyUnit.unitType == UnitType.Slave) continue;
                if (allyUnit.unitType == UnitType.BaseCorpse) continue; // 🌟 추가됨
            }

            float distSqr = (ally.transform.position - currentPos).sqrMagnitude;

            if (allyUnit.unitType == UnitType.Healer)
            {
                if (distSqr < distHealer)
                {
                    distHealer = distSqr;
                    closestHealer = ally;
                }
            }
            else
            {
                if (distSqr < distNonHealer)
                {
                    distNonHealer = distSqr;
                    closestNonHealer = ally;
                }
            }
        }

        return closestNonHealer != null ? closestNonHealer : closestHealer;
    }

    // 🏥 [수정] 회복 함수 (모든 회복의 진입점)
    public void Heal(float amount, bool showText = true)
    {
        if (isDead) return;

        // 🛑 [핵심] 회복 불가 상태면 회복 차단
        if (isUnhealable)
        {
            if (showText && FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Unhealable!", Color.gray, 25);
            return;
        }

        if (currentHP >= maxHP) return;

        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;

        if (hpSlider != null) hpSlider.value = currentHP;
        UpdateHealthColor();

        if (showText && FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, $"+{Mathf.RoundToInt(amount)}", Color.green, 25);
    }

    // 🌟 [수정] 인자 없는 버전도 공중 유닛 필터링 적용
    GameObject FindNearestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        
        GameObject nearest = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (GameObject enemy in enemies)
        {
            UnitController targetUnit = enemy.GetComponent<UnitController>();
            if (targetUnit != null)
            {
                // 1. 은신 체크
                if (targetUnit.isStealthed) continue;
                // ✈️ [핵심] 근거리는 공중 유닛 무시
                if (!this.isRangedUnit && targetUnit.isFlyingUnit) continue;
            }

            float distSqr = (enemy.transform.position - currentPos).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = enemy;
            }
        }
        return nearest;
    }

    void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    GameObject FindNearestTarget(string targetTag)
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        
        // 1. 암살병: 원거리 우선
        if (unitType == UnitType.Assassin)
        {
            GameObject bestRanged = GetClosestUnit(targets, true);
            if (bestRanged != null) return bestRanged; // 원거리가 있으면 그거 쫓음
            
            // 원거리가 없으면? 그냥 가까운 적 쫓음 (이때 로그 출력)
            Debug.Log("암살병: 원거리 유닛을 못 찾아서 근거리로 타겟 변경");
        }

        // 2. 일반 유닛
        return GetClosestUnit(targets, false);
    }

    void AttemptAttack(GameObject target)
    {
        if (unitType == UnitType.FlagBearer) return; // 🛑 공격 불가
        if (unitType == UnitType.Shielder) return;

        UnitController targetUnit = target.GetComponent<UnitController>();
        if (!isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) return; 

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            bool abilityHandled = false;
            if (myAbility != null) abilityHandled = myAbility.OnAttack(target);

            if (!abilityHandled)
            {
                UnitController enemyUnit = target.GetComponent<UnitController>();
                if (enemyUnit != null) enemyUnit.TakeDamage(attackDamage, false);
                else
                {
                    BaseController enemyBase = target.GetComponent<BaseController>();
                    if (enemyBase != null) enemyBase.TakeDamage(attackDamage);
                }
            }
        }
    }

    // -----------------------------------------------------------
    // 👇 [수정 1] TakeDamage 함수에서 방어력 변수 교체
    // -----------------------------------------------------------
    public void TakeDamage(float rawDamage, bool isTrueDamage = false)
    {
        if (isDead) return;

        // 💤 수면 중이었다면? -> 즉시 기상!
        if (isSleeping)
        {
            CureSleep();
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Wake Up!", Color.white, 30);
        }

        lastDamageTime = Time.time;

        float finalDamage = rawDamage;
        if (myAbility != null) finalDamage = myAbility.OnTakeDamage(rawDamage, null);

        if (isPoisoned) finalDamage *= (1.0f + POISON_AMP_RATIO);

        // 🩸 [핵심 수정] 살육의 나팔 패널티: 받는 피해 5% 증가
        if (HasTrumpeterBuff && isSlaughterBuffActive)
        {
            finalDamage *= 1.05f; 
        }

        float totalDefense = defense; 
        if (!isTrueDamage && finalDamage > 0)
        {
            float damageMultiplier = 50f / (50f + totalDefense);
            finalDamage *= damageMultiplier;
        }

        // 🛡️ 보호막 흡수 로직
        if (currentShield > 0)
        {
            if (finalDamage <= currentShield)
            {
                currentShield -= finalDamage;
                finalDamage = 0;
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Absorb", Color.cyan, 20);
            }
            else
            {
                finalDamage -= currentShield;
                currentShield = 0;
                UpdateShieldVisual(false); // 깨짐 -> 사라짐 (유지 안 함)
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Break!", Color.blue, 25);
            }
        }

        if (finalDamage > 0)
        {
            currentHP -= finalDamage;
            
            if (FloatingTextManager.I != null)
            {
                Color textColor = isTrueDamage ? Color.red : Color.white;
                string textContent = $"-{Mathf.RoundToInt(finalDamage)}";
                FloatingTextManager.I.ShowText(transform.position, textContent, textColor, 25);
            }
        }

        if (hpSlider != null) hpSlider.value = currentHP;
        UpdateHealthColor();

        if (currentHP <= 0) Die();
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

    private void Die()
    {
        isDead = true;

        // 🌟 [신규] Ability에게 사망 처리를 위임할지 물어봄
        if (myAbility != null && myAbility.OnDie())
        {
            // Ability가 true를 반환했으므로, 즉시 파괴하지 않고 대기.
            // Ability가 연출 후 FinishDeath()를 호출해줄 것임.
            
            // 단, 체력바나 충돌체는 미리 꺼두는 게 깔끔함
            if (hpSlider != null) hpSlider.gameObject.SetActive(false);
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            
            return; 
        }

        // 일반적인 사망 처리
        FinishDeath();
    }

    // 🌟 [수정] 최종 사망 처리
    public void FinishDeath()
    {
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Dead", Color.gray, 20);

        // ❌ [삭제] 기존 코드: Destroy(gameObject);
        
        // ✅ [수정] 풀 매니저에게 반납 (Recycle)
        if (PoolManager.I != null)
        {
            PoolManager.I.Return(unitType, gameObject);
        }
        else
        {
            Destroy(gameObject); // 매니저가 없으면 그냥 삭제 (안전장치)
        }
    }

    // =========================================================
    // 🏃‍♂️ [신규] 수동 이동 공용 함수 (Necromancer, Skeleton 등 사용)
    // =========================================================
    public void MoveTo(Vector3 targetPos)
    {
        if (isDead) return;

        // 1. 이동
        // (둔화, 버프 등이 반영된 moveSpeed 사용)
        float step = moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        // 2. 회전 (목표 방향 바라보기)
        Vector3 dir = targetPos - transform.position;
        if (dir != Vector3.zero)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);
        }
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        // 🛑 [버그 수정] 이미 죽었거나 비활성화된 상태면 넉백 코루틴 실행 불가
        if (isDead || !gameObject.activeInHierarchy) return;

        StartCoroutine(KnockbackRoutine(direction, force));
    }

    GameObject GetClosestUnit(GameObject[] candidates, bool prioritizeRanged)
    {
        GameObject nearest = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (GameObject t in candidates)
        {
            if (t == gameObject) continue;

            UnitController targetUnit = t.GetComponent<UnitController>();
            
            if (targetUnit != null && targetUnit.isStealthed) continue;

            // 🛑 2. [신규] 근거리 유닛은 공중 유닛 공격 불가!
            if (!this.isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

            if (prioritizeRanged)
            {
                if (targetUnit == null || !targetUnit.isRangedUnit) continue;
            }

            float distSqr = (t.transform.position - currentPos).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = t;
            }
        }
        return nearest;
    }

    // 🌟 [신규] 외부(Ability)에서 스탯을 강제로 수정할 때 사용
    public void ApplyStatMultiplier(float hpMultiplier)
    {
        float ratio = currentHP / maxHP; // 현재 체력 비율 유지
        
        maxHP *= hpMultiplier;
        currentHP = maxHP * ratio; // 비율에 맞춰 현재 체력도 증가
        
        // UI 갱신 (늘어난 체력 반영)
        if (hpSlider != null) 
        {
            hpSlider.maxValue = maxHP; // 슬라이더 최대값 갱신 필요
            hpSlider.value = currentHP;
        }
    }

    // 👻 투명도 조절 함수 (AssassinAbility에서 호출)
    public void SetOpacity(float alpha)
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 direction, float distance)
    {
        float duration = 0.2f; 
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (direction.normalized * distance);

        while (elapsed < duration)
        {
            if (isDead) yield break;
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
    }


    public bool IsDemonic(UnitType type)
    {
        int id = (int)type;
        return id >= 100 && id < 200;
    }

    // 🏗️ [신규] 고정형 유닛(건물)인지 확인하는 프로퍼티
    // 이동 로직에서 이들을 한 번에 걸러내기 위함입니다.
    public bool IsStaticUnit 
    {
        get { return unitType == UnitType.BaseArcher || unitType == UnitType.BaseCorpse; }
    }

    // ==================================================================================
    // 🖱️ [신규] 유닛 호버 기능 구현 (Collider2D가 있어야 작동함 - 이미 있음)
    // ==================================================================================
    // 🌟 [추가] UnitInfoPanel에서 원본 데이터(이름, 아이콘 등)에 접근하기 위한 프로퍼티
    public UnitData LinkedData => _linkedData;

    // ==================================================================================
    // 🖱️ [수정] 유닛 호버 기능 구현 (실시간 데이터 표시로 변경)
    // ==================================================================================
    private void OnMouseEnter()
    {
        // 죽은 유닛이나 UI가 없는 상태면 무시
        if (isDead || UnitInfoPanel.I == null || _linkedData == null) return;

        // 🌟 [변경] 기존 ShowUnitInfo 대신 실시간 정보를 보여주는 ShowDynamicUnitInfo 호출
        UnitInfoPanel.I.ShowDynamicUnitInfo(this);
    }

    private void OnMouseExit()
    {
        // 정보창이 없다면 무시
        if (UnitInfoPanel.I == null) return;

        // 마우스가 나가면 정보창 숨기기
        UnitInfoPanel.I.HideInfo();
    }

    // 🌟 [신규] 통합 이동 함수 (모든 이동은 이 함수를 통함)
    public void MoveToPosition(Vector3 targetPos)
    {
        Vector2 targetDir = (targetPos - transform.position).normalized;
        
        // 🚀 [수정] 겹침 허용 -> 분리(Separation) 벡터 계산 안 함 (항상 0)
        Vector2 separation = Vector2.zero;
        
        // 만약 아주 약간의 부드러운 거리두기만 원한다면 아래 주석 해제 (지금은 완전 겹침 허용)
        // separation = CalculateSeparation(); 

        Vector2 finalDirection = (targetDir + (separation * separationWeight)).normalized;

        rb.linearVelocity = finalDirection * moveSpeed;
        RotateTowards(targetPos);
    }

    // 분리 계산 (혹시 나중에 필요할 수 있어 남겨두되 사용 안 함)
    private Vector2 CalculateSeparation()
    {
        return Vector2.zero; 
    }

    // 🛑 [신규] 정지 함수
    public void StopMoving()
    {
        if (!isForcedMoving && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}