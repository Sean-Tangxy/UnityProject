using UnityEngine;

[CreateAssetMenu(fileName = "NewShieldData", menuName = "武器/盾牌数据")]
public class ShieldData : ScriptableObject
{
    [Header("基础信息")]
    public string shieldName = "盾牌";
    public Sprite shieldSprite;
    public Color shieldColor = Color.white;

    [Header("防御属性")]
    [Range(0f, 1f)] public float damageReduction = 0.7f; // 伤害减免70%
    [Range(0f, 1f)] public float blockAngle = 180f; // 防御角度（180度前方）
    public float blockStaminaCost = 10f; // 每次格挡消耗的体力
    public float blockCooldown = 0.5f; // 格挡冷却时间

    [Header("碰撞设置")]
    public Vector2 shieldSize = new Vector2(0.8f, 1.2f);
    public Vector2 shieldOffset = new Vector2(0.4f, 0f);

    [Header("效果")]
    public AudioClip blockSound;
    public GameObject blockEffectPrefab;
    public float blockShakeIntensity = 0.1f;
    public float blockShakeDuration = 0.2f;

    [Header("耐久度")]
    public float maxDurability = 100f;
    public float durabilityPerHit = 5f; // 每次格挡消耗的耐久

    [Header("特殊效果")]
    public bool canParry = true; // 是否可以弹反
    public float parryWindow = 0.2f; // 弹反窗口时间
    public float parryStunDuration = 1f; // 弹反成功后敌人的僵直时间
}