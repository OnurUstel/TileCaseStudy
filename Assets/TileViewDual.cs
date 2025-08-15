using UnityEngine;
using DG.Tweening;
using TMPro; // <= EKLENDİ

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class TileViewDual : MonoBehaviour
{
    [Header("Child visuals")]
    [SerializeField] GameObject unselectedGO;   // Active = true
    [SerializeField] GameObject selectedGO;     // Active = false

    [Header("Glow (2D / SpriteRenderer + URP/Particles/Unlit Additive)")]
    [SerializeField] GameObject glowHalo;       // SpriteRenderer + GlowMat_Particle + Layer=TileGlow
    [SerializeField] float   glowScale    = 1.08f;   // tile’dan biraz büyük
    [SerializeField] string  glowLayerName = "TileGlow";

    [Header("Text (TMP) Colors")]
    [SerializeField] Color unselectedTextColor = Color.black;
    [SerializeField] Color selectedTextColor   = Color.white;

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

    // Cache TMP refs
    TextMeshProUGUI[] tmpTexts;   // UGUI için
    TextMeshPro[]     tmp3DTexts; // 3D TextMeshPro kullanıyorsan da kapsasın

    void Reset()
    {
        SafeInitScale();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        FitCollider();

        CacheTexts();
        SetupGlowObject();
        ApplySelection(isSelected, animate:false);
    }

    void Awake()
    {
        SafeInitScale();

        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        if (fitColliderToSprite) FitCollider();

        CacheTexts();
        SetupGlowObject();
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

            CacheTexts();
            SetupGlowObject();
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
        if (glowHalo)     glowHalo.SetActive(sel);

        UpdateTextColors(); // <= RENKLERİ GÜNCELLE

        if (animate) Pulse();
        else
        {
            t?.Kill();
            transform.localScale = baseScale;
        }
    }

    void UpdateTextColors()
    {
        var target = isSelected ? selectedTextColor : unselectedTextColor;

        if (tmpTexts != null)
        {
            for (int i = 0; i < tmpTexts.Length; i++)
                if (tmpTexts[i]) tmpTexts[i].color = target;
        }

        if (tmp3DTexts != null)
        {
            for (int i = 0; i < tmp3DTexts.Length; i++)
                if (tmp3DTexts[i]) tmp3DTexts[i].color = target;
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

        // Layer = TileGlow (Overlay kullanıyorsan bu layer'ı sadece glow kameraya göster)
        int glowLayer = LayerMask.NameToLayer(glowLayerName);
        if (glowLayer >= 0) glowHalo.layer = glowLayer;

        glowHalo.transform.localScale = Vector3.one * glowScale;
        glowHalo.SetActive(isSelected);

        var sr = glowHalo.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // İstersen order'ı tile'dan düşükte tut
            sr.sortingOrder = Mathf.Min(sr.sortingOrder, 0);
        }
    }

    void CacheTexts()
    {
        // Tile'ın altındaki tüm TMP'leri yakala (deaktifler dahil)
        tmpTexts    = GetComponentsInChildren<TextMeshProUGUI>(true);
        tmp3DTexts  = GetComponentsInChildren<TextMeshPro>(true);
    }
}
