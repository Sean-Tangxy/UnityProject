// WeaponDataFactory.cs - 用于创建和管理WeaponData
using UnityEngine;

public static class WeaponDataFactory
{
    // 创建基础武器数据
    public static WeaponData CreateBasicWeapon(string name, float damage, float attackSpeed)
    {
        WeaponData data = new WeaponData
        {
            weaponName = name,
            damage = damage,
            attackSpeed = attackSpeed,
            attackCooldown = 1f / attackSpeed,
            attackRange = 1f,
            swingAngle = 90f,
            swingDuration = 0.3f,
            weaponColor = Color.white,
            gripOffset = Vector2.zero
        };

        return data;
    }

    // 预设武器类型
    public static WeaponData CreateSword()
    {
        return new WeaponData
        {
            weaponName = "铁剑",
            damage = 15f,
            attackSpeed = 1.2f,
            attackCooldown = 0.4f,
            attackRange = 0.8f,
            swingAngle = 120f,
            swingDuration = 0.25f,
            weaponColor = new Color(0.8f, 0.8f, 0.8f, 1f),
            gripOffset = new Vector2(0, 0.1f)
        };
    }

    public static WeaponData CreateAxe()
    {
        return new WeaponData
        {
            weaponName = "战斧",
            damage = 25f,
            attackSpeed = 0.8f,
            attackCooldown = 0.7f,
            attackRange = 1.2f,
            swingAngle = 140f,
            swingDuration = 0.4f,
            weaponColor = new Color(0.5f, 0.3f, 0.1f, 1f),
            gripOffset = new Vector2(0, 0.2f)
        };
    }

    public static WeaponData CreateDagger()
    {
        return new WeaponData
        {
            weaponName = "匕首",
            damage = 8f,
            attackSpeed = 2.0f,
            attackCooldown = 0.2f,
            attackRange = 0.5f,
            swingAngle = 60f,
            swingDuration = 0.15f,
            weaponColor = new Color(0.2f, 0.2f, 0.2f, 1f),
            gripOffset = new Vector2(0, -0.1f)
        };
    }
}