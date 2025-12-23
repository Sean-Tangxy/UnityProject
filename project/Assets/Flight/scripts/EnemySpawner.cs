using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnhancedEnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    public GameObject enemyPrefab;
    public int enemiesPerWave = 5;
    public float spawnInterval = 0.5f; // 每个敌人的生成间隔
    public float waveInterval = 3f;
    public float spawnRadius = 5f;

    [Header("位置设置")]
    public float minEnemyDistance = 1.5f; // 敌人间最小距离
    public LayerMask spawnBlockLayers; // 阻挡生成的图层

    [Header("波浪设置")]
    public bool spawnInWaves = true;
    public int totalWaves = 3;
    public bool infiniteWaves = false;

    [Header("状态")]
    private int currentWave = 0;
    private bool isSpawning = false;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    void Start()
    {
        if (spawnInWaves)
        {
            StartCoroutine(WaveSpawner());
        }
        else
        {
            StartCoroutine(ContinuousSpawner());
        }
    }

    void Update()
    {
        // 清理已销毁的敌人
        spawnedEnemies.RemoveAll(enemy => enemy == null);
    }

    IEnumerator WaveSpawner()
    {
        while (infiniteWaves || currentWave < totalWaves)
        {
            currentWave++;
            Debug.Log($"开始第 {currentWave} 波敌人生成");

            // 生成一波敌人
            yield return StartCoroutine(SpawnWave(enemiesPerWave));

            // 等待下一波
            yield return new WaitForSeconds(waveInterval);

            // 如果不是无限波，检查是否完成
            if (!infiniteWaves && currentWave >= totalWaves)
            {
                Debug.Log("所有波次完成");
                yield break;
            }
        }
    }

    IEnumerator ContinuousSpawner()
    {
        while (true)
        {
            yield return StartCoroutine(SpawnWave(enemiesPerWave));
            yield return new WaitForSeconds(waveInterval);
        }
    }

    IEnumerator SpawnWave(int enemyCount)
    {
        isSpawning = true;

        List<Vector3> usedPositions = new List<Vector3>();

        for (int i = 0; i < enemyCount; i++)
        {
            // 寻找有效生成位置
            Vector3 spawnPosition = FindValidSpawnPosition(usedPositions);

            if (spawnPosition != Vector3.negativeInfinity)
            {
                // 生成敌人
                GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                spawnedEnemies.Add(enemy);
                usedPositions.Add(spawnPosition);

                // 随机旋转（避免所有敌人面朝同一方向）
                float randomRotation = Random.Range(0f, 360f);
                enemy.transform.rotation = Quaternion.Euler(0, 0, randomRotation);

                // 添加随机AI延迟
                AddRandomAIDelay(enemy);

                Debug.Log($"生成敌人 {i + 1}/{enemyCount} 在位置: {spawnPosition}");
            }
            else
            {
                Debug.LogWarning($"无法为敌人 {i + 1} 找到有效位置");
            }

            // 等待间隔
            yield return new WaitForSeconds(spawnInterval);
        }

        isSpawning = false;
    }

    Vector3 FindValidSpawnPosition(List<Vector3> usedPositions)
    {
        int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 生成随机位置
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(0f, spawnRadius);
            Vector3 testPosition = transform.position + new Vector3(randomCircle.x, randomCircle.y, 0);

            // 检查是否被地形阻挡
            if (IsPositionBlocked(testPosition))
            {
                continue;
            }

            // 检查是否与其他生成位置太近
            bool tooCloseToOther = false;
            foreach (Vector3 usedPos in usedPositions)
            {
                if (Vector3.Distance(testPosition, usedPos) < minEnemyDistance)
                {
                    tooCloseToOther = true;
                    break;
                }
            }

            if (tooCloseToOther)
            {
                continue;
            }

            // 检查是否与现有敌人太近
            if (IsTooCloseToExistingEnemies(testPosition))
            {
                continue;
            }

            // 位置有效
            return testPosition;
        }

        // 尝试失败，返回无效位置
        return Vector3.negativeInfinity;
    }

    bool IsPositionBlocked(Vector3 position)
    {
        if (spawnBlockLayers.value == 0) return false;

        Collider2D hit = Physics2D.OverlapCircle(position, 0.5f, spawnBlockLayers);
        return hit != null;
    }

    bool IsTooCloseToExistingEnemies(Vector3 position)
    {
        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(position, minEnemyDistance);

        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.CompareTag("Enemy") && col.gameObject != gameObject)
            {
                return true;
            }
        }

        return false;
    }

    void AddRandomAIDelay(GameObject enemy)
    {
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            // 添加随机延迟启动
            StartCoroutine(DelayedAIStart(enemyAI, Random.Range(0f, 0.3f)));
        }
    }

    IEnumerator DelayedAIStart(EnemyAI enemyAI, float delay)
    {
        if (delay <= 0) yield break;

        // 保存原始状态
        bool wasEnabled = enemyAI.enabled;
        enemyAI.enabled = false;

        yield return new WaitForSeconds(delay);

        // 恢复状态
        if (enemyAI != null)
        {
            enemyAI.enabled = wasEnabled;
        }
    }

    // 清除所有生成的敌人
    public void ClearAllEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        spawnedEnemies.Clear();
    }

    // 强制生成单个敌人（用于测试）
    public void SpawnSingleEnemy()
    {
        if (!isSpawning)
        {
            StartCoroutine(SpawnSingleEnemyCoroutine());
        }
    }

    IEnumerator SpawnSingleEnemyCoroutine()
    {
        isSpawning = true;

        Vector3 spawnPosition = FindValidSpawnPosition(new List<Vector3>());
        if (spawnPosition != Vector3.negativeInfinity)
        {
            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            spawnedEnemies.Add(enemy);

            // 随机旋转
            enemy.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            // 添加AI延迟
            AddRandomAIDelay(enemy);

            Debug.Log($"生成了单个敌人在位置: {spawnPosition}");
        }

        yield return new WaitForSeconds(0.1f);
        isSpawning = false;
    }

    // 在Scene视图中显示生成范围
    void OnDrawGizmosSelected()
    {
        // 生成范围
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // 最小距离指示
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, minEnemyDistance);

        // 生成器位置
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}