// Assets/scripts/Weapons/WeaponData.cs
using UnityEngine;

[System.Serializable]
public class WeaponData
{
    [Header("基础信息")]
    public string weaponName = "新武器";
    public Sprite weaponIcon;

    [Header("攻击属性")]
    public float damage = 10f;
    public float attackRange = 1f;
    public float attackSpeed = 1f;
    public float attackCooldown = 0.5f;

    [Header("挥动设置")]
    public float swingAngle = 90f;
    public float swingDuration = 0.3f;
    public AnimationCurve swingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("效果")]
    public Color weaponColor = Color.white;
    public Vector2 gripOffset = Vector2.zero;

    // 音效字段（如果需要）
    public AudioClip swingSound;
    public AudioClip hitSound;

    // 计算属性
    public float GetActualCooldown()
    {
        return attackCooldown / Mathf.Max(0.1f, attackSpeed);
    }

    public float GetActualDamage()
    {
        return damage;
    }
}