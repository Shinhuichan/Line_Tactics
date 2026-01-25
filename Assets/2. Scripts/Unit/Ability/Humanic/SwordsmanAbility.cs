using UnityEngine;
using System.Collections;

public class SwordsmanAbility : UnitAbility
{
    [Header("기존 능력: 약점 포착")]
    [Tooltip("방어력을 무시하는 공격을 합니다.")]
    public bool ignoreDefense = true;

    [Header("신규 능력: 격노 (Fury)")]
    public string furyUpgradeKey = "FURY"; // 업그레이드 키
    public float furyThresholdRatio = 0.25f; // 체력 25% 이상일 때 발동
    public float furyHPDrain = 5.0f; // 초당 체력 소모
    
    [Header("격노 효과 (배율)")]
    public float damageMultiplier = 1.25f;
    public float speedMultiplier = 1.25f;
    public float cooldownMultiplier = 1.25f; // 공격 속도 1.25배 (쿨타임 감소)

    [Header("상태 (Read Only)")]
    public bool isFuryActive = false;

    private Coroutine furyVisualCoroutine;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    public override void OnUpdate()
    {
        // 1. 업그레이드 해금 여부 확인
        // 🌟 [수정] owner.tag 전달
        if (UpgradeManager.I == null || !UpgradeManager.I.IsAbilityActive(furyUpgradeKey, owner.tag)) return;

        // 2. 조건 확인
        float hpRatio = owner.currentHP / owner.maxHP;
        bool hasEnemy = owner.HasEnemyInDetectRange(); // UnitController에 추가한 함수 사용

        // [발동 조건]: 체력 25% 초과 AND 적 발견 AND 현재 비활성
        if (!isFuryActive && hpRatio > furyThresholdRatio && hasEnemy)
        {
            ActivateFury();
        }
        // [해제 조건]: 체력 25% 이하 OR 적 없음 AND 현재 활성
        else if (isFuryActive && (hpRatio <= furyThresholdRatio || !hasEnemy))
        {
            DeactivateFury();
        }

        // 3. 활성화 중 효과 처리 (체력 소모)
        if (isFuryActive)
        {
            // 방어 무시(True Damage)로 체력 깎음
            owner.TakeDamage(furyHPDrain * Time.deltaTime, true);
        }
    }

    void ActivateFury()
    {
        isFuryActive = true;
        
        // 스탯 뻥튀기 적용
        owner.SetMultipliers(damageMultiplier, speedMultiplier, cooldownMultiplier);

        // 텍스트 연출
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(transform.position, "FURY!", Color.red, 40);

        // 비주얼 효과 시작
        if (furyVisualCoroutine != null) StopCoroutine(furyVisualCoroutine);
        furyVisualCoroutine = StartCoroutine(FuryVisualRoutine());
    }

    void DeactivateFury()
    {
        isFuryActive = false;

        // 스탯 원상복구
        owner.SetMultipliers(1.0f, 1.0f, 1.0f);

        // 비주얼 효과 종료
        if (furyVisualCoroutine != null) StopCoroutine(furyVisualCoroutine);
        if (spriteRenderer != null) spriteRenderer.color = originalColor; // 색상 복구
    }

    // 🔥 이글거리는 효과 (색상 진동)
    IEnumerator FuryVisualRoutine()
    {
        if (spriteRenderer == null) yield break;

        // 붉은색 계열로 빠르게 깜빡임
        Color furyColor = new Color(1f, 0.4f, 0.4f); // 밝은 빨강
        float speed = 10f; // 깜빡임 속도

        while (true)
        {
            float t = Mathf.PingPong(Time.time * speed, 1f);
            // 원래 색과 격노 색 사이를 왔다갔다
            spriteRenderer.color = Color.Lerp(originalColor, furyColor, t);
            yield return null;
        }
    }

    // 공격 로직 (기존 유지)
    public override bool OnAttack(GameObject target)
    {
        UnitController enemyUnit = target.GetComponent<UnitController>();
        if (enemyUnit != null)
        {
            enemyUnit.TakeDamage(owner.attackDamage, ignoreDefense);
            return true;
        }
        return false; 
    }

    void OnDisable()
    {
        // 유닛이 죽거나 비활성화되면 격노 상태 해제
        if (isFuryActive) DeactivateFury();
    }
}