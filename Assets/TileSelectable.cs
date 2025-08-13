using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class TileSelectable : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer sr;

    [Header("Sprites")]
    [SerializeField] private Sprite unselectedSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Tween")]
    [SerializeField] private float punchScale = 1.12f; // 1.0 = orijinal
    [SerializeField] private float punchTime  = 0.12f;
    [SerializeField] private Ease  ease       = Ease.OutBack;

    Vector3 _baseScale;
    Tween _t;

    void Awake()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        if (sr && unselectedSprite) sr.sprite = unselectedSprite;
    }

    public void SelectPulse()
    {
        if (sr && selectedSprite) sr.sprite = selectedSprite;
        PlayPulse();
    }

    public void Deselect()
    {
        _t?.Kill();
        transform.localScale = _baseScale;
        if (sr && unselectedSprite) sr.sprite = unselectedSprite;
    }

    void PlayPulse()
    {
        _t?.Kill();
        transform.localScale = _baseScale;
        float target = _baseScale.x * punchScale;
        _t = transform.DOScale(target, punchTime)
                      .SetEase(ease)
                      .SetLoops(2, LoopType.Yoyo);
    }
}
