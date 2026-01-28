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
    
    // 💥 [신규] 넉백 거리 설정 추가 (기획된 넉백 기능을 위해 변수화)
    [Tooltip("적을 밀어내는 거리")]
    public float knockbackDistance = 2.5f;

    [Header("이펙트 (선택)")]
    public GameObject smashEffect; // 땅 찍을 때 이펙트

    // 내부 상태
    private bool isAttacking = false;
    private float baseSmashLength; // 📏 원본 사거리 저장용

    // 공격 중에는 이동 등 다른 행동 불가
    public override bool IsBusy => isAttacking;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);

        // 🛡️ [안전 장치] 풀링 사용 시 smashLength가 이미 늘어난 상태일 수 있으므로,
        // 최초 1회만 base값을 저장하거나, 로직에 따라 매번 초기화할 필요가 있음.
        // 여기서는 baseSmashLength가 0일 때(최초 실행)만 저장하도록 함.
        if (baseSmashLength == 0f)
        {
            baseSmashLength = smashLength;
        }
        else
        {
            // 재활용된 유닛이라면 smashLength를 원본으로 복구하고 시작
            smashLength = baseSmashLength;
        }

        // 사거리 안전장치 (기존 코드 유지)
        if (owner.attackRange > smashLength)
        {
            owner.attackRange = smashLength * 0.9f;
        }
    }

    // 📏 거대화 비율 적용 함수 (UnitController에서 호출)
    public void UpdateGiantStats(float multiplier)
    {
        // 🛡️ [버그 수정] owner가 Null인 경우(실행 순서 꼬임) 방어 코드
        if (owner == null)
        {
            // UnitController.Initialize 순서를 수정했으므로 여기 올 일은 없어야 하지만,
            // 만약 발생한다면 자신에게서 컴포넌트를 찾습니다.
            owner = GetComponent<UnitController>();
        }

        // baseSmashLength가 아직 세팅 안됐다면(순서 문제 등) 현재 값을 기준으로 잡음
        if (baseSmashLength == 0f) baseSmashLength = smashLength;

        // 공격 범위(이펙트 길이) 늘리기
        smashLength = baseSmashLength * multiplier;
        
        // 공격 사거리도 같이 늘려줌
        if (owner != null)
        {
            owner.attackRange = smashLength * 0.9f; 
        }
    }

    public override bool OnAttack(GameObject target)
    {
        if (isAttacking) return true;
        
        // 공격 시작 시점의 방향을 고정하기 위해 코루틴 진입
        StartCoroutine(SmashAttackRoutine());
        return true; 
    }

    IEnumerator SmashAttackRoutine()
    {
        isAttacking = true;

        // 1. 내려찍기 전 딜레이 (Wind Up)
        // UnitController는 IsBusy가 true인 동안 이동과 회전을 멈추므로,
        // 이 시점의 바라보는 방향(transform.up)이 공격 방향으로 고정됩니다.
        yield return new WaitForSeconds(windUpTime);

        // 2. 공격 판정 실행
        PerformSmash();

        // 3. 후딜레이 (필요하다면 추가)
        isAttacking = false;
    }

    void PerformSmash()
    {
        // 🔧 [수정] 방향 계산 로직 개선
        // UnitController.ProcessMainBehavior에서 이미 적을 향해 RotateTowards를 수행한 후 공격합니다.
        // 따라서 복잡한 조건문 없이 현재 유닛의 정면(transform.up)을 공격 방향으로 사용하면 됩니다.
        Vector3 direction = transform.up;

        Vector3 centerPos = transform.position + (direction * (smashLength * 0.5f));
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        // 💥 범위 타격 (BoxOverlap)
        Collider2D[] hits = Physics2D.OverlapBoxAll(centerPos, new Vector2(smashWidth, smashLength), angle);
        bool hitAnything = false;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // 적군이거나 적 기지인 경우
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController enemyUnit = hit.GetComponent<UnitController>();
                if (enemyUnit != null)
                {
                    // 1. 데미지
                    enemyUnit.TakeDamage(owner.attackDamage, false);
                    
                    // 2. 상태 이상: 기절
                    enemyUnit.ApplyStun(stunDuration);

                    // 3. 💥 [신규] 상태 이상: 넉백 (누락된 기능 구현)
                    // 공격 방향(direction)으로 밀어냅니다.
                    enemyUnit.ApplyKnockback(direction, knockbackDistance);

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
            // 이펙트 생성
            Instantiate(smashEffect, transform.position + (direction * 1.0f), Quaternion.Euler(0, 0, angle));
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && owner != null)
        {
            // 플레이 중에는 실제 바라보는 방향 사용
            Vector3 direction = transform.up;
            Gizmos.color = Color.red;
            Vector3 center = transform.position + (direction * (smashLength * 0.5f));
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            
            // 회전된 큐브를 그리기 위해 매트릭스 설정
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(smashWidth, smashLength, 1));
            Gizmos.matrix = Matrix4x4.identity; // 복구
        }
    }
}