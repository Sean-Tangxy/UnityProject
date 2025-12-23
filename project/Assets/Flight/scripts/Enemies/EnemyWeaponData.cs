using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyWeaponData", menuName = "武器/敌人武器数据")]
public class EnemyWeaponData : ScriptableObject
{
    [Header("基础信息")]
    public string weaponName = "敌人武器";
    public GameObject weaponPrefab;
    public Sprite weaponIcon;

    [Header("攻击属性")]
    public float baseDamage = 15f;
    public float attackKnockback = 8f;
    public float attackRange = 1.2f;
    public Vector2 attackSize = new Vector2(1.2f, 0.5f);

    [Header("攻击动画")]
    public float attackWindupTime = 0.2f;
    public float attackSwingTime = 0.3f;
    public float attackRecoveryTime = 0.2f;

    [Header("效果")]
    public AudioClip attackSound;
    public GameObject attackEffectPrefab;
    public Color weaponColor = Color.white;

    [Header("持有设置")]
    public Vector3 gripOffset = Vector3.zero;
    public float gripRotation = 0f;
}