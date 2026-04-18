using System;
using UnityEngine;
using DG.Tweening;

public class Block : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private BoxCollider2D _collider;

    public int GridRow { get; private set; }
    public int GridCol { get; private set; }

    public Transform T { get; private set; }

    private Tween _moveTween;
    private Tween _wobbleTween;

    private Action _onFallComplete;
    private TweenCallback _fallCompleteCallback;
    private TweenCallback FallCompleteCallback => _fallCompleteCallback ??= OnFallComplete;

    private static readonly Vector3 _squashVector = new Vector3(0.15f, -0.15f, 0);

    private void Awake()
    {
        T = transform;

        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_collider == null && !TryGetComponent(out _collider))
        {
            _collider = gameObject.AddComponent<BoxCollider2D>();
        }
    }

    public void OnSpawned()
    {
    }

    public void OnDespawned()
    {
        KillTweens();
    }

    public void Init(int row, int col, Sprite sprite)
    {
        KillTweens();
        GridRow = row;
        GridCol = col;
        _renderer.sprite = sprite;

        T.localPosition = new Vector3(col, row, 0);
        T.localRotation = Quaternion.identity;
        T.localScale = Vector3.one;
    }

    public void InitForFall(int targetRow, int col, Sprite sprite, Vector3 spawnPosition)
    {
        KillTweens();
        GridRow = targetRow;
        GridCol = col;
        _renderer.sprite = sprite;

        T.localPosition = spawnPosition;
        T.localRotation = Quaternion.identity;
        T.localScale = Vector3.one;
    }

    public void AnimateFall(float targetY, float fallSpeed = 1f, float delay = 0f, Action onComplete = null)
    {
        KillTweens();

        float distance = Mathf.Abs(T.localPosition.y - targetY);

        float duration = Mathf.Sqrt(distance) * (0.2f / fallSpeed);
        duration = Mathf.Clamp(duration, 0.05f, 0.8f);

        Vector3 targetPos = new Vector3(GridCol, targetY, 0);

        _onFallComplete = onComplete;

        _moveTween = T.DOLocalMove(targetPos, duration)
            .SetDelay(delay)
            .SetEase(Ease.InQuad)
            .OnComplete(FallCompleteCallback);
    }

    private void OnFallComplete()
    {
        DoLandingSquash();
        _onFallComplete?.Invoke();
    }

    private void DoLandingSquash()
    {
        T.DOPunchScale(_squashVector, 0.15f, 10, 0.5f);
    }

    public void KillTweens()
    {
        _moveTween?.Kill();
        _wobbleTween?.Kill();
        _moveTween = null;
        _wobbleTween = null;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    public void UpdateVisuals(Sprite sprite)
    {
        if (_renderer.sprite != sprite)
        {
            _renderer.sprite = sprite;
        }
    }

    public void UpdatePosition(int row, int col)
    {
        GridRow = row;
        GridCol = col;
    }
}