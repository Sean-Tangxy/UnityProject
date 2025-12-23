using UnityEngine;
using System.Collections;

public class WeaponObject : MonoBehaviour
{
    [Header("武器数据")]
    public WeaponData weaponData;

    [Header("组件")]
    private SpriteRenderer spriteRenderer;
    private Collider2D weaponCollider;
    private AudioSource audioSource;

    [Header("状态")]
    private bool isAttacking;
    private bool canDamage;
    private Transform handlePoint;

    // 绕挂载点旋转/位移
    private Transform pivot;

    // ✅ 当前攻击伤害倍率（挥砍=1，突刺=thrustDamageMultiplier）
    private float currentDamageMultiplier = 1f;

    [Header("突刺参数（独立于挥砍）")]
    [Tooltip("突刺距离（世界单位）。如果勾选 useWeaponRangeAsBase，则这里是倍率。")]
    public float thrustDistance = 1.2f;

    [Tooltip("突刺前刺时间（秒）。越小越快。")]
    public float thrustForwardTime = 0.08f;

    [Tooltip("突刺回收时间（秒）。越小越快。")]
    public float thrustReturnTime = 0.06f;

    [Tooltip("突刺伤害倍率：1=等于基础伤害，1.5=更高。")]
    public float thrustDamageMultiplier = 1.3f;

    [Tooltip("是否以 weaponData.attackRange 为基础：true 时 thrustDistance 代表倍率（例如 1.2=attackRange*1.2）")]
    public bool useWeaponRangeAsBase = true;

    [Tooltip("突刺的插值曲线（默认更顺滑）")]
    public AnimationCurve thrustCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        weaponCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
            weaponCollider.enabled = false;
        }

        handlePoint = transform.Find("HandlePoint");
        if (handlePoint == null) handlePoint = transform;

        ApplyWeaponData();
    }

    void ApplyWeaponData()
    {
        if (weaponData == null) return;

        if (spriteRenderer != null && weaponData.weaponIcon != null)
            spriteRenderer.sprite = weaponData.weaponIcon;
    }

    #region Pivot
    public void SetPivot(Transform weaponHand)
    {
        pivot = weaponHand;
    }
    #endregion

    #region 状态
    public bool CanAttack() => !isAttacking;
    #endregion

    #region ===== 普通挥砍（鼠标左键）=====
    public void StartAttack()
    {
        if (isAttacking || pivot == null || weaponData == null) return;
        StartCoroutine(SlashRoutine());
    }

    IEnumerator SlashRoutine()
    {
        isAttacking = true;

        // 挥砍伤害倍率=1
        currentDamageMultiplier = 1f;

        canDamage = true;
        if (weaponCollider != null) weaponCollider.enabled = true;

        Quaternion startRot = pivot.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 0, weaponData.swingAngle);

        float elapsed = 0f;
        float dur = Mathf.Max(0.0001f, weaponData.swingDuration);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float p = weaponData.swingCurve.Evaluate(t);
            pivot.localRotation = Quaternion.Slerp(startRot, endRot, p);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        canDamage = false;
        if (weaponCollider != null) weaponCollider.enabled = false;

        // 收回
        elapsed = 0f;
        float backTime = dur * 0.3f;
        backTime = Mathf.Max(0.0001f, backTime);

        while (elapsed < backTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / backTime);
            pivot.localRotation = Quaternion.Slerp(endRot, startRot, t);
            yield return null;
        }

        pivot.localRotation = startRot;
        isAttacking = false;
    }
    #endregion

    #region ===== 突刺（空格）=====
    public void StartThrust()
    {
        if (isAttacking || pivot == null || weaponData == null) return;
        StartCoroutine(ThrustRoutine());
    }

    IEnumerator ThrustRoutine()
    {
        isAttacking = true;

        // ✅ 突刺伤害倍率
        currentDamageMultiplier = Mathf.Max(0f, thrustDamageMultiplier);

        canDamage = true;
        if (weaponCollider != null) weaponCollider.enabled = true;

        // 当前朝向（挂载点的右方向）
        Vector3 dir = pivot.right.normalized;

        // 用 pivot 的“本地位置”做插值
        Vector3 startPos = pivot.localPosition;

        float baseRange = Mathf.Max(0.0001f, weaponData.attackRange);
        float dist = useWeaponRangeAsBase ? (baseRange * thrustDistance) : thrustDistance;
        dist = Mathf.Max(0f, dist);

        Vector3 targetPos = startPos + dir * dist;

        float forwardTime = Mathf.Max(0.0001f, thrustForwardTime);
        float returnTime = Mathf.Max(0.0001f, thrustReturnTime);

        // 向前突刺
        float elapsed = 0f;
        while (elapsed < forwardTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / forwardTime);
            float p = thrustCurve.Evaluate(t);
            pivot.localPosition = Vector3.LerpUnclamped(startPos, targetPos, p);
            yield return null;
        }

        yield return new WaitForSeconds(0.03f);

        // 结束伤害帧
        canDamage = false;
        if (weaponCollider != null) weaponCollider.enabled = false;

        // 回收
        elapsed = 0f;
        while (elapsed < returnTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnTime);
            float p = thrustCurve.Evaluate(t);
            pivot.localPosition = Vector3.LerpUnclamped(targetPos, startPos, p);
            yield return null;
        }

        pivot.localPosition = startPos;
        isAttacking = false;
    }
    #endregion

    #region 碰撞伤害
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDamage || weaponData == null) return;

        // ✅ 只要碰到敌人的任意子Collider都可以（常见：BodyHitBox在子物体上）
        if (!other.CompareTag("Enemy")) return;

        // ✅ 关键：从父物体找 EnemyHealth（避免打到子Collider拿不到血条/倍率逻辑）
        EnemyHealth hp = other.GetComponentInParent<EnemyHealth>();
        if (hp != null)
        {
            float dmg = weaponData.damage * currentDamageMultiplier;
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            hp.TakeDamage(dmg, hitPoint);

            // 可选调试：确认确实命中了哪个Collider
            // Debug.Log($"Weapon hit: {other.name}, root health: {hp.gameObject.name}, dmg={dmg}");
        }
        // else
        // {
        //     Debug.LogWarning($"Hit {other.name} but no EnemyHealth found in parents.");
        // }
    }
    #endregion

    #region 公共
    public Vector3 GetHandleLocalPosition()
    {
        return handlePoint != null ? handlePoint.localPosition : Vector3.zero;
    }
    #endregion
}
