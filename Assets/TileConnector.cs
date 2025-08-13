using UnityEngine;
using System.Collections.Generic;

public class TileConnectorSegmentsHV : MonoBehaviour
{
    [Header("Detect")]
    public LayerMask tileLayer;
    public float checkRadius = 0.35f;

    [Header("Line")]
    public float lineWidth = 0.08f;
    public float edgeInset = 0.02f; // kenardan �ok az i�eri
    public int capVerts = 4, cornerVerts = 4;
    public string sortingLayer = "Default";
    public int orderInLayer = 0;

    [Header("Auto Tiling")]
    public bool autoTile = true;
    public float worldUnitsPerTile = 0.5f; // 1 tekrar�n d�nya birimi
    public bool standardizeUV = true;      // UV�yi eksene g�re sabitle (flip engellenir)

    [Header("Materials (Sprite shader kullan!)")]
    public Material matHorizontal;   // yatay segmentlerde
    public Material matVertical;     // dikey segmentlerde
    public Material matDiagonal;     // opsiyonel (�apraz)

    [Header("Color (tek renk)")]
    public Color lineColor = Color.white; // t�m segmentlerde ayn� tint

    private bool isDrawing;
    private readonly List<Transform> tiles = new();
    private readonly List<LineRenderer> segments = new();

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Begin();

        if (isDrawing && Input.GetMouseButton(0))
            TrackTilesUnderMouse();

        if (Input.GetMouseButtonUp(0))
            isDrawing = false;
    }

    void Begin()
    {
        isDrawing = true;
        ClearSegments();
        tiles.Clear();
    }

    void TrackTilesUnderMouse()
    {
        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        m.z = 0f;

        Collider2D hit = Physics2D.OverlapCircle(m, checkRadius, tileLayer);
        if (!hit) return;

        Transform t = hit.transform;

        // Ayn� tile'� art arda ekleme
        if (tiles.Count == 0 || tiles[^1] != t)
        {
            // if (tiles.Contains(t)) return; // geri d�n��� istemezsen a�
            tiles.Add(t);

            if (tiles.Count >= 2)
            {
                Transform prev = tiles[^2];
                Transform curr = tiles[^1];

                // ��k��-giri� kenar noktalar� (merkezden kenara k�rpma)
                Vector2 prevHalf = GetHalfExtents(prev, edgeInset);
                Vector2 currHalf = GetHalfExtents(curr, edgeInset);

                Vector3 pA = EdgePointFromTo(prev.position, curr.position, prevHalf);
                Vector3 pB = EdgePointFromTo(curr.position, prev.position, currHalf);

                // Y�n�ne g�re materyal se�
                Material pickedMat = PickMaterial(pA, pB);

                // --- UV sabitleme: yatayda soldan->sa�a, dikeyde a�a��dan->yukar�ya ---
                bool reversed = false;
                if (standardizeUV)
                {
                    Vector2 d = pB - pA;
                    if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
                    {
                        if (pA.x > pB.x) { Swap(ref pA, ref pB); reversed = true; }
                    }
                    else
                    {
                        if (pA.y > pB.y) { Swap(ref pA, ref pB); reversed = true; }
                    }
                }

                LineRenderer lr = CreateSegment(pickedMat);
                lr.SetPosition(0, pA);
                lr.SetPosition(1, pB);
                segments.Add(lr);

                if (autoTile) AutoTileByLength(lr, worldUnitsPerTile, reversed);
            }
        }
    }

    // ---- Y�n bazl� materyal se�imi ----
    Material PickMaterial(Vector3 a, Vector3 b)
    {
        Vector2 d = b - a;
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        const float bias = 1.2f; // k���k a�� sapmalar�n� yok say

        if (ax > ay * bias) return matHorizontal ? matHorizontal : matVertical;
        if (ay > ax * bias) return matVertical ? matVertical : matHorizontal;
        return matDiagonal ? matDiagonal : (matHorizontal ? matHorizontal : matVertical);
    }

    LineRenderer CreateSegment(Material srcMat)
    {
        GameObject go = new GameObject("LineSegment");
        var lr = go.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Tile;
        lr.alignment = LineAlignment.View;
        lr.startWidth = lineWidth;
        lr.endWidth   = lineWidth;
        lr.numCapVertices = capVerts;
        lr.numCornerVertices = cornerVerts;
        lr.sortingLayerName = sortingLayer;
        lr.sortingOrder = orderInLayer;

        // Her segmente kendi material instance'�
        Material baseMat = srcMat != null ? srcMat : new Material(Shader.Find("Sprites/Default"));
        var inst = new Material(baseMat);
        if (inst.HasProperty("_Color")) inst.color = Color.white; // materyal beyaz kals�n
        lr.material = inst;

        // Tek renk tint
        lr.startColor = lineColor;
        lr.endColor   = lineColor;

        if (lr.material.mainTexture != null)
            lr.material.mainTexture.wrapMode = TextureWrapMode.Repeat;

        return lr;
    }

    void ClearSegments()
    {
        foreach (var lr in segments)
            if (lr) Destroy(lr.gameObject);
        segments.Clear();
    }

    Vector2 GetHalfExtents(Transform t, float inset)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        var col = t.GetComponent<Collider2D>();
        Vector2 ext;

        if (sr) ext = sr.bounds.extents;
        else if (col) ext = col.bounds.extents;
        else ext = new Vector2(0.5f, 0.5f);

        ext.x = Mathf.Max(0f, ext.x - inset);
        ext.y = Mathf.Max(0f, ext.y - inset);
        return ext;
    }

    // from merkezinden to y�n�ne giderken dikd�rtgen s�n�r�na temas noktas�
    Vector3 EdgePointFromTo(Vector3 fromCenter, Vector3 toCenter, Vector2 half)
    {
        Vector2 dir = (toCenter - fromCenter);
        if (dir.sqrMagnitude < 1e-6f) return fromCenter;
        dir.Normalize();

        float tx = Mathf.Abs(dir.x) > 1e-6f ? half.x / Mathf.Abs(dir.x) : float.PositiveInfinity;
        float ty = Mathf.Abs(dir.y) > 1e-6f ? half.y / Mathf.Abs(dir.y) : float.PositiveInfinity;
        float t = Mathf.Min(tx, ty);

        Vector2 offset = dir * t;
        return fromCenter + new Vector3(offset.x, offset.y, 0f);
    }

    // �izgi uzunlu�una g�re texture tiling (+ ters y�n i�in negatif tiling)
    void AutoTileByLength(LineRenderer lr, float unitsPerTile, bool reversed)
    {
        if (!lr.material || !lr.material.mainTexture) return;

        float len = 0f;
        for (int i = 1; i < lr.positionCount; i++)
            len += Vector3.Distance(lr.GetPosition(i - 1), lr.GetPosition(i));

        float tiles = Mathf.Max(1f, len / Mathf.Max(0.001f, unitsPerTile));
        if (!standardizeUV && reversed) tiles = -tiles;  // normalize kapal�ysa flip�i d�zelt

        var scale = lr.material.mainTextureScale;
        scale.x = tiles;
        lr.material.mainTextureScale = scale;
        lr.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
    }

    // helpers
    void Swap(ref Vector3 a, ref Vector3 b) { var t = a; a = b; b = t; }
}
