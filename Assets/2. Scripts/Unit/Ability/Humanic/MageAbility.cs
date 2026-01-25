using UnityEngine;
using System.Collections;

public class MageAbility : UnitAbility
{
    [Header("기본 능력: 광역 마법")]
    public float explosionRadius = 1.5f; 
    public GameObject explosionVFX;      

    [Header("신규 능력: 용암지옥 (Lava Hell)")]
    public string lavaUpgradeKey = "LAVA_HELL";
    public float lavaCooldown = 25.0f;
    public float lavaDuration = 3.0f;   
    public float lavaDamagePerTick = 5.0f; 
    public float lavaTickInterval = 0.5f;
    public GameObject lavaPrefab; 

    [Header("디버그 설정 (테스트용)")]
    public bool debugForceActive = false; // 🌟 체크하면 업그레이드 무시하고 발동

    [Header("상태 (Read Only)")]
    private float lavaTimer = 0f;
    public bool isCastingLava = false;

    public override bool IsBusy => isCastingLava;

    // 🌟 [추가] 드래그&드롭 테스트 시 owner 연결 보장
    void Start()
    {
        if (owner == null)
        {
            owner = GetComponent<UnitController>();
            // 테스트 편의를 위해 사거리가 0이면 강제 설정
            if (owner != null && owner.attackRange <= 0.1f)
            {
                owner.attackRange = 6.0f;
                Debug.LogWarning("⚠️ [테스트] 사거리가 0이라서 6.0으로 강제 설정했습니다.");
            }
        }
    }

    public override void OnUpdate()
    {
        if (lavaTimer > 0) lavaTimer -= Time.deltaTime;

        // 1. 업그레이드 상태 확인
        bool isUpgraded = false;
        
        if (debugForceActive)
        {
            isUpgraded = true; // 강제 활성화
        }
        else if (UpgradeManager.I != null && owner != null)
        {
            isUpgraded = UpgradeManager.I.IsAbilityActive(lavaUpgradeKey, owner.tag);
        }

        // 업그레이드가 없으면 여기서 리턴 (이게 로그가 안 뜨는 이유였습니다)
        if (!isUpgraded) 
        {
            // 디버깅을 위해 '업그레이드 안됨' 로그를 1번만 보고 싶다면 아래 주석 해제
            // Debug.Log($"[{name}] 업그레이드 미적용 상태 (Key: {lavaUpgradeKey})");
            return; 
        }

        // 2. 쿨타임 및 발동 조건
        if (lavaTimer <= 0)
        {
            // 여기까지 왔으면 업그레이드는 통과한 것임
            GameObject target = FindTargetForLava();
            
            if (target != null)
            {
                Debug.Log($"🔥 [성공] 타겟 발견 ({target.name}) -> 용암지옥 발동!");
                CastLavaHell(target.transform.position);
            }
            else
            {
                // 너무 자주 뜨면 시끄러우니 조건부 로그
                // Debug.Log($"[대기] 쿨타임은 됐으나 사거리({owner.attackRange}) 내 적 없음");
            }
        }
    }

    void CastLavaHell(Vector3 targetPos)
    {
        isCastingLava = true;
        lavaTimer = lavaCooldown; // 쿨타임 적용

        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Lava Hell!", new Color(1f, 0.4f, 0f), 30);

        if (lavaPrefab != null)
        {
            GameObject lava = Instantiate(lavaPrefab, targetPos, Quaternion.identity);
            
            LavaZoneController zone = lava.GetComponent<LavaZoneController>();
            if (zone == null) zone = lava.AddComponent<LavaZoneController>();

            // owner가 null일 경우 대비
            string eTag = owner != null ? owner.enemyTag : "Enemy";
            string bTag = owner != null ? owner.targetBaseTag : "Enemy";

            zone.Initialize(lavaDamagePerTick, lavaTickInterval, lavaDuration, eTag, bTag);
        }
        else
        {
            Debug.LogError("🔥 [오류] Lava Prefab이 연결되지 않았습니다! Inspector를 확인하세요.");
        }

        isCastingLava = false;
    }

    GameObject FindTargetForLava()
    {
        if (owner == null) return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, owner.attackRange);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController u = hit.GetComponent<UnitController>();
                if (u != null && u.isStealthed) continue;
                return hit.gameObject; 
            }
        }
        return null;
    }
    
    // ... (나머지 OnAttack 등 기존 코드 유지) ...
    public override bool OnAttack(GameObject target)
    {
        if (isCastingLava) return true;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(target.transform.position, explosionRadius);
        bool hitAny = false;

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController enemyUnit = hit.GetComponent<UnitController>();
                if (enemyUnit != null) enemyUnit.TakeDamage(owner.attackDamage, false);
                else {
                    BaseController enemyBase = hit.GetComponent<BaseController>();
                    if (enemyBase != null) enemyBase.TakeDamage(owner.attackDamage);
                }
                hitAny = true;
            }
        }

        if (hitAny && explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, target.transform.position, Quaternion.identity);
            Destroy(vfx, 1.0f);
        }

        return true; 
    }
}