using UnityEngine;
using DG.Tweening;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class TileViewDual : MonoBehaviour
{
    [Header("Child visuals")]
    [SerializeField] GameObject unselectedGO;
    [SerializeField] GameObject selectedGO;

    [Header("Glow (2D / SpriteRenderer + URP/Particles/Unlit Additive)")]
    [SerializeField] GameObject glowHalo;
    [SerializeField] float   glowScale    = 1.08f;
    [SerializeField] string  glowLayerName = "TileGlow";

    [Header("Text (TMP) Colors")]
    [SerializeField] Color unselectedTextColor = Color.black;
    [SerializeField] Color selectedTextColor   = Color.white;

    [Header("Tween (select bump)")]
    [SerializeField] float bumpScale = 1.12f;
    [SerializeField] float bumpTime  = 0.12f;
    [SerializeField] Ease  ease      = Ease.OutBack;

    [Header("Collider fit")]
    [SerializeField] bool  fitColliderToSprite = true;
    [SerializeField] float colliderPadding     = 0.00f;

    [SerializeField] bool  isSelected = false;

    // -------- Vanish (N>=5 aynı hamlede, hamle bitince) --------
    [Header("Vanish Settings")]
    [SerializeField] GameObject vanishFxPrefab;           // Pivotta Instantiate edilir
    [SerializeField] float vanishStartDelayOnEnd = 0.12f; // MouseUp'tan sonra başlama gecikmesi
    [SerializeField] float vanishBetweenDelay    = 0.20f; // Her tile arası gecikme
    [SerializeField] float vanishUpScale         = 1.20f; // önce büyü
    [SerializeField] float vanishUpTime          = 0.12f;
    [SerializeField] float vanishDownTime        = 0.18f; // sonra 0'a küçül
    [SerializeField] Ease  vanishUpEase          = Ease.OutBack;
    [SerializeField] Ease  vanishDownEase        = Ease.InBack;
    [SerializeField] bool  vanishDestroy         = true;  // true: Destroy, false: SetActive(false)

    Vector3 baseScale = Vector3.one;
    Tween t;
    BoxCollider2D col;

    TextMeshProUGUI[] tmpTexts;
    TextMeshPro[]     tmp3DTexts;

    // ---------- HAREKET (gesture) bazlı seçim kuyruğu ----------
    static readonly System.Collections.Generic.List<TileViewDual> s_sessionQueue = new();
    static int  s_sessionId = 0;             // her Begin()'de artar
    int         _lastEnqueuedSessionId = -1; // bu tile son hangi harekette kuyruğa girdi?

    // ---- Line kapatma için event ----
    public static event System.Action OnVanishStarted;

    // ---- İsteğe bağlı otomatik MouseDown/MouseUp algılama ----
    public static bool AutoDetectGestures = true;
    static GestureDriver s_driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGestureDriver()
    {
        if (s_driver) return;
        var go = new GameObject("TileViewDual_GestureDriver");
        Object.DontDestroyOnLoad(go);
        s_driver = go.AddComponent<GestureDriver>();
    }

    class GestureDriver : MonoBehaviour
    {
        bool pressed;
        void Update()
        {
            if (!AutoDetectGestures) return;
            if (Input.GetMouseButtonDown(0))
            {
                TileViewDual.StartChainSession();
                pressed = true;
            }
            if (pressed && Input.GetMouseButtonUp(0))
            {
                TileViewDual.EndChainSessionAndMaybeVanish();
                pressed = false;
            }
        }
    }

    // === Dışarıdan manuel çağırmak istersen ===
    public static void StartChainSession()
    {
        s_sessionId++;
        s_sessionQueue.Clear();
    }

    // Hamle bitiminde çağrılır: N>=5 ise o hamlede seçilen TÜM tile'ları sırayla patlat
    public static void EndChainSessionAndMaybeVanish()
    {
        int n = s_sessionQueue.Count;
        if (n >= 5)
        {
            var cfg = s_sessionQueue[0];
            float startDelay = Mathf.Max(0f, cfg.vanishStartDelayOnEnd);
            float stepDelay  = Mathf.Max(0f, cfg.vanishBetweenDelay);

            // Patlama gerçekten BAŞLARKEN haber ver (LineRenderer'ları kapatabilsin)
            DOVirtual.DelayedCall(startDelay, () => OnVanishStarted?.Invoke());

            // Sırayla planla (ilk seçilenden son seçilene)
            for (int i = 0; i < n; i++)
            {
                var tile = s_sessionQueue[i];
                if (!tile) continue;

                // tekrar seçilmesin
                if (tile.col) tile.col.enabled = false;

                float delay = startDelay + i * stepDelay;
                tile.PlayVanishWithDelay(delay);
            }
        }

        // hamle bitti: kuyruk temiz
        s_sessionQueue.Clear();
    }

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

        UpdateTextColors();

        if (animate) Pulse();
        else { t?.Kill(); transform.localScale = baseScale; }

        // HAREKET İÇİ sayımı sadece select=true için yap
        if (sel) TryEnqueueThisTileForCurrentSession();
    }

    void UpdateTextColors()
    {
        var target = isSelected ? selectedTextColor : unselectedTextColor;

        if (tmpTexts != null)
            for (int i = 0; i < tmpTexts.Length; i++)
                if (tmpTexts[i]) tmpTexts[i].color = target;

        if (tmp3DTexts != null)
            for (int i = 0; i < tmp3DTexts.Length; i++)
                if (tmp3DTexts[i]) tmp3DTexts[i].color = target;
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

    void SafeInitScale()
    {
        if (transform.localScale.sqrMagnitude < 1e-6f) transform.localScale = Vector3.one;
        baseScale = transform.localScale;
    }

    void SetupGlowObject()
    {
        if (!glowHalo) return;
        int glowLayer = LayerMask.NameToLayer(glowLayerName);
        if (glowLayer >= 0) glowHalo.layer = glowLayer;
        glowHalo.transform.localScale = Vector3.one * glowScale;
        glowHalo.SetActive(isSelected);
        var sr = glowHalo.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = Mathf.Min(sr.sortingOrder, 0);
    }

    void CacheTexts()
    {
        tmpTexts    = GetComponentsInChildren<TextMeshProUGUI>(true);
        tmp3DTexts  = GetComponentsInChildren<TextMeshPro>(true);
    }

    // ==================== HAREKET İÇİ KUYRUK ====================
    void TryEnqueueThisTileForCurrentSession()
    {
        // Aynı hamlede aynı tile bir kez sayılmalı
        if (_lastEnqueuedSessionId == s_sessionId) return;
        _lastEnqueuedSessionId = s_sessionId;

        if (!s_sessionQueue.Contains(this))
            s_sessionQueue.Add(this);
    }

    // =============== VANISH (FX + scale anim) ===============
    void PlayVanishWithDelay(float delay)
    {
        // aktif bump tween varsa öldür
        t?.Kill();

        var seq = DOTween.Sequence();
        seq.AppendInterval(delay);

        // Patlama BAŞLAMADAN hemen önce seçili görseli kapat (5. tile turuncu görünsün diye End'de bekledik)
        seq.AppendCallback(() =>
        {
            if (isSelected) SetSelected(false, animate:false);
        });

        // FX (Play On Awake kapalı olsa bile)
        if (vanishFxPrefab)
        {
            seq.AppendCallback(() =>
            {
                var fx = Instantiate(vanishFxPrefab, transform.position, Quaternion.identity);
                ForcePlayAllEffects(fx);
            });
        }

        // büyü → küçül
        seq.Append(transform.DOScale(baseScale * vanishUpScale, vanishUpTime).SetEase(vanishUpEase));
        seq.Append(transform.DOScale(Vector3.zero, vanishDownTime).SetEase(vanishDownEase));

        seq.OnComplete(() =>
        {
            if (vanishDestroy) Destroy(gameObject);
            else gameObject.SetActive(false);
        });
    }

    // PlayOnAwake kapalı bile olsa tüm efektleri çalıştır
    void ForcePlayAllEffects(GameObject fxRoot)
    {
        if (!fxRoot) return;

        // ParticleSystem
        var psAll = fxRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < psAll.Length; i++)
            psAll[i].Play(true);

        // AudioSource
        var audAll = fxRoot.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audAll.Length; i++)
            if (audAll[i]) audAll[i].Play();

        // VFX Graph (varsa) — reflection
        var vfxType = System.Type.GetType("UnityEngine.VFX.VisualEffect, UnityEngine.VFXModule");
        if (vfxType != null)
        {
            var comps = fxRoot.GetComponentsInChildren(vfxType, true);
            foreach (var c in comps)
            {
                var m = vfxType.GetMethod("Play", System.Type.EmptyTypes);
                m?.Invoke(c, null);
            }
        }
    }
}
