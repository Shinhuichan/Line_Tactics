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
    Healer, 
    Mage, 
    Assassin, 
    BaseArcher, // 🏰 성채 장궁병 (타겟팅 제외 대상)
    Balloon, 
    FlagBearer, 
    Spearman, 
    Ballista, 
    None, 


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
    BaseCorpse = 112, // 🏰 성채 시체병 (타겟팅 제외 대상)
    Slave = 113
}

public class UnitController : MonoBehaviour
{
    // ... (기존 변수들 그대로 유지) ...
    public static List<UnitController> activeUnits = new List<UnitController>();

    [Header("현재 상태 (Read Only)")]
    [SerializeField] public UnitType unitType;
    [Header("기본 스탯 (Base)")]
    [SerializeField] private float baseMaxHP;
    [SerializeField] private float baseDefense;     
    [SerializeField] private float baseMoveSpeed;
    [SerializeField] private float baseAttackDamage;
    [SerializeField] private float baseAttackCooldown; 

    [Header("분대 정보")]
    public Squad assignedSquad; 

    [Header("버프 상태 (Buffs)")]
    private float trumpeterBuffVal = 0f;
    private float trumpeterBuffTimer = 0f;
    private bool isSlaughterBuffActive = false;

    public bool HasTrumpeterBuff => trumpeterBuffTimer > 0;
    private float tempAttackSpeedBuffVal = 0f;
    private float tempAttackSpeedBuffTimer = 0f;
    
    [Header("현재 스탯 (Calculated)")]
    public float maxHP;
    public float defense;
    public float moveSpeed;
    public float attackDamage;
    public float currentHP; 

    [Header("설정")]
    public float attackRange;
    public float detectRange = 6.0f;
    public float attackCooldown;

    [Header("버프 승수 (Multipliers)")]
    public float multiplierAttack = 1.0f;
    public float multiplierMoveSpeed = 1.0f;
    public float multiplierCooldown = 1.0f; 

    public bool isRangedUnit;
    public bool isFlyingUnit;
    public bool isStealthed = false;
    public bool isManualMove = false;
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

    public float defendDistance; 
    private float randomOffsetX;  
    private float siegeRandomX;   
    private float siegeRandomY;   

    private float aiThinkTimer = 0f;
    private Vector3 currentBestBuffPos;

    public const float BURN_DAMAGE_PER_SEC = 5.0f;
    public const float BURN_DURATION = 3.0f;
    public const float POISON_DAMAGE_PER_SEC = 1.0f;
    public const float POISON_AMP_RATIO = 0.05f; 
    public const float SHOCK_DAMAGE = 1.0f;
    public const float SHOCK_INTERVAL = 0.5f;

    [Header("보호막 (Shield)")]
    public float currentShield = 0f;
    private GameObject shieldInstance; 
    private GameObject racialShieldPrefab; 

    [Header("둔화 (Slow)")]
    public bool isSlowed = false; 
    private float slowTimer = 0f;
    private float currentSlowIntensity = 0f; 
    private const float SLOW_DURATION_FIXED = 3.0f; 

    public bool IsSlowed => isSlowed;

    [Header("상태 이상 (Debuffs)")]
    private float burnTimer = 0f;       
    private float currentBurnDps = 0f; 
    private float burnTickTimer = 0f;   
    private bool isBurning = false;

    public bool isPoisoned = false; 
    private float poisonTickTimer = 0f;

    public bool isShocked = false;
    private float shockTimer = 0f;      
    private float shockTickTimer = 0f;  

    [Header("상태 이상: 제어 불가 (CC기)")]
    public bool isStunned = false; 
    public bool isForcedMoving = false; 
    private float stunTimer = 0f;
    public bool isSleeping = false;
    public bool isPetrified = false;
    public bool isUnhealable = false;
    private float unhealableTimer = 0f;

    public bool IsCrowdControlled => isStunned || isForcedMoving || isSleeping || isPetrified;

    public bool IsBurning => isBurning;
    public bool IsPoisoned => isPoisoned;
    public bool IsShocked => isShocked; 
    

    [Header("종족 특성 (Race Traits)")]
    public UnitRace unitRace; 
    private float raceTraitTimer = 0f; 

    private const float DEMONIC_REGEN_INTERVAL = 5.0f;
    private const float DEMONIC_REGEN_AMOUNT = 5.0f;

    private float lastDamageTime = 0f;
    private const float OUT_OF_COMBAT_TIME = 5.0f; 
    private const float SHIELD_REGEN_RATE = 10.0f; 

    private UnitData _linkedData;

    [Header("물리 및 이동 설정")]
    private Rigidbody2D rb;
    private CircleCollider2D col;

    public bool isGhost { get; private set; }

    public float separationWeight = 0f; 
    public float separationRadius = 1.0f;
    
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

        col.isTrigger = true; 
    }

    public void SetGhostMode(bool enable)
    {
        isGhost = enable;
    }

    void OnEnable()
    {
        activeUnits.Add(this);
        if (hpSlider != null) hpSlider.gameObject.SetActive(true);
        UpdateHealthColor();
    }

    void OnDisable()
    {
        activeUnits.Remove(this);
        if (UpgradeManager.I != null)
            UpgradeManager.I.OnUpgradeCompleted -= OnUpgradeCompletedHandler;
    }

    private void OnUpgradeCompletedHandler(string teamTag)
    {
        if (gameObject.CompareTag(teamTag))
        {
            RecalculateStats();
        }
    }

    public void Initialize(UnitData data, string myTag)
    {
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

        multiplierAttack = 1.0f;
        multiplierMoveSpeed = 1.0f;
        multiplierCooldown = 1.0f;

        RecalculateStats();
        this.currentHP = this.maxHP;
        this.isDead = false;
        
        InitUI();
        if (myAbility != null) myAbility.Initialize(this);
    }

    public void SetStateToAttackMove(Vector3 target)
    {
        isAttackMoving = true;
        attackMoveTarget = target;
        isManualMove = false; 
    }

    public void RecalculateStats()
    {
        if (UpgradeManager.I == null)
        {
            maxHP = baseMaxHP;
            defense = baseDefense;
            
            float slowFactor = isSlowed ? (1.0f - currentSlowIntensity) : 1.0f;
            moveSpeed = (baseMoveSpeed * multiplierMoveSpeed) * slowFactor;

            float damageBuffMultiplier = 1.0f + trumpeterBuffVal;
            attackDamage = (baseAttackDamage * multiplierAttack) * damageBuffMultiplier;
            
            attackCooldown = baseAttackCooldown / multiplierCooldown;
            return;
        }

        string myTag = gameObject.tag; 

        float hpBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.MaxHP, myTag);
        float defBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.Defense, myTag);
        float spdBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.MoveSpeed, myTag);
        float atkBonus = UpgradeManager.I.GetStatBonus(unitType, StatType.AttackDamage, myTag);

        float skirmisherSpeedMult = 1.0f;
        float skirmisherAtkSpdMult = 1.0f;

        float giantGrowthMultiplier = 1.0f;

        if (unitType == UnitType.Skirmisher && UpgradeManager.I.IsAbilityActive("SKIRMISHER_FRENZY", myTag))
        {
            skirmisherSpeedMult = 1.25f;  
            skirmisherAtkSpdMult = 1.25f; 
        }

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

            transform.localScale = Vector3.one * giantGrowthMultiplier;

            GiantAbility giantAbility = GetComponent<GiantAbility>();
            if (giantAbility != null)
            {
                giantAbility.UpdateGiantStats(giantGrowthMultiplier);
            }
        }

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

        if (hpSlider != null) hpSlider.maxValue = maxHP;

        if (currentHP > maxHP) currentHP = maxHP;
    }

    public void SetMultipliers(float atkMult, float spdMult, float cdMult)
    {
        multiplierAttack = atkMult;
        multiplierMoveSpeed = spdMult;
        multiplierCooldown = cdMult;
        RecalculateStats(); 
    }

    void Update()
    {
        if (isDead) return;

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

        HandleRaceTraits();

        if (CheckAndProcessSiege()) return;

        if (myAbility != null)
        {
            myAbility.OnUpdate();
            if (myAbility.IsBusy) 
            {
                StopMoving();
                return;
            }
        }

        ProcessMainBehavior();
    }

    void ProcessMainBehavior()
    {
        if (unitType == UnitType.FlagBearer || unitType == UnitType.Trumpeter)
        {
            if (!isManualMove) 
            {
                if (unitType == UnitType.FlagBearer) MoveToBestBuffPosition();
                else MoveToAlly(); 
            }
            return;
        }

        bool isSiegeMode = false;
        if (gameObject.CompareTag("Player") && TacticalCommandManager.I != null)
             isSiegeMode = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        else if (gameObject.CompareTag("Enemy"))
             isSiegeMode = (EnemyBot.enemyState == TacticalState.Siege);

        if (unitType == UnitType.Healer)
        {
            if (!isManualMove) ProcessHealerMove();
            return;
        }

        bool canAttack = true;
        if (isSiegeMode && !isRangedUnit && !IsStaticUnit) canAttack = false;

        GameObject validTarget = null;
        
        if (canAttack) validTarget = FindBestTarget(); 

        if (validTarget != null)
        {
            RotateTowards(validTarget.transform.position);
            AttemptAttack(validTarget);
            StopMoving(); 
        }
        else if (isAttackMoving) 
        {
            MoveToPosition(attackMoveTarget);
            
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
            ProcessTacticalMove(); 
        }
    }

    // 🎯 [핵심 수정] 타겟 탐색 시 성채 유닛 무시 로직 적용
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
                    
                    // 1. 은신 체크
                    if (targetUnit != null && targetUnit.isStealthed) continue;

                    // 🛑 [신규] 성채 유닛(장궁병/시체병)은 타겟팅 절대 금지
                    if (targetUnit != null && targetUnit.IsStaticUnit) continue;

                    // 2. 근거리 유닛의 공중 공격 불가 체크
                    if (!isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

                    if (targetUnit != null)
                    {
                        if (targetUnit.isRangedUnit) rangedInReach = obj; 
                        else meleeInReach = obj; 
                    }
                    else meleeInReach = obj; // UnitController가 없는 건물(BaseController) 등
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
                    
                    // 1. 은신 유닛 무시
                    if (targetUnit != null && targetUnit.isStealthed) continue;

                    // 🛑 [신규] 성채 유닛(장궁병/시체병)은 타겟팅 절대 금지
                    if (targetUnit != null && targetUnit.IsStaticUnit) continue;

                    // 2. 근거리 유닛은 비행 유닛 완전 무시
                    if (!isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) 
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

    bool CheckAndProcessSiege()
    {
        if (IsStaticUnit) return false;

        bool isSiege = false;

        if (CompareTag("Player") && TacticalCommandManager.I != null)
        {
            isSiege = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        }
        else if (CompareTag("Enemy"))
        {
             isSiege = (EnemyBot.enemyState == TacticalState.Siege);
        }

        if (isSiege)
        {
            BaseController nearestBase = FindNearestBase();

            if (nearestBase != null)
            {
                if (TryEnterGarrison(nearestBase.transform.position, nearestBase.transform))
                {
                    return true;
                }
                MoveToHideInPoint(nearestBase.transform.position);
            }
            return true; 
        }

        return false;
    }

    void HandleRaceTraits()
    {
        switch (unitRace)
        {
            case UnitRace.Humanic:
                break;

            case UnitRace.Demonic:
                if (currentHP < maxHP)
                {
                    raceTraitTimer += Time.deltaTime;
                    if (raceTraitTimer >= DEMONIC_REGEN_INTERVAL)
                    {
                        raceTraitTimer = 0f;
                        Heal(DEMONIC_REGEN_AMOUNT, false); 
                    }
                }
                else
                {
                    raceTraitTimer = 0f; 
                }
                break;

            case UnitRace.Angelic:
                if (Time.time - lastDamageTime >= OUT_OF_COMBAT_TIME)
                {
                    float maxShield = maxHP * 0.2f; 

                    if (currentShield < maxShield)
                    {
                        float regen = SHIELD_REGEN_RATE * Time.deltaTime;
                        currentShield += regen;

                        if (currentShield > maxShield) currentShield = maxShield;

                        if (racialShieldPrefab != null)
                        {
                            UpdateShieldVisual(true, racialShieldPrefab);
                        }
                    }
                }
                break;
        }
    }

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

    public void ApplyUnhealable(float duration)
    {
        isUnhealable = true;
        unhealableTimer = duration;

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Rotting...", new Color(0.5f, 0f, 0.5f), 20);
    }

    public void ApplyTemporaryAttackSpeedBuff(float percent, float duration)
    {
        if (percent >= tempAttackSpeedBuffVal)
        {
            tempAttackSpeedBuffVal = percent;
            tempAttackSpeedBuffTimer = duration;
            RecalculateStats();
        }
    }

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

    public void ApplyTrumpeterBuff(float percent, float duration, bool isSlaughterMode = false)
    {
        trumpeterBuffVal = percent;
        trumpeterBuffTimer = duration;
        isSlaughterBuffActive = isSlaughterMode; 
        
        RecalculateStats();

        if (FloatingTextManager.I != null)
        {
            string msg = isSlaughterMode ? "Slaughter!" : "+DMG!";
            Color color = isSlaughterMode ? new Color(1f, 0.2f, 0.2f) : Color.red;
            FloatingTextManager.I.ShowText(transform.position + Vector3.up, msg, color, 20);
        }
    }

    void HandleTrumpeterBuff()
    {
        if (trumpeterBuffTimer > 0)
        {
            trumpeterBuffTimer -= Time.deltaTime;
            if (trumpeterBuffTimer <= 0)
            {
                trumpeterBuffTimer = 0f;
                trumpeterBuffVal = 0f;
                isSlaughterBuffActive = false; 
                RecalculateStats(); 
            }
        }
    }

    public void ApplyPetrify(float durationBeforeBreak = 1.5f)
    {
        if (isDead || isPetrified) return; 

        StartCoroutine(PetrifyRoutine(durationBeforeBreak));
    }

    private IEnumerator PetrifyRoutine(float duration)
    {
        isPetrified = true; 

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray; 
        }

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.speed = 0f; 
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; 
        }

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Stone...", Color.gray, 30);

        yield return new WaitForSeconds(duration);

        FinishDeath(); 
    }

    public void ApplySleep()
    {
        if (isDead) return;

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
    }

    public void ApplyStun(float duration)
    {
        if (isDead) return;
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

    public void CureStun()
    {
        if (!isStunned) return;

        isStunned = false;
        stunTimer = 0f;
    }

    void HandleStunStatus()
    {
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
            }
        }
    }

    public void ApplyKnockback(Vector3 pushDirection, float distance, float duration = 0.2f)
    {
        if (isDead) return;
        if (isMechanical) return;
        StartCoroutine(ForcedMoveRoutine(pushDirection.normalized, distance, duration));
    }

    public void ApplyPull(Vector3 pullSourcePos, float distance, float duration = 0.5f)
    {
        if (isDead) return;
        if (isMechanical) return;
        Vector3 pullDir = (pullSourcePos - transform.position).normalized;
        
        ApplyStun(duration); 
        
        StartCoroutine(ForcedMoveRoutine(pullDir, distance, duration));
    }

    void HandleSlowStatus()
    {
        if (isSlowed)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                CureSlow(); 
            }
        }
    }

    public void ApplySlow(float intensity)
    {
        if (isDead) return;

        if (!isSlowed)
        {
            isSlowed = true;
            currentSlowIntensity = intensity;
            slowTimer = SLOW_DURATION_FIXED;
            
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Slow!", Color.gray, 20);

            RecalculateStats(); 
        }
        else
        {
            if (intensity >= currentSlowIntensity)
            {
                currentSlowIntensity = intensity; 
                slowTimer = SLOW_DURATION_FIXED;  
                RecalculateStats(); 
            }
        }
    }

    private IEnumerator ForcedMoveRoutine(Vector3 direction, float distance, float duration)
    {
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
            
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        isForcedMoving = false;
    }

    public void CureSlow()
    {
        if (!isSlowed) return;

        isSlowed = false;
        currentSlowIntensity = 0f;
        slowTimer = 0f;
        
        RecalculateStats(); 
    }

    void HandleShockStatus()
    {
        if (isShocked)
        {
            shockTimer -= Time.deltaTime;
            shockTickTimer += Time.deltaTime;

            if (shockTickTimer >= SHOCK_INTERVAL)
            {
                shockTickTimer = 0f;
                TakeDamage(SHOCK_DAMAGE, true); 
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Zzzt!", Color.yellow, 20);
            }

            if (shockTimer <= 0)
            {
                isShocked = false;
                shockTickTimer = 0f;
            }
        }
    }

    public void ApplyShock(float duration)
    {
        if (isMechanical) return;

        isShocked = true;
        shockTimer = duration;
        shockTickTimer = 0f; 

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Shocked!", Color.yellow, 25);
    }

    public void CureShock()
    {
        if (isShocked)
        {
            isShocked = false;
            shockTimer = 0f;
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(transform.position, "Grounding!", Color.green, 20); 
        }
    }

    public void ApplyShield(float amount, GameObject visualPrefab)
    {
        currentShield = amount;
        UpdateShieldVisual(true, visualPrefab);

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "+Shield", Color.cyan, 25);
    }

    void UpdateShieldVisual(bool isActive, GameObject prefab = null)
    {
        if (isActive)
        {
            if (shieldInstance != null)
            {
                shieldInstance.SetActive(true);
            }
            else if (prefab != null)
            {
                shieldInstance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
                shieldInstance.name = "Shield_Effect";
                shieldInstance.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            if (shieldInstance != null) shieldInstance.SetActive(false);
        }
    }

    void ProcessHealerMove()
    {
        GameObject target = FindBestHealTarget();
        if (target == null) target = FindNearestAlly();
        if (target == null) { MoveToBase(); return; }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > attackRange * 0.8f)
        {
            MoveToPosition(target.transform.position); 
        }
        else
        {
            StopMoving();
        }
    }

    // 🚑 [핵심 수정] 치유병 타겟 검색 시에도 성채 유닛 무시
    GameObject FindBestHealTarget()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectRange);
        UnitController bestCandidate = null;
        float minHpRatio = 1.0f; 

        foreach (var col in colliders)
        {
            if (!col.CompareTag(this.tag)) continue;
            if (col.gameObject == gameObject) continue; 

            if (col.GetComponent<BaseController>() != null) continue;

            UnitController ally = col.GetComponent<UnitController>();
            
            // 🛑 [신규] 성채 유닛(장궁병/시체병)은 치유 대상에서 제외
            if (ally == null || ally.IsStaticUnit) continue; 

            if (ally.currentHP >= ally.maxHP) continue;

            float ratio = ally.currentHP / ally.maxHP;

            if (ratio < minHpRatio)
            {
                minHpRatio = ratio;
                bestCandidate = ally;
            }
        }

        return bestCandidate != null ? bestCandidate.gameObject : null;
    }

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

    void HandlePoisonStatus()
    {
        if (isPoisoned)
        {
            poisonTickTimer += Time.deltaTime;

            if (poisonTickTimer >= 1.0f)
            {
                poisonTickTimer = 0f;
                TakeDamage(POISON_DAMAGE_PER_SEC, true);

                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Poison", new Color(0.5f, 0f, 1f), 20); 
            }
        }
    }

    public void ApplyPoison()
    {
        if (isMechanical) return;

        if (!isPoisoned)
        {
            isPoisoned = true;
            poisonTickTimer = 0f; 
        }

        if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Poison", new Color(0.5f, 0f, 1f), 20); 
    }

    public void CurePoison()
    {
        isPoisoned = false;
    }

    void HandleBurnStatus()
    {
        if (burnTimer > 0)
        {
            burnTimer -= Time.deltaTime;
            burnTickTimer += Time.deltaTime;

            if (burnTickTimer >= 1.0f)
            {
                burnTickTimer = 0f;
                TakeDamage(currentBurnDps, true);
                
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Burn", new Color(1f, 0.5f, 0f), 20);
            }
        }
        else
        {
            isBurning = false;
        }
    }

    public void ApplyBurn()
    {
        isBurning = true;
        burnTimer = BURN_DURATION; 
        
        if (isMechanical)
        {
            currentBurnDps = BURN_DAMAGE_PER_SEC * 3.0f;
        }
        else
        {
            currentBurnDps = BURN_DAMAGE_PER_SEC; 
        }

        if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "Burn", new Color(1f, 0.5f, 0f), 20);
    }

    public void AddBonusDefense(float amount)
    {
        bonusDefenseBuff += amount;
        RecalculateStats(); 
    }

    public void RemoveBonusDefense(float amount)
    {
        bonusDefenseBuff = Mathf.Max(0, bonusDefenseBuff - amount);
        RecalculateStats();
    }

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

    // 🎯 [수정 1] 요격/감지 로직: 근거리 유닛은 공중 유닛을 감지조차 하지 않도록 수정
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
                
                // 1. 은신 유닛 무시
                if (targetUnit != null && targetUnit.isStealthed) continue;

                // 2. 성채 유닛 무시
                if (targetUnit != null && targetUnit.IsStaticUnit) continue;

                // 🛑 [신규] 근거리 유닛(Melee)은 공중 유닛(Flying)을 절대 감지하지 않음 (반응 X)
                if (!isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

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
    
    public bool HasEnemyInDetectRange()
    {
        return FindEnemyInDetectRange() != null;
    }

    void LateUpdate()
    {
        if (hpSlider != null)
        {
            hpSlider.transform.rotation = Quaternion.identity;
        }
    }

    void ProcessTacticalMove()
    {
        if (unitType == UnitType.FlagBearer) { MoveToBestBuffPosition(); return; }
        if (unitType == UnitType.Healer) { MoveToAlly(); return; }
        
        bool isSiege = false;
        
        if (gameObject.CompareTag("Player") && TacticalCommandManager.I != null)
             isSiege = (TacticalCommandManager.I.currentState == TacticalState.Siege);
        else if (gameObject.CompareTag("Enemy"))
             isSiege = (EnemyBot.enemyState == TacticalState.Siege);

        if (unitType == UnitType.Worker)
        {
            if (!isSiege) return; 
        }

        if (gameObject.CompareTag("Enemy"))
        {
            if (EnemyBot.enemyState == TacticalState.Attack) 
            {
                MoveToEnemy();
            }
            else if (EnemyBot.enemyState == TacticalState.Siege) 
            {
                float distToFront = Vector3.Distance(transform.position, EnemyBot.enemyFrontLinePos);
                
                if (distToFront < 20.0f)
                {
                    TryEnterGarrison(EnemyBot.enemyFrontLinePos); 
                }
                else
                {
                    MoveToBase(); 
                }
            }
            else 
            {
                if (CheckIntercept()) return; 
                MoveToBase(); 
            }
            return;
        }

        if (TacticalCommandManager.I == null) { MoveToEnemy(); return; }
        Transform rallyPoint = TacticalCommandManager.I.currentRallyPoint;
        if (rallyPoint == null) return;

        if (isSiege)
        {
            float distToRally = Vector3.Distance(transform.position, rallyPoint.position);
            
            if (distToRally < 20.0f)
            {
                if (TryEnterGarrison(rallyPoint.position, rallyPoint)) 
                {
                    return; 
                }
                
                MoveToHideInPoint(rallyPoint.position);
                return; 
            }
            else
            {
                MoveToRallyPoint(rallyPoint);
                return;
            }
        }

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

        MoveToRallyPoint(rallyPoint);
    }

    bool TryEnterGarrison(Vector3 targetPos, Transform targetTransform = null)
    {
        float dist = Vector3.Distance(transform.position, targetPos);
        
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
                baseCtrl.GarrisonUnit(this); 
                return true; 
            }
        }
        return false; 
    }

    void MoveToRallyPoint(Transform target)
    {
        Vector3 edgePos = target.position;
        Collider2D targetCol = target.GetComponent<Collider2D>();

        Vector3 forwardDir = (gameObject.CompareTag("Player")) ? Vector3.up : Vector3.down;

        if (targetCol != null)
        {
            float edgeY = (gameObject.CompareTag("Player")) ? targetCol.bounds.max.y : targetCol.bounds.min.y;
            edgePos = new Vector3(targetCol.bounds.center.x, edgeY, 0);
        }

        Vector3 destPos = edgePos + (forwardDir * defendDistance);
        destPos.x += randomOffsetX; 

        float dist = Vector3.Distance(transform.position, destPos);
        
        if (dist <= 0.2f) 
        {
            StopMoving(); 
            Quaternion lookRotation = (gameObject.CompareTag("Player")) ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
            transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            return; 
        }

        MoveToPosition(destPos); 
    }

    void MoveToHideInPoint(Vector3 targetPos)
    {
        RotateTowards(targetPos);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
    }

    void MoveToBestBuffPosition()
    {
        aiThinkTimer += Time.deltaTime;

        if (aiThinkTimer >= 0.5f)
        {
            aiThinkTimer = 0f;
            currentBestBuffPos = CalculateBestBuffPos();
        }

        float dist = Vector3.Distance(transform.position, currentBestBuffPos);
        if (dist > 0.5f)
        {
            RotateTowards(currentBestBuffPos);
            transform.position = Vector3.MoveTowards(transform.position, currentBestBuffPos, moveSpeed * Time.deltaTime);
        }
    }

    Vector3 CalculateBestBuffPos()
    {
        GameObject[] allies = GameObject.FindGameObjectsWithTag(gameObject.tag);
        
        Vector3 bestPos = transform.position; 
        float maxScore = -1f;

        foreach (GameObject candidate in allies)
        {
            UnitController candidateUnit = candidate.GetComponent<UnitController>();
            if (candidateUnit == null) continue; 

            Vector3 testPos = candidate.transform.position;

            float score = 0f;
            
            foreach (GameObject ally in allies)
            {
                if (ally.GetComponent<BaseController>() != null) continue; 

                float d = Vector3.Distance(testPos, ally.transform.position);
                if (d <= attackRange)
                {
                    UnitController u = ally.GetComponent<UnitController>();
                    if (u != null)
                    {
                        score += GetUnitValue(u.unitType);
                    }
                }
            }

            if (score > maxScore)
            {
                maxScore = score;
                bestPos = testPos;
            }
        }
        
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
                return 1.5f; 

            case UnitType.Worker:
            case UnitType.Healer:
            case UnitType.FlagBearer:
                return 0.5f; 

            default:
                return 1.0f;
        }
    }

    bool CheckIntercept()
    {
        GameObject nearbyEnemy = FindEnemyInDetectRange();

        if (nearbyEnemy != null)
        {
            MoveToEnemy(); 
            return true; 
        }

        return false; 
    }

    void MoveToEnemy()
    {
        GameObject target = FindNearestTarget(enemyTag);
        if (target != null)
        {
            MoveToPosition(target.transform.position); 
        }
        else
        {
            StopMoving();
        }
    }

    void MoveToBase()
    {
        GameObject myBase = GameObject.FindGameObjectWithTag(myBaseTag);

        if (myBase != null)
        {
            Vector3 forwardDir = (myBaseTag == "Player") ? Vector3.up : Vector3.down;
            
            Vector3 baseEdgePos = myBase.transform.position;
            Collider2D baseCol = myBase.GetComponent<Collider2D>();

            if (baseCol != null)
            {
                float yEdge = (myBaseTag == "Player") ? baseCol.bounds.max.y : baseCol.bounds.min.y;
                baseEdgePos = new Vector3(baseCol.bounds.center.x, yEdge, 0);
            }

            Vector3 targetPos = baseEdgePos + (forwardDir * defendDistance);
            targetPos.x += randomOffsetX; 

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

    void MoveToSiege()
    {
        GameObject myBase = GameObject.FindGameObjectWithTag(myBaseTag);
        if (myBase != null)
        {
            Vector3 centerPos = myBase.transform.position;
            Collider2D baseCol = myBase.GetComponent<Collider2D>();

            if (baseCol != null)
            {
                centerPos = baseCol.bounds.center;
            }
            
            Vector3 targetPos = centerPos + new Vector3(siegeRandomX, siegeRandomY, 0);

            float dist = Vector3.Distance(transform.position, targetPos);
            
            if (dist > 0.2f)
            {
                RotateTowards(targetPos);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }
            else
            {
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

    void Move()
    {
        GameObject target = FindNearestTarget();

        if (target != null)
        {
            Vector3 dir = target.transform.position - transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        myTransform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
    }

    void MoveToAlly()
    {
        GameObject target = FindNearestAlly();
        if (target == null) { MoveToBase(); return; }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > attackRange * 0.8f)
        {
            MoveToPosition(target.transform.position); 
        }
        else
        {
            StopMoving();
        }
    }

    BaseController FindNearestBase()
    {
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        BaseController bestBase = null;
        float minDst = Mathf.Infinity;

        foreach (var b in bases)
        {
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

            if (allyUnit.IsStaticUnit) continue;

            if (allyUnit.unitType == UnitType.Worker && allyUnit.isManualMove) continue;

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

    public void Heal(float amount, bool showText = true)
    {
        if (isDead) return;

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

    // 🎯 [수정 2] 단순 거리 기반 탐색: 여기서도 공중 유닛이 걸리면 쳐다보거나 쫓아갈 수 있으므로 필터링 추가
    GameObject FindNearestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        
        GameObject nearest = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (GameObject enemy in enemies)
        {
            UnitController targetUnit = enemy.GetComponent<UnitController>();

            // 1. 은신 체크 (기존에 없었다면 추가하여 안전성 확보)
            if (targetUnit != null && targetUnit.isStealthed) continue;

            // 2. 성채 유닛 무시
            if (targetUnit != null && targetUnit.IsStaticUnit) continue;

            // 🛑 [신규] 근거리 유닛은 공중 유닛을 탐색 대상에서 완전 제외
            if (!isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

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
        
        if (unitType == UnitType.Assassin)
        {
            GameObject bestRanged = GetClosestUnit(targets, true);
            if (bestRanged != null) return bestRanged; 
            
            Debug.Log("암살병: 원거리 유닛을 못 찾아서 근거리로 타겟 변경");
        }

        return GetClosestUnit(targets, false);
    }

    void AttemptAttack(GameObject target)
    {
        if (unitType == UnitType.FlagBearer) return; 
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

    public void TakeDamage(float rawDamage, bool isTrueDamage = false)
    {
        if (isDead) return;

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
                UpdateShieldVisual(false); 
                
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

        if (myAbility != null && myAbility.OnDie())
        {
            if (hpSlider != null) hpSlider.gameObject.SetActive(false);
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            
            return; 
        }

        FinishDeath();
    }

    public void FinishDeath()
    {
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Dead", Color.gray, 20);

        if (PoolManager.I != null)
        {
            PoolManager.I.Return(unitType, gameObject);
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    public void MoveTo(Vector3 targetPos)
    {
        if (isDead) return;

        float step = moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        Vector3 dir = targetPos - transform.position;
        if (dir != Vector3.zero)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.AngleAxis(angle, Vector3.forward), Time.deltaTime * 10f);
        }
    }

    // 🎯 [핵심 수정] 글로벌 타겟 탐색 시에도 성채 유닛 무시 (추격 금지)
    GameObject GetClosestUnit(GameObject[] candidates, bool prioritizeRanged)
    {
        GameObject nearest = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (GameObject t in candidates)
        {
            if (t == gameObject) continue;

            UnitController targetUnit = t.GetComponent<UnitController>();
            
            // 1. 은신 감지 불가
            if (targetUnit != null && targetUnit.isStealthed) continue;

            // 🛑 [신규] 성채 유닛 무시 (추격/공격 대상에서 제외)
            if (targetUnit != null && targetUnit.IsStaticUnit) continue;

            // 2. 근거리 유닛은 공중 유닛 공격 불가!
            if (!this.isRangedUnit && targetUnit != null && targetUnit.isFlyingUnit) continue;

            // 3. 암살병 우선순위 처리
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

    public void ApplyStatMultiplier(float hpMultiplier)
    {
        float ratio = currentHP / maxHP; 
        
        maxHP *= hpMultiplier;
        currentHP = maxHP * ratio; 
        
        if (hpSlider != null) 
        {
            hpSlider.maxValue = maxHP; 
            hpSlider.value = currentHP;
        }
    }

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

    public bool IsStaticUnit 
    {
        get { return unitType == UnitType.BaseArcher || unitType == UnitType.BaseCorpse; }
    }

    private void OnMouseEnter()
    {
        if (isDead || UnitInfoPanel.I == null || _linkedData == null) return;
        UnitInfoPanel.I.ShowUnitInfo(_linkedData);
    }

    private void OnMouseExit()
    {
        if (UnitInfoPanel.I == null) return;
        UnitInfoPanel.I.HideInfo();
    }

    public void MoveToPosition(Vector3 targetPos)
    {
        Vector2 targetDir = (targetPos - transform.position).normalized;
        Vector2 separation = Vector2.zero;
        Vector2 finalDirection = (targetDir + (separation * separationWeight)).normalized;

        rb.linearVelocity = finalDirection * moveSpeed;
        RotateTowards(targetPos);
    }

    private Vector2 CalculateSeparation()
    {
        return Vector2.zero; 
    }

    public void StopMoving()
    {
        if (!isForcedMoving && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}