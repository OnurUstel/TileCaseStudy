using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class TileViewDual : MonoBehaviour
{
    [Header("Child visuals")]
    [SerializeField] GameObject unselectedGO;   // Active = true
    [SerializeField] GameObject selectedGO;     // Active = false

    [Header("Glow (2D / SpriteRenderer + URP/Particles/Unlit Additive)")]
    [SerializeField] GameObject glowHalo;       // SpriteRenderer + GlowMat_Particle + Layer=TileGlow
    [SerializeField] float glowScale = 1.08f;   // tile’dan biraz büyük
    [SerializeField] string glowLayerName = "TileGlow";

    [Header("Tween")]
    [SerializeField] float bumpScale = 1.12f;
    [SerializeField] float bumpTime  = 0.12f;
    [SerializeField] Ease  ease      = Ease.OutBack;

    [Header("Collider fit")]
    [SerializeField] bool  fitColliderToSprite = true;
    [SerializeField] float colliderPadding     = 0.00f;

    [SerializeField] bool  isSelected = false;

    Vector3 baseScale = Vector3.one;
    Tween t;
    BoxCollider2D col;

    void Reset()
    {
        SafeInitScale();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        FitCollider();

        ApplySelection(isSelected, animate:false);
        SetupGlowObject();    // <-- glow kur
    }

    void Awake()
    {
        SafeInitScale();

        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        if (fitColliderToSprite) FitCollider();

        SetupGlowObject();           // <-- glow kur
        ApplySelection(isSelected, animate:false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SafeInitScale();

            col = GetComponent<BoxCollider2D>();
            if (col) col.isTrigger = true;
            if (fitColliderToSprite) FitCollider();

            SetupGlowObject();       // <-- editörde de doğru layer/scale
            ApplySelection(isSelected, animate:false);
        }
    }
#endif

    public void SetSelected(bool sel, bool animate = true)
        => ApplySelection(sel, animate);

    void ApplySelection(bool sel, bool animate)
    {
        isSelected = sel;

        if (unselectedGO) unselectedGO.SetActive(!sel);
        if (selectedGO)   selectedGO.SetActive(sel);
        if (glowHalo)     glowHalo.SetActive(sel);     // <-- sadece burada aç/kapat

        if (animate) Pulse();
        else
        {
            t?.Kill();
            transform.localScale = baseScale;
        }
    }

    void Pulse()
    {
        t?.Kill();
        transform.localScale = baseScale;
        t = transform.DOScale(baseScale * bumpScale, bumpTime)
                     .SetEase(ease)
                     .SetLoops(2, LoopType.Yoyo);
    }

    // Root collider'ı child SpriteRenderer'a göre ayarla
    void FitCollider()
    {
        if (!col) col = GetComponent<BoxCollider2D>();
        var sr = GetComponentInChildren<SpriteRenderer>(true);
        if (!col || !sr) return;

        Vector2 size = sr.bounds.size;
        size.x = Mathf.Max(0.01f, size.x - 2f * colliderPadding);
        size.y = Mathf.Max(0.01f, size.y - 2f * colliderPadding);
        col.size = size;

        Vector3 worldCenter = sr.bounds.center;
        Vector2 localOffset = transform.InverseTransformPoint(worldCenter);
        col.offset = localOffset;
    }

    // --- helpers ---
    void SafeInitScale()
    {
        if (transform.localScale.sqrMagnitude < 1e-6f) transform.localScale = Vector3.one;
        baseScale = transform.localScale;
    }

    void SetupGlowObject()
    {
        if (!glowHalo) return;

        // Layer = TileGlow (Overlay kamera sadece bunu görecek)
        int glowLayer = LayerMask.NameToLayer(glowLayerName);
        if (glowLayer >= 0) glowHalo.layer = glowLayer;

        // Ölçek ve başlangıç durumu
        glowHalo.transform.localScale = Vector3.one * glowScale;
        glowHalo.SetActive(isSelected);

        // SpriteRenderer varsa küçük güvenlik ayarı: ortada sorting arkada kalsın
        var sr = glowHalo.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // genelde tile’dan düşük order kullanırsın; istersen burada dokunma
            sr.sortingOrder = Mathf.Min(sr.sortingOrder, 0); 
        }
    }
}
