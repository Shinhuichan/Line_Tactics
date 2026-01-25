using UnityEngine;
using System.Collections;

public class GiantAbility : UnitAbility
{
    [Header("거인병 공격 설정")]
    [Tooltip("공격 준비 시간 (내려찍기 전 딜레이)")]
    public float windUpTime = 0.5f;

    [Tooltip("공격 범위 너비 (좌우 폭)")]
    public float smashWidth = 0.5f;

    [Tooltip("공격 범위 길이 (최대 사거리)")]
    public float smashLength = 1.5f; 

    [Header("상태 이상")]
    public float stunDuration = 1.0f; // 기절 시간

    [Header("이펙트 (선택)")]
    public GameObject smashEffect; // 땅 찍을 때 이펙트

    // 내부 상태
    private bool isAttacking = false;
    private float baseSmashLength; // 📏 [신규] 원본 사거리 저장용

    // 공격 중에는 이동 등 다른 행동 불가
    public override bool IsBusy => isAttacking;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
        // 초기 설정값 저장 (업그레이드 기준점)
        baseSmashLength = smashLength;
    }

    // 📏 [신규] 거대화 비율 적용 함수 (UnitController에서 호출)
    public void UpdateGiantStats(float multiplier)
    {
        // 공격 범위(이펙트 길이)도 같이 늘어남
        smashLength = baseSmashLength * multiplier;
    }

    public override bool OnAttack(GameObject target)
    {
        if (isAttacking) return true;
        StartCoroutine(SmashAttackRoutine());
        return true; 
    }

    IEnumerator SmashAttackRoutine()
    {
        isAttacking = true;

        // 1. 내려찍기 전 딜레이 (Wind Up)
        yield return new WaitForSeconds(windUpTime);

        // 2. 공격 판정 실행
        PerformSmash();

        // 3. 후딜레이 (필요하다면 추가, 현재는 없음)
        isAttacking = false;
    }

    void PerformSmash()
    {
        // 전방(Enemy인 경우 아래, Player인 경우 위) 계산
        Vector3 direction = (owner.tag == "Enemy") ? Vector3.down : Vector3.up;
        
        // 회전이 되어있다면 transform.up 사용
        if (transform.rotation.z != 0) direction = transform.up;

        Vector3 centerPos = transform.position + (direction * (smashLength * 0.5f));
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        // 💥 범위 타격 (BoxOverlap)
        Collider2D[] hits = Physics2D.OverlapBoxAll(centerPos, new Vector2(smashWidth, smashLength), angle);
        bool hitAnything = false;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController enemyUnit = hit.GetComponent<UnitController>();
                if (enemyUnit != null)
                {
                    enemyUnit.TakeDamage(owner.attackDamage, false);
                    enemyUnit.ApplyStun(stunDuration);
                    hitAnything = true;
                }
                else if (hit.GetComponent<BaseController>() != null)
                {
                    hit.GetComponent<BaseController>().TakeDamage(owner.attackDamage);
                    hitAnything = true;
                }
            }
        }

        // 피드백
        if (hitAnything && FloatingTextManager.I != null)
        {
            FloatingTextManager.I.ShowText(centerPos, "SMASH!", Color.red, 40);
        }

        if (smashEffect != null)
        {
            // 이펙트도 크기에 맞춰 조금 키워주면 좋음 (선택사항)
            GameObject vfx = Instantiate(smashEffect, transform.position + (direction * 1.0f), Quaternion.Euler(0, 0, angle));
            // vfx.transform.localScale *= (smashLength / baseSmashLength); // 필요 시 주석 해제
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && owner != null)
        {
            // 플레이 중에는 실제 계산된 smashLength 사용
            Vector3 direction = (owner.tag == "Enemy") ? Vector3.down : Vector3.up;
            if (transform.rotation.z != 0) direction = transform.up;

            Gizmos.color = Color.red;
            Vector3 center = transform.position + (direction * (smashLength * 0.5f));
            // 회전된 박스 그리기 (간략화)
            Gizmos.DrawWireCube(center, new Vector3(smashWidth, smashLength, 1));
        }
    }
}