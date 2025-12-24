using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;

    [Header("Tiles")]
    public TileBase roadTile;
    public TileBase wallTile;

    [Header("地图设置")]
    [Range(3, 6)] public int mapGridSize = 4;      // 3x3 到 6x6 的网格
    public int roomSpacing = 20;                   // 房间之间的间距
    [Range(1, 3)] public int scale = 2;            // 整体缩放

    [Header("房间设置")]
    public int startRoomSize = 5;                  // 起始房间大小（格子数）
    public int endRoomSize = 11;                   // 终点房间大小
    [Range(2, 4)] public int minRoomSize = 2;      // 最小房间大小参数
    [Range(3, 5)] public int maxRoomSize = 3;      // 最大房间大小参数

    [Header("敌人生成")]
    public GameObject enemyPrefab;
    [Range(1, 5)] public int minEnemiesPerRoom = 1;
    [Range(1, 5)] public int maxEnemiesPerRoom = 3;
    public float enemySpawnMargin = 1.5f;          // 距离墙壁的边距
    public bool spawnEnemiesInStartRoom = false;
    public bool spawnEnemiesInEndRoom = true;

    [Header("传送门生成")]
    public GameObject portalPrefab;
    public bool spawnPortalInEndRoom = true;
    public Vector2 portalOffset = new Vector2(0.5f, 0.5f); // 对齐格子中心
    public bool avoidNearWallForPortal = true;             // 尽量别贴墙
    [System.NonSerialized] private GameObject spawnedPortal;

    [Header("调试")]
    public bool drawGizmos = true;
    public bool logDetails = true;

    // 运行时数据（不序列化，每次重新生成）
    [System.NonSerialized] private List<Room> rooms = new List<Room>();
    [System.NonSerialized] private HashSet<Vector3Int> roadPositions = new HashSet<Vector3Int>();
    [System.NonSerialized] private List<GameObject> spawnedEnemies = new List<GameObject>();
    [System.NonSerialized] private int generationId = 0;

    void Start()
    {
        // 确保每次都是全新生成
        GenerateCompleteMap();
    }

    void GenerateCompleteMap()
    {
        generationId++;
        if (logDetails) Debug.Log($"=== 第 {generationId} 次地图生成开始 ===");

        // 1. 完全清理
        ClearEverything();

        // 2. 初始化随机种子
        InitializeRandom();

        // 3. 生成房间布局
        GenerateRoomLayout();

        // 4. 连接所有房间
        ConnectAllRooms();

        // 5. 构建房间（渲染到Tilemap）
        BuildAllRooms();

        // 6. 生成敌人（基于当前生成的地图）
        GenerateAllEnemies();

        // 7. 添加周围墙壁
        AddSurroundingWalls();

        // 8. 在终点房间生成传送门
        GenerateEndPortal();

        if (logDetails)
        {
            Debug.Log($"地图生成完成！");
            Debug.Log($"- 房间数量: {rooms.Count}");
            Debug.Log($"- 道路格子: {roadPositions.Count}");
            Debug.Log($"- 敌人数量: {spawnedEnemies.Count}");
            Debug.Log($"- 地图网格: {mapGridSize}x{mapGridSize}");
            Debug.Log($"- 传送门: {(spawnedPortal != null ? spawnedPortal.name : "未生成")}");
        }
    }

    void ClearEverything()
    {
        // 清理Tilemap
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        // 清理敌人
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        spawnedEnemies.Clear();

        // 清理传送门
        if (spawnedPortal != null) Destroy(spawnedPortal);
        spawnedPortal = null;

        // 清理数据
        rooms.Clear();
        roadPositions.Clear();

        if (logDetails) Debug.Log("场景清理完成");
    }

    void InitializeRandom()
    {
        // 使用时间和帧数确保每次不同
        int seed = (int)(Time.realtimeSinceStartup * 1000) + Time.frameCount;
        Random.InitState(seed);
        if (logDetails) Debug.Log($"随机种子: {seed}");

        // 随机地图大小（3-6）  注意：Random.Range(int,int) 上限不包含，所以用 7 才能取到 6
        mapGridSize = Random.Range(3, 7);
    }

    #region 房间生成
    void GenerateRoomLayout()
    {
        // 创建网格位置
        int spacing = roomSpacing * scale;

        for (int gridX = 0; gridX < mapGridSize; gridX++)
        {
            for (int gridY = 0; gridY < mapGridSize; gridY++)
            {
                // 随机决定是否有房间（70%几率）
                bool hasRoom = Random.value > 0.3f;
                if (gridX == 0 && gridY == 0) hasRoom = true; // 起点必须有房间

                if (hasRoom)
                {
                    // 计算世界坐标
                    int worldX = gridX * spacing;
                    int worldY = gridY * spacing;

                    // 确定房间类型
                    bool isStart = (gridX == 0 && gridY == 0);
                    bool isEnd = false; // 将在后续确定

                    // 创建房间
                    Room room = new Room
                    {
                        gridPosition = new Vector2Int(gridX, gridY),
                        worldCenter = new Vector3Int(worldX, worldY, 0),
                        isStartRoom = isStart,
                        isEndRoom = isEnd
                    };

                    // 设置房间大小
                    if (isStart)
                    {
                        room.width = startRoomSize * scale;
                        room.height = startRoomSize * scale;
                    }
                    else
                    {
                        int sizeParam = Random.Range(minRoomSize, maxRoomSize + 1);
                        room.width = (2 * sizeParam + 1) * scale;
                        room.height = (2 * sizeParam + 1) * scale;
                    }

                    rooms.Add(room);
                }
            }
        }

        // 设置终点房间（离起点最远的房间）
        SetEndRoom();

        if (logDetails) Debug.Log($"生成了 {rooms.Count} 个房间");
    }

    void SetEndRoom()
    {
        if (rooms.Count < 2) return;

        Room startRoom = rooms.Find(r => r.isStartRoom);
        if (startRoom == null) return;

        Room farthestRoom = null;
        float maxDistance = 0;

        foreach (var room in rooms)
        {
            if (room.isStartRoom) continue;

            float distance = Vector2Int.Distance(room.gridPosition, startRoom.gridPosition);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthestRoom = room;
            }
        }

        if (farthestRoom != null)
        {
            farthestRoom.isEndRoom = true;
            farthestRoom.width = endRoomSize * scale;
            farthestRoom.height = endRoomSize * scale;

            if (logDetails) Debug.Log($"终点房间: ({farthestRoom.gridPosition.x}, {farthestRoom.gridPosition.y})");
        }
    }
    #endregion

    #region 房间连接
    void ConnectAllRooms()
    {
        // 确保所有房间都连接到起点
        HashSet<Vector2Int> connectedRooms = new HashSet<Vector2Int>();
        Queue<Vector2Int> roomQueue = new Queue<Vector2Int>();

        // 从起点开始
        Room startRoom = rooms.Find(r => r.isStartRoom);
        if (startRoom == null) return;

        connectedRooms.Add(startRoom.gridPosition);
        roomQueue.Enqueue(startRoom.gridPosition);

        while (roomQueue.Count > 0)
        {
            Vector2Int current = roomQueue.Dequeue();
            Room currentRoom = GetRoomAtGrid(current);

            // 检查四个方向的邻居
            Vector2Int[] directions = {
                new Vector2Int(1, 0),  // 右
                new Vector2Int(-1, 0), // 左
                new Vector2Int(0, 1),  // 上
                new Vector2Int(0, -1)  // 下
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = current + dir;
                Room neighborRoom = GetRoomAtGrid(neighborPos);

                if (neighborRoom != null && !connectedRooms.Contains(neighborPos))
                {
                    // 连接这两个房间
                    ConnectTwoRooms(currentRoom, neighborRoom);

                    connectedRooms.Add(neighborPos);
                    roomQueue.Enqueue(neighborPos);
                }
            }
        }
    }

    void ConnectTwoRooms(Room roomA, Room roomB)
    {
        Vector3Int start = roomA.worldCenter;
        Vector3Int end = roomB.worldCenter;

        // 绘制加宽的走廊
        if (start.x == end.x) // 垂直走廊
        {
            int yMin = Mathf.Min(start.y, end.y);
            int yMax = Mathf.Max(start.y, end.y);

            for (int y = yMin; y <= yMax; y++)
            {
                DrawThickLine(start.x, y, true);
            }
        }
        else // 水平走廊
        {
            int xMin = Mathf.Min(start.x, end.x);
            int xMax = Mathf.Max(start.x, end.x);

            for (int x = xMin; x <= xMax; x++)
            {
                DrawThickLine(x, start.y, false);
            }
        }
    }

    void DrawThickLine(int centerX, int centerY, bool isVertical)
    {
        int halfThickness = scale / 2;

        if (isVertical)
        {
            for (int dx = -halfThickness; dx <= halfThickness; dx++)
            {
                Vector3Int pos = new Vector3Int(centerX + dx, centerY, 0);
                AddRoadTile(pos);
            }
        }
        else
        {
            for (int dy = -halfThickness; dy <= halfThickness; dy++)
            {
                Vector3Int pos = new Vector3Int(centerX, centerY + dy, 0);
                AddRoadTile(pos);
            }
        }
    }
    #endregion

    #region 房间构建
    void BuildAllRooms()
    {
        foreach (var room in rooms)
        {
            BuildRoom(room);
        }
    }

    void BuildRoom(Room room)
    {
        int halfWidth = room.width / 2;
        int halfHeight = room.height / 2;

        // 填充房间内部
        for (int dx = -halfWidth; dx <= halfWidth; dx++)
        {
            for (int dy = -halfHeight; dy <= halfHeight; dy++)
            {
                Vector3Int pos = new Vector3Int(room.worldCenter.x + dx, room.worldCenter.y + dy, 0);
                AddRoadTile(pos);
            }
        }

        // 如果是终点房间，添加特殊墙壁
        if (room.isEndRoom)
        {
            BuildEndRoomWalls(room);
        }
    }

    void BuildEndRoomWalls(Room room)
    {
        int innerHalfWidth = room.width / 2;
        int innerHalfHeight = room.height / 2;
        int outerHalfWidth = innerHalfWidth + 1;
        int outerHalfHeight = innerHalfHeight + 1;

        // 绘制外框墙壁
        for (int dx = -outerHalfWidth; dx <= outerHalfWidth; dx++)
        {
            for (int dy = -outerHalfHeight; dy <= outerHalfHeight; dy++)
            {
                bool isBorder = Mathf.Abs(dx) == outerHalfWidth || Mathf.Abs(dy) == outerHalfHeight;
                if (!isBorder) continue;

                Vector3Int pos = new Vector3Int(room.worldCenter.x + dx, room.worldCenter.y + dy, 0);

                // 检查外部是否有道路 -> 作为门洞
                bool shouldBeDoor = false;
                if (dx == -outerHalfWidth) shouldBeDoor = roadPositions.Contains(pos + new Vector3Int(-1, 0, 0));
                else if (dx == outerHalfWidth) shouldBeDoor = roadPositions.Contains(pos + new Vector3Int(1, 0, 0));
                else if (dy == -outerHalfHeight) shouldBeDoor = roadPositions.Contains(pos + new Vector3Int(0, -1, 0));
                else if (dy == outerHalfHeight) shouldBeDoor = roadPositions.Contains(pos + new Vector3Int(0, 1, 0));

                if (shouldBeDoor)
                {
                    AddRoadTile(pos); // 开门
                }
                else
                {
                    AddWallTile(pos);
                }
            }
        }
    }
    #endregion

    #region 敌人生成
    void GenerateAllEnemies()
    {
        if (enemyPrefab == null)
        {
            if (logDetails) Debug.LogWarning("未设置敌人预制体，跳过敌人生成");
            return;
        }

        // 创建敌人容器
        GameObject enemyContainer = new GameObject($"Enemies_Gen{generationId}");
        enemyContainer.transform.SetParent(transform);

        foreach (var room in rooms)
        {
            // 检查是否应该在这个房间生成敌人
            if (room.isStartRoom && !spawnEnemiesInStartRoom) continue;
            if (room.isEndRoom && !spawnEnemiesInEndRoom) continue;

            GenerateEnemiesInRoom(room, enemyContainer);
        }
    }

    void GenerateEnemiesInRoom(Room room, GameObject container)
    {
        int enemyCount = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        int enemiesSpawned = 0;

        float halfWidth = room.width / 2f - enemySpawnMargin;
        float halfHeight = room.height / 2f - enemySpawnMargin;

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPos = FindValidSpawnPosition(room, halfWidth, halfHeight);
            if (spawnPos != Vector3.zero)
            {
                GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                enemy.name = $"Enemy_R{room.gridPosition.x}_{room.gridPosition.y}_{i}";
                enemy.transform.SetParent(container.transform);

                spawnedEnemies.Add(enemy);
                enemiesSpawned++;
            }
        }

        if (logDetails && enemiesSpawned > 0)
        {
            Debug.Log($"房间({room.gridPosition.x},{room.gridPosition.y}) 生成 {enemiesSpawned}/{enemyCount} 个敌人");
        }
    }

    Vector3 FindValidSpawnPosition(Room room, float halfWidth, float halfHeight, int maxAttempts = 20)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float randomX = Random.Range(-halfWidth, halfWidth);
            float randomY = Random.Range(-halfHeight, halfHeight);
            Vector3 spawnPos = new Vector3(
                room.worldCenter.x + randomX,
                room.worldCenter.y + randomY,
                0
            );

            Vector3Int tilePos = new Vector3Int(
                Mathf.RoundToInt(spawnPos.x),
                Mathf.RoundToInt(spawnPos.y),
                0
            );

            // 检查位置是否在道路上
            if (roadPositions.Contains(tilePos))
            {
                return spawnPos;
            }
        }

        return Vector3.zero;
    }
    #endregion

    #region 传送门生成（终点房间）
    void GenerateEndPortal()
    {
        if (!spawnPortalInEndRoom) return;

        if (portalPrefab == null)
        {
            if (logDetails) Debug.LogWarning("未设置 portalPrefab，跳过传送门生成");
            return;
        }

        Room endRoom = rooms.Find(r => r.isEndRoom);
        if (endRoom == null)
        {
            if (logDetails) Debug.LogWarning("没有找到终点房间，跳过传送门生成");
            return;
        }

        Vector3Int tilePos = FindValidPortalTileInRoom(endRoom);
        if (tilePos.x == int.MinValue)
        {
            if (logDetails) Debug.LogWarning("没有找到合适的传送门落点（终点房间内无路面格子？）");
            return;
        }

        Vector3 worldPos = new Vector3(tilePos.x, tilePos.y, 0) + (Vector3)portalOffset;

        spawnedPortal = Instantiate(portalPrefab, worldPos, Quaternion.identity);
        spawnedPortal.name = $"Portal_EndRoom_{endRoom.gridPosition.x}_{endRoom.gridPosition.y}";
        spawnedPortal.transform.SetParent(transform);

        if (logDetails) Debug.Log($"已在终点房间生成传送门: tile({tilePos.x},{tilePos.y}) world({worldPos.x:F2},{worldPos.y:F2})");
    }

    Vector3Int FindValidPortalTileInRoom(Room room)
    {
        Vector3Int center = room.worldCenter;

        int halfW = room.width / 2;
        int halfH = room.height / 2;

        // 先从中心向外扩散找“路面且不贴墙”的位置
        int maxR = Mathf.Max(halfW, halfH);
        for (int r = 0; r <= maxR; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    // 只检查这一圈边界
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;

                    // 必须在房间边界内（严格限制在房间内部）
                    if (dx < -halfW || dx > halfW || dy < -halfH || dy > halfH) continue;

                    Vector3Int p = new Vector3Int(center.x + dx, center.y + dy, 0);

                    if (!roadPositions.Contains(p)) continue;

                    if (avoidNearWallForPortal && IsNearWall(p)) continue;

                    return p;
                }
            }
        }

        // 找不到“不贴墙”的，就退一步：房间内任意路面都行
        for (int dx = -halfW; dx <= halfW; dx++)
        {
            for (int dy = -halfH; dy <= halfH; dy++)
            {
                Vector3Int p = new Vector3Int(center.x + dx, center.y + dy, 0);
                if (roadPositions.Contains(p)) return p;
            }
        }

        return new Vector3Int(int.MinValue, int.MinValue, 0);
    }

    bool IsNearWall(Vector3Int tilePos)
    {
        if (wallTilemap == null) return false;

        // 8邻域有墙则判定靠近墙
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector3Int n = new Vector3Int(tilePos.x + dx, tilePos.y + dy, 0);
                if (wallTilemap.GetTile(n) != null) return true;
            }
        }
        return false;
    }
    #endregion

    #region 辅助功能
    void AddRoadTile(Vector3Int position)
    {
        if (groundTilemap == null) return;

        groundTilemap.SetTile(position, roadTile);
        roadPositions.Add(position);
    }

    void AddWallTile(Vector3Int position)
    {
        if (wallTilemap == null) return;
        if (roadPositions.Contains(position)) return;
        if (wallTilemap.GetTile(position) != null) return;

        wallTilemap.SetTile(position, wallTile);
    }

    Room GetRoomAtGrid(Vector2Int gridPos)
    {
        foreach (var room in rooms)
        {
            if (room.gridPosition == gridPos) return room;
        }
        return null;
    }

    void AddSurroundingWalls()
    {
        HashSet<Vector3Int> wallCandidates = new HashSet<Vector3Int>();

        // 收集所有道路旁边的空位
        foreach (var roadPos in roadPositions)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector3Int neighbor = new Vector3Int(roadPos.x + dx, roadPos.y + dy, 0);
                    if (!roadPositions.Contains(neighbor))
                    {
                        wallCandidates.Add(neighbor);
                    }
                }
            }
        }

        // 添加墙壁
        foreach (var pos in wallCandidates)
        {
            AddWallTile(pos);
        }
    }
    #endregion

    #region 房间类
    [System.Serializable]
    public class Room
    {
        public Vector2Int gridPosition;    // 在网格中的位置
        public Vector3Int worldCenter;     // 世界坐标中心
        public int width;                  // 房间宽度（格子数）
        public int height;                 // 房间高度（格子数）
        public bool isStartRoom;           // 是否是起始房间
        public bool isEndRoom;             // 是否是终点房间

        public Vector3 GetWorldCenterFloat()
        {
            return new Vector3(worldCenter.x, worldCenter.y, 0);
        }
    }
    #endregion

    #region 调试和工具
    [ContextMenu("重新生成地图")]
    public void RegenerateMap()
    {
        GenerateCompleteMap();
    }

    [ContextMenu("清理所有")]
    public void CleanAll()
    {
        ClearEverything();
    }

    [ContextMenu("显示地图信息")]
    public void LogMapInfo()
    {
        Debug.Log($"=== 地图信息 ===");
        Debug.Log($"生成ID: {generationId}");
        Debug.Log($"网格大小: {mapGridSize}x{mapGridSize}");
        Debug.Log($"房间数量: {rooms.Count}");
        Debug.Log($"道路格子: {roadPositions.Count}");
        Debug.Log($"敌人数量: {spawnedEnemies.Count}");
        Debug.Log($"传送门: {(spawnedPortal != null ? spawnedPortal.name : "未生成")}");

        foreach (var room in rooms)
        {
            string type = room.isStartRoom ? "起始" : room.isEndRoom ? "终点" : "普通";
            Debug.Log($"{type}房间: ({room.gridPosition.x},{room.gridPosition.y}) 大小:{room.width}x{room.height}");
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || rooms.Count == 0) return;

        // 绘制房间
        foreach (var room in rooms)
        {
            if (room.isStartRoom)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(room.GetWorldCenterFloat(), new Vector3(room.width, room.height, 0));
            }
            else if (room.isEndRoom)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(room.GetWorldCenterFloat(), new Vector3(room.width, room.height, 0));
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(room.GetWorldCenterFloat(), new Vector3(room.width, room.height, 0));
            }
        }

        // 绘制道路
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        foreach (var pos in roadPositions)
        {
            Gizmos.DrawCube(new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0), Vector3.one * 0.8f);
        }

        // 绘制敌人
        Gizmos.color = Color.magenta;
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Gizmos.DrawSphere(enemy.transform.position, 0.5f);
            }
        }

        // 绘制传送门
        if (spawnedPortal != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnedPortal.transform.position, 0.8f);
        }
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.white;
        boxStyle.fontSize = 12;

        string info = $"地图: {mapGridSize}x{mapGridSize}\n" +
                     $"房间: {rooms.Count}\n" +
                     $"敌人: {spawnedEnemies.Count}\n" +
                     $"生成: #{generationId}\n" +
                     $"传送门: {(spawnedPortal != null ? "已生成" : "无")}";

        GUI.Box(new Rect(10, 10, 170, 95), info, boxStyle);

        if (GUI.Button(new Rect(10, 115, 170, 30), "重新生成"))
        {
            RegenerateMap();
        }

        if (GUI.Button(new Rect(10, 155, 170, 30), "显示信息"))
        {
            LogMapInfo();
        }
    }
    #endregion
}
