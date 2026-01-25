using UnityEngine;
using System.Collections;

public class BalloonAbility : UnitAbility
{
    [Header("열기구 설정")]
    public float explosionRadius = 1.5f; 
    public GameObject explosionEffectPrefab; 

    [Header("신규 능력: 추락 폭격 (Crash Landing)")]
    public string crashUpgradeKey = "CRASH_LANDING";
    public float hpBonusMultiplier = 1.2f; // 체력 20% 증가
    public float crashDuration = 0.5f;     // 추락 연출 시간

    private bool isCrashUpgradeActive = false;
    private bool hasAppliedStatBonus = false;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 1. 업그레이드 확인 및 스탯 적용 (한 번만)
        if (!hasAppliedStatBonus && UpgradeManager.I != null)
        {
            if (UpgradeManager.I.IsAbilityActive(crashUpgradeKey, owner.tag))
            {
                isCrashUpgradeActive = true;
                hasAppliedStatBonus = true;
                
                // 체력 20% 뻥튀기 적용
                owner.ApplyStatMultiplier(hpBonusMultiplier);
                
                // (선택) 체력 증가 텍스트
                if (FloatingTextManager.I != null)
                    FloatingTextManager.I.ShowText(transform.position, "HP UP!", Color.green, 30);
            }
        }
    }

    public override bool OnAttack(GameObject target)
    {
        Explode(target.transform.position);
        return true; 
    }

    // 🌟 [핵심] 사망 시 호출됨
    public override bool OnDie()
    {
        // 업그레이드가 되어 있다면 추락 연출 시작!
        if (isCrashUpgradeActive)
        {
            StartCoroutine(CrashRoutine());
            return true; // UnitController야, 아직 삭제하지 마라!
        }

        return false; // 일반 사망
    }

    // ✈️ 추락 연출 코루틴
    IEnumerator CrashRoutine()
    {
        float timer = 0f;
        Vector3 initialScale = transform.localScale;
        Quaternion initialRot = transform.rotation;

        // 추락 시작 텍스트
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "Mayday!", Color.red, 35);

        while (timer < crashDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / crashDuration;

            // 1. 빙글빙글 회전 (1초에 720도 회전)
            transform.Rotate(Vector3.forward * 720f * Time.deltaTime);

            // 2. 작아짐 (0.3배까지 축소 -> 멀어지는 느낌)
            transform.localScale = Vector3.Lerp(initialScale, initialScale * 0.3f, progress);

            yield return null;
        }

        // 💥 쾅! 자폭 데미지 (현재 위치 기준)
        Explode(transform.position);

        // 연출 종료 후 진짜 사망 처리
        owner.FinishDeath();
    }

    void Explode(Vector3 center)
    {
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(center, "Bomb!", Color.red, 35);

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, explosionRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;

            if (col.CompareTag(owner.enemyTag) || col.CompareTag(owner.targetBaseTag))
            {
                UnitController enemyUnit = col.GetComponent<UnitController>();
                if (enemyUnit != null)
                {
                    enemyUnit.TakeDamage(owner.attackDamage);
                }
                else
                {
                    BaseController enemyBase = col.GetComponent<BaseController>();
                    if (enemyBase != null) enemyBase.TakeDamage(owner.attackDamage);
                }
            }
        }

        if (explosionEffectPrefab != null)
        {
            GameObject vfxInstance = Instantiate(explosionEffectPrefab, center, Quaternion.identity);
            Destroy(vfxInstance, 1.0f);
        }
    }
}