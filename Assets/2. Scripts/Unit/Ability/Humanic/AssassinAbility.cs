using UnityEngine;

public class AssassinAbility : UnitAbility
{
    [Header("기본 암살자 설정")]
    public float baseStealthDuration = 4.0f;
    public float baseDamageMultiplier = 2.5f;
    public float stealthCooldown = 10.0f; 

    [Header("업그레이드: 암살 (Assassination)")]
    public string assassinationKey = "ASSASSINATION"; 
    public float upgradedStealthDuration = 6.0f; 
    public float upgradedDamageMultiplier = 3.0f; 

    [Header("상태 (Debug)")]
    public bool isAbilityActive = false; 
    private float abilityTimer = 0f;     
    private float cooldownTimer = 0f;    

    // ... (Initialize, OnUpdate 등 기존 로직 유지) ...
    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (isAbilityActive)
        {
            abilityTimer += Time.deltaTime;
            float currentDuration = GetCurrentStealthDuration();
            if (abilityTimer >= currentDuration) DeactivateStealth(); 
        }
        else
        {
            if (cooldownTimer <= 0) CheckAndTriggerStealth();
        }
    }
    
    // ... (GetHelper 함수들과 CheckAndTriggerStealth 유지) ...
    float GetCurrentStealthDuration()
    {
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(assassinationKey, owner.tag))
            return upgradedStealthDuration;
        return baseStealthDuration;
    }

    float GetCurrentDamageMultiplier()
    {
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(assassinationKey, owner.tag))
            return upgradedDamageMultiplier;
        return baseDamageMultiplier;
    }

    void CheckAndTriggerStealth()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.detectRange);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                ActivateStealth();
                return;
            }
        }
    }

    void ActivateStealth()
    {
        isAbilityActive = true;
        abilityTimer = 0f;
        owner.isStealthed = true;
        owner.SetOpacity(0.3f); 
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Hide!", Color.gray, 20);
    }

    void DeactivateStealth()
    {
        isAbilityActive = false;
        owner.isStealthed = false; 
        cooldownTimer = stealthCooldown; 
        owner.SetOpacity(1.0f); 
    }

    public override bool OnAttack(GameObject target)
    {
        if (isAbilityActive)
        {
            float originalDmg = owner.attackDamage;
            owner.attackDamage *= GetCurrentDamageMultiplier();

            // 🌟 [수정] 업그레이드 여부에 따라 텍스트 분기 처리
            bool isUpgraded = false;
            if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(assassinationKey, owner.tag))
            {
                isUpgraded = true;
            }

            if (FloatingTextManager.I != null)
            {
                if (isUpgraded)
                {
                    // 업그레이드 상태일 때
                    FloatingTextManager.I.ShowText(transform.position, "Assassination!", new Color(0.6f, 0f, 0.8f), 45); // 보라색, 크게
                }
                else
                {
                    // 일반 기습일 때
                    FloatingTextManager.I.ShowText(transform.position, "Ambush!", Color.red, 35); // 빨간색, 보통
                }
            }

            UnitController enemy = target.GetComponent<UnitController>();
            if (enemy != null) 
            {
                enemy.TakeDamage(owner.attackDamage, false);
                // 독 적용 (이미 위에서 isUpgraded 체크했으므로 재사용)
                if (isUpgraded) enemy.ApplyPoison();
            }
            else 
            {
                BaseController enemyBase = target.GetComponent<BaseController>();
                if (enemyBase != null) enemyBase.TakeDamage(owner.attackDamage);
            }

            owner.attackDamage = originalDmg;
            DeactivateStealth();

            return true; 
        }

        return false; 
    }
    
    void OnDrawGizmosSelected()
    {
        if (owner != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, owner.detectRange);
        }
    }
}