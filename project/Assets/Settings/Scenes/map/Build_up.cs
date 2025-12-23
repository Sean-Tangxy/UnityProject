using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Build_up : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap groundTilemap;   // 放 road
    public Tilemap wallTilemap;     // 放 wall（加 TilemapCollider2D + CompositeCollider2D + Rigidbody2D(Static)）

    [Header("Tiles")]
    public TileBase road;
    public TileBase wall;

    [Header("Map")]
    public int map_n = 6;           // <=10
    public int baseCellSpacing = 15;

    [Header("Scale")]
    public int scale = 2;           // 整体翻倍

    int[,] position = new int[10, 10];
    int[,] finalmap = new int[10, 10];
    int[,] px = new int[10, 10];
    int[,] py = new int[10, 10];

    int sx = 0, sy = 0;

    HashSet<Vector3Int> roadSet = new HashSet<Vector3Int>();

    void Awake()
    {
        Generate();
    }

    void ClearAll()
    {
        groundTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        roadSet.Clear();
    }

    void SetRoad(Vector3Int p)
    {
        groundTilemap.SetTile(p, road);
        roadSet.Add(p);
    }

    void SetWall(Vector3Int p)
    {
        if (roadSet.Contains(p)) return;
        if (wallTilemap.GetTile(p) != null) return;
        wallTilemap.SetTile(p, wall);
    }

    // 中心对齐填充矩形（支持偶数尺寸）
    void FillRectCentered(Vector3Int center, int sizeX, int sizeY)
    {
        int startX = -sizeX / 2;
        int endX = startX + sizeX - 1;
        int startY = -sizeY / 2;
        int endY = startY + sizeY - 1;

        for (int dx = startX; dx <= endX; dx++)
            for (int dy = startY; dy <= endY; dy++)
                SetRoad(new Vector3Int(center.x + dx, center.y + dy, 0));
    }

    // 边框（墙厚=1），外侧有路则开门（该格设为 road，不放墙）
    void BorderRectCentered_WallWithDoor(Vector3Int center, int sizeX, int sizeY)
    {
        int startX = -sizeX / 2;
        int endX = startX + sizeX - 1;
        int startY = -sizeY / 2;
        int endY = startY + sizeY - 1;

        for (int dx = startX; dx <= endX; dx++)
        {
            for (int dy = startY; dy <= endY; dy++)
            {
                bool isBorder = (dx == startX || dx == endX || dy == startY || dy == endY);
                if (!isBorder) continue;

                Vector3Int p = new Vector3Int(center.x + dx, center.y + dy, 0);

                Vector3Int outward = Vector3Int.zero;
                if (dx == startX) outward = new Vector3Int(-1, 0, 0);
                else if (dx == endX) outward = new Vector3Int(1, 0, 0);
                else if (dy == startY) outward = new Vector3Int(0, -1, 0);
                else if (dy == endY) outward = new Vector3Int(0, 1, 0);

                Vector3Int outside = p + outward;

                if (roadSet.Contains(outside))
                    SetRoad(p);   // 开门
                else
                    SetWall(p);
            }
        }
    }

    // ====== 关键：走廊“加粗” ======
    // vertical=true 表示走廊方向是“竖直”(y变化)，需要在 x 方向刷宽度
    // vertical=false 表示走廊方向是“水平”(x变化)，需要在 y 方向刷宽度
    void PaintThickPoint(int x, int y, bool vertical)
    {
        // 让宽度尽量“居中”：scale=2 -> 偏移为 -1,0；scale=3 -> -1,0,1
        int oStart = -(scale / 2);
        int oEnd = oStart + scale - 1;

        if (vertical)
        {
            for (int ox = oStart; ox <= oEnd; ox++)
                SetRoad(new Vector3Int(x + ox, y, 0));
        }
        else
        {
            for (int oy = oStart; oy <= oEnd; oy++)
                SetRoad(new Vector3Int(x, y + oy, 0));
        }
    }

    // ===== DFS 连通 + 画“加粗走廊” =====
    void Connect(int x, int y)
    {
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int k = 0; k < 4; k++)
        {
            int nx = x + dx[k];
            int ny = y + dy[k];

            if (nx < 0 || ny < 0 || nx >= map_n || ny >= map_n) continue;
            if (position[nx, ny] != 1) continue;
            if (finalmap[nx, ny] == 1) continue;

            finalmap[nx, ny] = 1;

            int x0 = px[x, y], y0 = py[x, y];
            int x1 = px[nx, ny], y1 = py[nx, ny];

            if (x0 == x1)
            {
                // 竖直走廊：在x方向刷宽
                int ya = Mathf.Min(y0, y1);
                int yb = Mathf.Max(y0, y1);
                for (int yy = ya; yy <= yb; yy++)
                    PaintThickPoint(x0, yy, vertical: true);
            }
            else
            {
                // 水平走廊：在y方向刷宽
                int xa = Mathf.Min(x0, x1);
                int xb = Mathf.Max(x0, x1);
                for (int xx = xa; xx <= xb; xx++)
                    PaintThickPoint(xx, y0, vertical: false);
            }

            Connect(nx, ny);
        }
    }

    int CountConnectedCells()
    {
        int c = 0;
        for (int x = 0; x < map_n; x++)
            for (int y = 0; y < map_n; y++)
                if (finalmap[x, y] == 1) c++;
        return c;
    }

    // BFS + 叶子选终点
    Vector2Int FindEndCell()
    {
        const int INF = 9999;
        int[,] dist = new int[10, 10];
        for (int i = 0; i < 10; i++)
            for (int j = 0; j < 10; j++)
                dist[i, j] = INF;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(new Vector2Int(sx, sy));
        dist[sx, sy] = 0;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = cur.x + dx[k];
                int ny = cur.y + dy[k];
                if (nx < 0 || ny < 0 || nx >= map_n || ny >= map_n) continue;
                if (finalmap[nx, ny] != 1) continue;
                if (dist[nx, ny] != INF) continue;

                dist[nx, ny] = dist[cur.x, cur.y] + 1;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        Vector2Int best = new Vector2Int(sx, sy);
        int bestDist = -1;

        for (int x = 0; x < map_n; x++)
            for (int y = 0; y < map_n; y++)
            {
                if (finalmap[x, y] != 1) continue;
                if (position[x, y] != 1) continue;

                int deg = 0;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= map_n || ny >= map_n) continue;
                    if (finalmap[nx, ny] == 1) deg++;
                }

                if (deg == 1 && !(x == sx && y == sy))
                {
                    if (dist[x, y] > bestDist)
                    {
                        bestDist = dist[x, y];
                        best = new Vector2Int(x, y);
                    }
                }
            }

        if (bestDist < 0)
        {
            for (int x = 0; x < map_n; x++)
                for (int y = 0; y < map_n; y++)
                {
                    if (finalmap[x, y] != 1) continue;
                    if (position[x, y] != 1) continue;
                    if (dist[x, y] == INF) continue;
                    if (dist[x, y] > bestDist)
                    {
                        bestDist = dist[x, y];
                        best = new Vector2Int(x, y);
                    }
                }
        }

        return best;
    }

    void BuildStartFixed_5x5_Scaled()
    {
        Vector3Int c = new Vector3Int(px[sx, sy], py[sx, sy], 0);
        int startSize = 5 * scale; // 10
        FillRectCentered(c, startSize, startSize);
    }

    void BuildNormalRooms(Vector2Int endCell)
    {
        for (int x = 0; x < map_n; x++)
            for (int y = 0; y < map_n; y++)
            {
                if (finalmap[x, y] != 1) continue;

                if (x == sx && y == sy) continue;
                if (x == endCell.x && y == endCell.y) continue;

                Vector3Int c = new Vector3Int(px[x, y], py[x, y], 0);

                int e = Random.Range(2, 4);     // 2..3
                int size = (2 * e + 1) * scale; // 10..14
                FillRectCentered(c, size, size);
            }
    }

    void BuildEndRoom_Inner11_Scaled_NoWallAtConnection(Vector3Int center)
    {
        int innerSize = 11 * scale;    // 22
        int outerSize = innerSize + 2; // 24（墙厚1）

        FillRectCentered(center, innerSize, innerSize);
        BorderRectCentered_WallWithDoor(center, outerSize, outerSize);
    }

    void SurroundWalls()
    {
        HashSet<Vector3Int> candidates = new HashSet<Vector3Int>();

        foreach (var p in roadSet)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector3Int q = new Vector3Int(p.x + dx, p.y + dy, 0);
                    if (!roadSet.Contains(q) && wallTilemap.GetTile(q) == null)
                        candidates.Add(q);
                }
        }

        foreach (var q in candidates)
            SetWall(q);
    }

    void Generate()
    {
        while (true)
        {
            ClearAll();

            map_n = Random.Range(3, 6);
            sx = sy = 0;

            int spacing = baseCellSpacing * scale; // 间距也翻倍（整体翻倍）

            for (int x = 0; x < map_n; x++)
                for (int y = 0; y < map_n; y++)
                {
                    position[x, y] = Random.Range(0, 2);
                    finalmap[x, y] = 0;
                    px[x, y] = x * spacing;
                    py[x, y] = y * spacing;
                }

            position[sx, sy] = 1;
            finalmap[sx, sy] = 1;

            // 先连通并画“加粗走廊”
            Connect(sx, sy);

            int connected = CountConnectedCells();
            int need = map_n * map_n / 3 + 1;
            if (connected < need) continue;

            BuildStartFixed_5x5_Scaled();

            Vector2Int end = FindEndCell();
            Vector3Int endCenter = new Vector3Int(px[end.x, end.y], py[end.x, end.y], 0);

            BuildNormalRooms(end);

            BuildEndRoom_Inner11_Scaled_NoWallAtConnection(endCenter);

            SurroundWalls();

            Debug.Log($"scale={scale}, map_n={map_n}, connectedRooms={connected}, need={need}, endCell=({end.x},{end.y})");
            break;
        }
    }
}
