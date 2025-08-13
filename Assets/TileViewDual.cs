using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]               // ← Root'ta collider şart
public class TileViewDual : MonoBehaviour
{
    [Header("Child visuals")]
    [SerializeField] GameObject unselectedGO;   // Active = true
    [SerializeField] GameObject selectedGO;     // Active = false

    [Header("Tween")]
    [SerializeField] float bumpScale = 1.12f;
    [SerializeField] float bumpTime  = 0.12f;
    [SerializeField] Ease  ease      = Ease.OutBack;

    [Header("Collider fit")]
    [SerializeField] bool  fitColliderToSprite = true;   // child sprite'a göre boyutla
    [SerializeField] float colliderPadding     = 0.00f;  // kenarlardan ek boşluk
    [SerializeField] bool  isSelected = false;

    Vector3 baseScale = Vector3.one;
    Tween t;
    BoxCollider2D col;

    void Reset()
    {
        // güvenli başlangıç
        if (transform.localScale.sqrMagnitude < 1e-6f) transform.localScale = Vector3.one;
        baseScale = transform.localScale;

        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        FitCollider();

        if (unselectedGO) unselectedGO.SetActive(!isSelected);
        if (selectedGO)   selectedGO.SetActive(isSelected);
    }

    void Awake()
    {
        if (transform.localScale.sqrMagnitude < 1e-6f) transform.localScale = Vector3.one;
        baseScale = transform.localScale;

        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        if (fitColliderToSprite) FitCollider();

        // sadece aktif/pasif ayarla (scale'a dokunma)
        if (unselectedGO) unselectedGO.SetActive(!isSelected);
        if (selectedGO)   selectedGO.SetActive(isSelected);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            col = GetComponent<BoxCollider2D>();
            if (col) col.isTrigger = true;
            if (fitColliderToSprite) FitCollider();

            if (unselectedGO) unselectedGO.SetActive(!isSelected);
            if (selectedGO)   selectedGO.SetActive(isSelected);
        }
    }
#endif

    public void SetSelected(bool sel, bool animate = true)
    {
        isSelected = sel;

        if (unselectedGO) unselectedGO.SetActive(!sel);
        if (selectedGO)   selectedGO.SetActive(sel);

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

        // Not: root'un scale=1, rotasyon=0 varsayımıyla çalışır (önerilen).
        Vector2 size = sr.bounds.size;
        size.x = Mathf.Max(0.01f, size.x - 2f * colliderPadding);
        size.y = Mathf.Max(0.01f, size.y - 2f * colliderPadding);
        col.size = size;

        Vector3 worldCenter = sr.bounds.center;
        Vector2 localOffset = transform.InverseTransformPoint(worldCenter);
        col.offset = localOffset;
    }
}
