using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SimpleMapGenerator : MonoBehaviour
{
    [Header("必须设置的引用")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public TileBase roadTile;
    public TileBase wallTile;
    public GameObject enemyPrefab;

    [Header("终点传送门")]
    public GameObject portalPrefab;
    [Tooltip("传送门在终点房间中心的偏移(世界坐标)")]
    public Vector3 portalWorldOffset = new Vector3(0.5f, 0.5f, 0f);

    [Header("地图设置")]
    public int gridSize = 4;
    public int spacing = 15;
    public int scale = 2;

    [Header("房间设置")]
    public int minRoomSize = 10;
    public int maxRoomSize = 14;
    public int startRoomSize = 10; // 5 * scale
    public int endRoomInnerSize = 22; // 11 * scale

    [Header("敌人生成")]
    public int minEnemiesPerRoom = 1;
    public int maxEnemiesPerRoom = 3;
    [Tooltip("是否在起点房间生成敌人")]
    public bool spawnEnemiesInStartRoom = true;
    [Tooltip("是否在终点房间生成敌人")]
    public bool spawnEnemiesInEndRoom = true;
    [Tooltip("敌人之间的最小间距（格子数）")]
    public float minEnemySpacing = 2.0f;

    // 数据结构
    private int[,] position; // 哪些格子有房间
    private int[,] finalmap; // 哪些格子被连接
    private int[,] px, py;   // 每个格子对应的世界坐标
    private HashSet<Vector3Int> roadSet = new HashSet<Vector3Int>();
    private List<GameObject> enemies = new List<GameObject>();
    private int generationCount = 0;

    // 起点和终点
    private int sx = 0, sy = 0;
    private Vector2Int endCell;

    // 存储每个房间的位置信息
    private Dictionary<Vector2Int, List<Vector3Int>> roomPositions = new Dictionary<Vector2Int, List<Vector3Int>>();

    // 传送门实例引用（用于清理/防重复）
    private GameObject portalInstance;

    void Start()
    {
        Debug.Log("=== 地图生成器启动 ===");
        GenerateNewMap();
    }

    void InitializeArrays()
    {
        position = new int[gridSize, gridSize];
        finalmap = new int[gridSize, gridSize];
        px = new int[gridSize, gridSize];
        py = new int[gridSize, gridSize];
    }

    void GenerateNewMap()
    {
        generationCount++;
        Debug.Log($"第 {generationCount} 次生成地图");

        CleanPreviousMap();
        gridSize = Random.Range(3, 6);
        InitializeArrays();

        // 清空房间位置缓存
        roomPositions.Clear();

        // 随机生成房间布局
        GenerateRoomLayout();

        // 连接房间并绘制路径
        ConnectRoomsDFS();

        // 检查连通性，确保足够多的房间被连接
        int connected = CountConnectedCells();
        int need = gridSize * gridSize / 3 + 1;

        if (connected < need)
        {
            Debug.Log($"连通房间数不足 ({connected}/{need})，重新生成");
            GenerateNewMap();
            return;
        }

        // 找到终点（距离起点最远的叶子节点）
        FindEndCell();

        // 构建各种房间
        BuildStartRoom();
        BuildNormalRooms();
        BuildEndRoom();

        // 生成墙壁
        AddWalls();

        // 生成敌人（每个房间独立生成）
        GenerateEnemiesInAllRooms();

        Debug.Log($"生成完成！网格大小: {gridSize}，连通房间: {connected}/{gridSize * gridSize}，终点: ({endCell.x},{endCell.y})");
    }

    void GenerateRoomLayout()
    {
        // 随机决定哪些格子有房间
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // 30%概率有房间，但起点一定有
                position[x, y] = (x == sx && y == sy) ? 1 : (Random.value > 0.7f ? 1 : 0);
                finalmap[x, y] = 0;
                px[x, y] = x * spacing * scale;
                py[x, y] = y * spacing * scale;
            }
        }

        // 确保至少有一定数量的房间
        int roomCount = CountRooms();
        while (roomCount < gridSize)
        {
            int x = Random.Range(0, gridSize);
            int y = Random.Range(0, gridSize);
            if (position[x, y] == 0)
            {
                position[x, y] = 1;
                roomCount++;
            }
        }
    }

    int CountRooms()
    {
        int count = 0;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                if (position[x, y] == 1) count++;
        return count;
    }

    void ConnectRoomsDFS()
    {
        // 重置连接状态
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                finalmap[x, y] = 0;

        finalmap[sx, sy] = 1;
        DFSConnect(sx, sy);
    }

    void DFSConnect(int x, int y)
    {
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        // 随机顺序访问邻居
        List<int> indices = new List<int> { 0, 1, 2, 3 };
        Shuffle(indices);

        foreach (int k in indices)
        {
            int nx = x + dx[k];
            int ny = y + dy[k];

            if (nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize) continue;
            if (position[nx, ny] != 1) continue;
            if (finalmap[nx, ny] == 1) continue;

            finalmap[nx, ny] = 1;

            // 绘制加粗的连接路径
            DrawThickPath(x, y, nx, ny);

            DFSConnect(nx, ny);
        }
    }

    void DrawThickPath(int x1, int y1, int x2, int y2)
    {
        int worldX1 = px[x1, y1];
        int worldY1 = py[x1, y1];
        int worldX2 = px[x2, y2];
        int worldY2 = py[x2, y2];

        if (worldX1 == worldX2)
        {
            int startY = Mathf.Min(worldY1, worldY2);
            int endY = Mathf.Max(worldY1, worldY2);

            for (int y = startY; y <= endY; y++)
            {
                for (int ox = -scale / 2; ox <= scale / 2; ox++)
                {
                    Vector3Int pos = new Vector3Int(worldX1 + ox, y, 0);
                    SetTile(groundTilemap, pos, roadTile);
                    roadSet.Add(pos);
                }
            }
        }
        else if (worldY1 == worldY2)
        {
            int startX = Mathf.Min(worldX1, worldX2);
            int endX = Mathf.Max(worldX1, worldX2);

            for (int x = startX; x <= endX; x++)
            {
                for (int oy = -scale / 2; oy <= scale / 2; oy++)
                {
                    Vector3Int pos = new Vector3Int(x, worldY1 + oy, 0);
                    SetTile(groundTilemap, pos, roadTile);
                    roadSet.Add(pos);
                }
            }
        }
    }

    int CountConnectedCells()
    {
        int count = 0;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                if (finalmap[x, y] == 1) count++;
        return count;
    }

    void FindEndCell()
    {
        int[,] dist = new int[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                dist[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(sx, sy));
        dist[sx, sy] = 0;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();

            for (int k = 0; k < 4; k++)
            {
                int nx = cur.x + dx[k];
                int ny = cur.y + dy[k];

                if (nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize) continue;
                if (finalmap[nx, ny] != 1) continue;
                if (dist[nx, ny] != -1) continue;

                dist[nx, ny] = dist[cur.x, cur.y] + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        endCell = new Vector2Int(sx, sy);
        int maxDist = -1;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (finalmap[x, y] != 1 || position[x, y] != 1) continue;
                if (x == sx && y == sy) continue;

                int degree = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize) continue;
                    if (finalmap[nx, ny] == 1) degree++;
                }

                if (degree == 1 && dist[x, y] > maxDist)
                {
                    maxDist = dist[x, y];
                    endCell = new Vector2Int(x, y);
                }
            }
        }
    }

    void BuildStartRoom()
    {
        Vector3Int center = new Vector3Int(px[sx, sy], py[sx, sy], 0);
        FillRectCentered(center, startRoomSize, startRoomSize, new Vector2Int(sx, sy));
    }

    void BuildNormalRooms()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (finalmap[x, y] != 1) continue;
                if (x == sx && y == sy) continue;
                if (x == endCell.x && y == endCell.y) continue;

                Vector3Int center = new Vector3Int(px[x, y], py[x, y], 0);
                int roomSize = Random.Range(minRoomSize, maxRoomSize + 1);
                FillRectCentered(center, roomSize, roomSize, new Vector2Int(x, y));
            }
        }
    }

    void BuildEndRoom()
    {
        Vector3Int center = new Vector3Int(px[endCell.x, endCell.y], py[endCell.x, endCell.y], 0);

        // 内部房间
        FillRectCentered(center, endRoomInnerSize, endRoomInnerSize, endCell);

        // 外部墙壁（带门）
        int outerSize = endRoomInnerSize + 2;
        AddRoomWalls(center, outerSize, outerSize);

        // ✅在终点房间生成传送门
        SpawnPortalAtEndRoom(center);
    }

    void SpawnPortalAtEndRoom(Vector3Int endRoomCenterCell)
    {
        if (portalPrefab == null) return;

        // 先清掉旧的
        if (portalInstance != null) Destroy(portalInstance);

        // 生成容器（可选）
        Transform portalContainer = GameObject.Find("Portal")?.transform;
        if (portalContainer == null)
        {
            portalContainer = new GameObject("Portal").transform;
        }

        // 计算世界坐标：用 Tilemap 的 CellToWorld 更准确
        Vector3 baseWorldPos = groundTilemap.CellToWorld(endRoomCenterCell);
        Vector3 spawnWorldPos = baseWorldPos + portalWorldOffset;

        portalInstance = Instantiate(portalPrefab, spawnWorldPos, Quaternion.identity);
        portalInstance.name = "EndPortal";
        portalInstance.transform.SetParent(portalContainer, true);

        Debug.Log($"终点传送门已生成：cell({endCell.x},{endCell.y}) worldPos={spawnWorldPos}");
    }

    void FillRectCentered(Vector3Int center, int sizeX, int sizeY, Vector2Int roomCoord)
    {
        int startX = -sizeX / 2;
        int endX = startX + sizeX - 1;
        int startY = -sizeY / 2;
        int endY = startY + sizeY - 1;

        List<Vector3Int> roomCells = new List<Vector3Int>();

        for (int dx = startX; dx <= endX; dx++)
        {
            for (int dy = startY; dy <= endY; dy++)
            {
                Vector3Int pos = new Vector3Int(center.x + dx, center.y + dy, 0);
                SetTile(groundTilemap, pos, roadTile);
                roadSet.Add(pos);
                roomCells.Add(pos);
            }
        }

        roomPositions[roomCoord] = roomCells;
    }

    void AddRoomWalls(Vector3Int center, int sizeX, int sizeY)
    {
        int startX = -sizeX / 2;
        int endX = startX + sizeX - 1;
        int startY = -sizeY / 2;
        int endY = startY + sizeY - 1;

        for (int dx = startX; dx <= endX; dx++)
        {
            for (int dy = startY; dy <= endY; dy++)
            {
                if (dx != startX && dx != endX && dy != startY && dy != endY) continue;

                Vector3Int pos = new Vector3Int(center.x + dx, center.y + dy, 0);

                bool isDoor = false;
                if (dx == startX && roadSet.Contains(pos + Vector3Int.right)) isDoor = true;
                if (dx == endX && roadSet.Contains(pos + Vector3Int.left)) isDoor = true;
                if (dy == startY && roadSet.Contains(pos + Vector3Int.up)) isDoor = true;
                if (dy == endY && roadSet.Contains(pos + Vector3Int.down)) isDoor = true;

                if (isDoor)
                {
                    SetTile(groundTilemap, pos, roadTile);
                    roadSet.Add(pos);
                }
                else if (!roadSet.Contains(pos))
                {
                    SetTile(wallTilemap, pos, wallTile);
                }
            }
        }
    }

    void AddWalls()
    {
        HashSet<Vector3Int> candidates = new HashSet<Vector3Int>();

        foreach (var pos in roadSet)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector3Int neighbor = new Vector3Int(pos.x + dx, pos.y + dy, 0);

                    if (!roadSet.Contains(neighbor) && wallTilemap.GetTile(neighbor) == null)
                    {
                        candidates.Add(neighbor);
                    }
                }
            }
        }

        foreach (var pos in candidates)
        {
            SetTile(wallTilemap, pos, wallTile);
        }
    }

    void GenerateEnemiesInAllRooms()
    {
        if (enemyPrefab == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        enemies.Clear();

        GameObject enemyContainer = new GameObject("Enemies");

        foreach (var roomEntry in roomPositions)
        {
            Vector2Int roomCoord = roomEntry.Key;
            List<Vector3Int> roomCells = roomEntry.Value;

            if (roomCoord.x == sx && roomCoord.y == sy && !spawnEnemiesInStartRoom) continue;
            if (roomCoord.x == endCell.x && roomCoord.y == endCell.y && !spawnEnemiesInEndRoom) continue;

            int enemyCountInRoom = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
            enemyCountInRoom = Mathf.Min(enemyCountInRoom, roomCells.Count);

            List<Vector3Int> shuffledCells = new List<Vector3Int>(roomCells);
            Shuffle(shuffledCells);

            List<Vector3> placedEnemyPositions = new List<Vector3>();

            int enemiesPlaced = 0;
            for (int i = 0; i < shuffledCells.Count && enemiesPlaced < enemyCountInRoom; i++)
            {
                Vector3Int cellPos = shuffledCells[i];
                Vector3 worldPos = groundTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0);

                bool tooClose = false;
                foreach (Vector3 placedPos in placedEnemyPositions)
                {
                    if (Vector3.Distance(worldPos, placedPos) < minEnemySpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
                    enemy.transform.parent = enemyContainer.transform;
                    enemies.Add(enemy);
                    placedEnemyPositions.Add(worldPos);
                    enemiesPlaced++;
                }
            }

            if (enemiesPlaced < enemyCountInRoom)
            {
                Debug.LogWarning($"房间({roomCoord.x},{roomCoord.y})只生成了{enemiesPlaced}个敌人，目标{enemyCountInRoom}（可能因为间距限制）");
            }
        }

        Debug.Log($"总共生成 {enemies.Count} 个敌人，分布在 {roomPositions.Count} 个房间中");
    }

    void CleanPreviousMap()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        roadSet.Clear();
        roomPositions.Clear();

        foreach (var enemy in enemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        enemies.Clear();

        if (portalInstance != null) Destroy(portalInstance);
        portalInstance = null;

        var portalObj = GameObject.Find("Portal");
        if (portalObj != null) Destroy(portalObj);

        var enemiesObj = GameObject.Find("Enemies");
        if (enemiesObj != null) Destroy(enemiesObj);
    }

    void SetTile(Tilemap tilemap, Vector3Int pos, TileBase tile)
    {
        if (tilemap != null && tile != null)
        {
            tilemap.SetTile(pos, tile);
        }
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    [ContextMenu("重新生成地图")]
    public void Regenerate()
    {
        GenerateNewMap();
    }

    [ContextMenu("清理所有")]
    public void CleanAll()
    {
        CleanPreviousMap();
        Debug.Log("已清理所有");
    }

    void OnDrawGizmos()
    {
        if (position != null)
        {
            Gizmos.color = Color.yellow;
            float cellSize = spacing * scale;
            float totalSize = gridSize * cellSize;
            Gizmos.DrawWireCube(new Vector3(totalSize / 2 - cellSize / 2, totalSize / 2 - cellSize / 2, 0),
                               new Vector3(totalSize, totalSize, 0));

            Gizmos.color = Color.green;
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (position[x, y] == 1)
                    {
                        Vector3 center = new Vector3(px[x, y], py[x, y], 0);
                        Gizmos.DrawWireCube(center, new Vector3(10, 10, 0));
                    }
                }
            }
        }
    }
}
