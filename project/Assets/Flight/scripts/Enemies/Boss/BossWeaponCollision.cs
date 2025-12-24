using UnityEngine;

public class BossWeaponCollision : MonoBehaviour
{
    private BossEnemyController bossController;
    private int weaponID = -1;
    private bool isDashing = false;

    public void Initialize(BossEnemyController controller, int id)
    {
        bossController = controller;
        weaponID = id;

        // 添加物理材质防止弹开
        if (GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true; // 运动学刚体，不受物理影响
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 连续碰撞检测
        }
    }

    public void SetDashingState(bool dashing)
    {
        isDashing = dashing;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (bossController == null) return;

        // 检测玩家
        if (other.CompareTag("Player"))
        {
            if (isDashing)
            {
                // 冲刺攻击，伤害更高
                bossController.WeaponDashDamagePlayer(weaponID, other);
            }
            else
            {
                // 旋转攻击，伤害较低
                bossController.WeaponTryDamagePlayer(weaponID, other);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 持续接触也可以造成伤害
        if (other.CompareTag("Player"))
        {
            bossController.WeaponTryDamagePlayer(weaponID, other);
        }
    }
}