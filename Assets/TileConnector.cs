using UnityEngine;
using System.Collections.Generic;

public class TileConnector : MonoBehaviour
{
    [Header("Detect")]
    public LayerMask tileLayer = ~0;
    public float checkRadius = 0.35f;

    [Header("Line")]
    public float lineWidth = 0.08f;
    public float edgeInset = 0.02f;
    public int capVerts = 4, cornerVerts = 4;
    public string sortingLayer = "Default";
    public int orderInLayer = 0;

    [Header("Auto Tiling")]
    public bool autoTile = true;
    public float worldUnitsPerTile = 0.5f;
    public bool standardizeUV = true;

    [Header("Materials (Sprite shader!)")]
    public Material matHorizontal;
    public Material matVertical;
    public Material matDiagonal;

    [Header("Color (tek renk)")]
    public Color lineColor = Color.white;

    [Header("Selection behaviour")]
    public bool deselectOnNewPath = true;   // yeni çizime başlayınca
    public bool deselectOnFinish  = false;  // mouse bırakınca

    [Header("Debug")]
    public bool debug = false;

    bool isDrawing;
    readonly List<Transform> tiles           = new();
    readonly List<LineRenderer> segments     = new();
    readonly List<TileViewDual> selectedViews = new();   // <<< seçili görselleri tut

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) Begin();
        if (isDrawing && Input.GetMouseButton(0)) TrackTilesUnderMouse();
        if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;
            if (deselectOnFinish) DeselectAll();
        }
    }

    void Begin()
    {
        isDrawing = true;
        ClearSegments();
        tiles.Clear();
        if (deselectOnNewPath) DeselectAll();            // <<< eski seçimleri kapat
    }

    void TrackTilesUnderMouse()
    {
        // ekran -> dünya ve ray
        Vector3 mp = Input.mousePosition;
        var ray = Camera.main.ScreenPointToRay(mp);
        RaycastHit2D rh = Physics2D.GetRayIntersection(ray, Mathf.Infinity, tileLayer);

        Vector3 m = Camera.main.ScreenToWorldPoint(mp); m.z = 0f;
        Collider2D ch = rh.collider ? rh.collider : Physics2D.OverlapPoint(m, tileLayer);
        if (!ch) ch = Physics2D.OverlapCircle(m, checkRadius, tileLayer);

        if (debug) Debug.Log(ch ? $"HIT: {ch.transform.name}" : "NO HIT (layer/collider?)");
        if (!ch) return;

        // child'a çarpsak da tile root'una çık
        Transform root = GetTileRoot(ch.transform);

        // aynı tile'ı üst üste ekleme
        if (tiles.Count == 0 || tiles[^1] != root)
        {
            tiles.Add(root);

            // <<< SEÇİM GÖRSELİ: TileViewDual çağır
            var view = root.GetComponent<TileViewDual>() ?? root.GetComponentInChildren<TileViewDual>(true);
            if (view)
            {
                view.SetSelected(true, animate:true);
                if (!selectedViews.Contains(view)) selectedViews.Add(view);
            }

            if (tiles.Count >= 2)
            {
                Transform prev = tiles[^2];
                Transform curr = tiles[^1];

                Vector2 prevHalf = GetHalfExtentsSmart(prev, edgeInset);
                Vector2 currHalf = GetHalfExtentsSmart(curr, edgeInset);

                Vector3 pA = EdgePointFromTo(prev.position, curr.position, prevHalf);
                Vector3 pB = EdgePointFromTo(curr.position, prev.position, currHalf);

                Material pickedMat = PickMaterial(pA, pB);

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

    // --- helpers ---
    Transform GetTileRoot(Transform t)
    {
        var dual = t.GetComponentInParent<TileViewDual>(true);
        if (dual) return dual.transform;

        var sr = t.GetComponentInParent<SpriteRenderer>(true);
        if (sr) return sr.transform;

        var col = t.GetComponentInParent<Collider2D>(true);
        if (col) return col.transform;

        return t;
    }

    Vector2 GetHalfExtentsSmart(Transform t, float inset)
    {
        var col = t.GetComponentInChildren<Collider2D>(true);
        if (col) { var e = col.bounds.extents; return new Vector2(Mathf.Max(0, e.x - inset), Mathf.Max(0, e.y - inset)); }

        var sr = t.GetComponentInChildren<SpriteRenderer>(true);
        if (sr) { var e = sr.bounds.extents; return new Vector2(Mathf.Max(0, e.x - inset), Mathf.Max(0, e.y - inset)); }

        return new Vector2(0.5f, 0.5f);
    }

    Material PickMaterial(Vector3 a, Vector3 b)
    {
        Vector2 d = b - a;
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        const float bias = 1.2f;

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

        Material baseMat = srcMat != null ? srcMat : new Material(Shader.Find("Sprites/Default"));
        var inst = new Material(baseMat);
        if (inst.HasProperty("_Color")) inst.color = Color.white;
        lr.material = inst;

        lr.startColor = lineColor;
        lr.endColor   = lineColor;

        if (lr.material.mainTexture != null)
            lr.material.mainTexture.wrapMode = TextureWrapMode.Repeat;

        return lr;
    }

    void DeselectAll()
    {
        foreach (var v in selectedViews)
            if (v) v.SetSelected(false, animate:true);
        selectedViews.Clear();
    }

    void ClearSegments()
    {
        foreach (var lr in segments)
            if (lr) Destroy(lr.gameObject);
        segments.Clear();
    }

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

    void AutoTileByLength(LineRenderer lr, float unitsPerTile, bool reversed)
    {
        if (!lr.material || !lr.material.mainTexture) return;

        float len = 0f;
        for (int i = 1; i < lr.positionCount; i++)
            len += Vector3.Distance(lr.GetPosition(i - 1), lr.GetPosition(i));

        float tiles = Mathf.Max(1f, len / Mathf.Max(0.001f, unitsPerTile));
        if (!standardizeUV && reversed) tiles = -tiles;

        var scale = lr.material.mainTextureScale;
        scale.x = tiles;
        lr.material.mainTextureScale = scale;
        lr.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
    }

    void Swap(ref Vector3 a, ref Vector3 b) { var t = a; a = b; b = t; }
}
