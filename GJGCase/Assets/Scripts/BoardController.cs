using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public enum GameState
{
    Init,
    WaitingForInput,
    ProcessingMatch,
    ApplyingGravity,
    RefillingBoard,
    Shuffling
}

public class BoardController : MonoBehaviour
{
    #region Inspector References

    [Header("Settings & Managers")]
    [SerializeField] private LevelSettings _levelSettings;
    [SerializeField] private GameSettings _gameSettings;
    [SerializeField] private PoolManager _poolManager;

    #endregion

    #region Private State

    private BoardLogic _logic;
    private Block[] _visualBlocks;

    private GameState _currentState = GameState.Init;
    private Camera _mainCamera;

    private readonly List<int> _procMatchBuffer = new List<int>(64);
    private readonly List<(Block block, float currentY)> _syncColBuffer = new List<(Block, float)>(16);
    private readonly List<int> _spawnRowsBuffer = new List<int>(16);

    private WaitForSeconds _waitShort;
    private WaitForSeconds _waitShuffle;

    private int[] _groupSizeCache;
    private bool[] _groupVisited;

    private int _pendingAnimations;
    private Action _onBlockAnimationComplete;

    private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[1];
    private ContactFilter2D _clickFilter;

    #endregion

    #region Constants

    private const float SHUFFLE_WAVE_DELAY = 0.02f;
    private const float SHUFFLE_SCALE_OUT = 0.3f;
    private const float SHUFFLE_SCALE_OUT_DURATION = 0.2f;
    private const float SHUFFLE_SCALE_IN_DURATION = 0.3f;
    private const float SHUFFLE_ROTATION_DEGREES = 180f;
    private const float SHUFFLE_PUNCH_MAGNITUDE = 0.15f;
    private const float SHUFFLE_PUNCH_DURATION = 0.2f;
    private const int SHUFFLE_PUNCH_VIBRATO = 5;
    private const float SHUFFLE_PUNCH_ELASTICITY = 0.5f;

    #endregion

    #region Lifecycle

    private void Start()
    {
        _mainCamera = Camera.main;

        _waitShort = new WaitForSeconds(_gameSettings.waitShort);
        _waitShuffle = new WaitForSeconds(_gameSettings.waitShuffle);

        _onBlockAnimationComplete = OnBlockAnimationComplete;
        _clickFilter = new ContactFilter2D { useTriggers = false };

        StartGame();
    }

    private void OnDestroy()
    {
        if (_logic != null)
        {
            _logic.OnBlocksMatched -= OnLogicBlocksMatched;
        }

        DOTween.Kill(transform, true);
    }

    private void Update()
    {
        if (_currentState != GameState.WaitingForInput) return;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            HandleInput(mouse);
        }
    }

    #endregion

    #region Initialization

    private void StartGame()
    {
        int cols = _levelSettings.n_cols;
        int rows = _levelSettings.m_rows;
        int totalCells = cols * rows;

        _logic = new BoardLogic(cols, rows, _levelSettings.k_colors);
        _logic.OnBlocksMatched += OnLogicBlocksMatched;

        _visualBlocks = new Block[totalCells];
        _groupSizeCache = new int[totalCells];
        _groupVisited = new bool[totalCells];

        SpawnInitialBoard();
        UpdateAllVisuals();

        if (_logic.IsDeadlocked())
        {
            _currentState = GameState.Shuffling;
            StartCoroutine(ShuffleRoutine());
        }
        else
        {
            _currentState = GameState.WaitingForInput;
        }
    }

    private void SpawnInitialBoard()
    {
        for (int i = 0; i < _visualBlocks.Length; i++)
        {
            _logic.GetCoordinates(i, out int x, out int y);
            int colorIndex = _logic.Grid[i];
            Sprite icon = GetSpriteForGroup(colorIndex, 1);

            Block b = _poolManager.GetBlock();
            b.Init(y, x, icon);
            _visualBlocks[i] = b;
        }
    }

    #endregion

    #region Input

    private void HandleInput(Mouse mouse)
    {
        Vector2 mouseScreenPos = mouse.position.ReadValue();
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

        int hitCount = Physics2D.Raycast(worldPos, Vector2.zero, _clickFilter, _hitBuffer);

        if (hitCount > 0 && _hitBuffer[0].collider.TryGetComponent(out Block clickedBlock))
        {
            int index = _logic.GetIndex(clickedBlock.GridCol, clickedBlock.GridRow);
            var match = _logic.GetMatchBuffer(index);

            if (match.Count >= 2)
            {
                _procMatchBuffer.Clear();
                _procMatchBuffer.AddRange(match);
                StartCoroutine(ProcessMatch(_procMatchBuffer));
            }
        }
    }

    #endregion

    #region Match Processing Pipeline

    private IEnumerator ProcessMatch(List<int> match)
    {
        _currentState = GameState.ProcessingMatch;

        _logic.RemoveBlocks(match);
        yield return _waitShort;

        _currentState = GameState.ApplyingGravity;
        _logic.ApplyGravity();
        yield return SyncVisualsWithLogic();

        _currentState = GameState.RefillingBoard;
        _logic.RefillBoard();
        yield return SpawnNewBlocksFromTop();

        UpdateAllVisuals();

        if (_logic.IsDeadlocked())
        {
            _currentState = GameState.Shuffling;
            yield return ShuffleRoutine();
        }

        _currentState = GameState.WaitingForInput;
    }

    private void OnLogicBlocksMatched(List<int> matchedBlockIndices)
    {
        for (int i = 0; i < matchedBlockIndices.Count; i++)
        {
            int index = matchedBlockIndices[i];
            Block b = _visualBlocks[index];
            if (b != null)
            {
                _poolManager.ReleaseBlock(b);
                _visualBlocks[index] = null;
            }
        }
    }

    private void OnBlockAnimationComplete()
    {
        _pendingAnimations--;
    }

    #endregion

    #region Gravity & Refill Animations

    private IEnumerator SyncVisualsWithLogic()
    {
        _pendingAnimations = 0;
        int rows = _levelSettings.m_rows;
        int cols = _levelSettings.n_cols;

        for (int x = 0; x < cols; x++)
        {
            _syncColBuffer.Clear();

            for (int y = rows - 1; y >= 0; y--)
            {
                int idx = _logic.GetIndex(x, y);
                if (_visualBlocks[idx] != null)
                {
                    _syncColBuffer.Add((_visualBlocks[idx], _visualBlocks[idx].T.localPosition.y));
                    _visualBlocks[idx] = null;
                }
            }

            int targetIndex = _syncColBuffer.Count - 1;

            for (int i = 0; i < _syncColBuffer.Count; i++)
            {
                Block b = _syncColBuffer[i].block;
                int targetY = targetIndex - i;
                int newIdx = _logic.GetIndex(x, targetY);

                _visualBlocks[newIdx] = b;
                b.UpdatePosition(targetY, x);

                float currentYPos = _syncColBuffer[i].currentY;

                if (Mathf.Abs(currentYPos - targetY) > 0.01f)
                {
                    _pendingAnimations++;
                    b.AnimateFall(targetY, _gameSettings.fallSpeed, 0f, _onBlockAnimationComplete);
                }
            }
        }

        while (_pendingAnimations > 0) yield return null;
    }

    private IEnumerator SpawnNewBlocksFromTop()
    {
        _pendingAnimations = 0;
        int rows = _levelSettings.m_rows;
        int cols = _levelSettings.n_cols;

        for (int x = 0; x < cols; x++)
        {
            _spawnRowsBuffer.Clear();

            for (int y = 0; y < rows; y++)
            {
                if (_visualBlocks[_logic.GetIndex(x, y)] == null)
                {
                    _spawnRowsBuffer.Add(y);
                }
            }

            if (_spawnRowsBuffer.Count == 0) continue;

            for (int i = 0; i < _spawnRowsBuffer.Count; i++)
            {
                int targetY = _spawnRowsBuffer[i];
                int targetIdx = _logic.GetIndex(x, targetY);
                int color = _logic.Grid[targetIdx];

                float spawnY = rows + i;

                Block b = _poolManager.GetBlock();
                b.InitForFall(targetY, x, GetSpriteForGroup(color, 1), new Vector3(x, spawnY, 0));
                _visualBlocks[targetIdx] = b;

                _pendingAnimations++;
                b.AnimateFall(targetY, _gameSettings.fallSpeed, 0f, _onBlockAnimationComplete);
            }
        }

        while (_pendingAnimations > 0) yield return null;
    }

    #endregion

    #region Shuffle

    private IEnumerator ShuffleRoutine()
    {
        yield return _waitShuffle;
        _logic.SmartShuffle();

        Vector3 punchVector = new Vector3(SHUFFLE_PUNCH_MAGNITUDE, SHUFFLE_PUNCH_MAGNITUDE, SHUFFLE_PUNCH_MAGNITUDE);
        Vector3 targetRotation = new Vector3(0, 0, SHUFFLE_ROTATION_DEGREES);
        
        float centerX = _levelSettings.n_cols / 2f;
        float centerY = _levelSettings.m_rows / 2f;

        float maxDelay = 0f;

        for (int i = 0; i < _visualBlocks.Length; i++)
        {
            Block b = _visualBlocks[i];
            if (b == null) continue;

            _logic.GetCoordinates(i, out int x, out int y);
            int color = _logic.Grid[i];
            
            float distanceToCenter = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
            float delay = distanceToCenter * SHUFFLE_WAVE_DELAY; 

            if (delay > maxDelay) maxDelay = delay;

            Transform t = b.T;

            t.DOScale(SHUFFLE_SCALE_OUT, SHUFFLE_SCALE_OUT_DURATION)
             .SetDelay(delay)
             .SetEase(Ease.InBack);
             
            t.DORotate(targetRotation, SHUFFLE_SCALE_OUT_DURATION, RotateMode.FastBeyond360)
             .SetDelay(delay)
             .OnComplete(() => 
             {
                 b.UpdateVisuals(GetSpriteForGroup(color, 1));
                 
                 t.DOScale(1f, SHUFFLE_SCALE_IN_DURATION).SetEase(Ease.OutBack);
                 t.DORotate(Vector3.zero, SHUFFLE_SCALE_IN_DURATION);
                 
                 t.DOPunchScale(punchVector, SHUFFLE_PUNCH_DURATION, SHUFFLE_PUNCH_VIBRATO, SHUFFLE_PUNCH_ELASTICITY)
                  .SetDelay(SHUFFLE_SCALE_IN_DURATION * 0.8f);
             });
        }

        float totalAnimationTime = maxDelay + SHUFFLE_SCALE_OUT_DURATION + SHUFFLE_SCALE_IN_DURATION + SHUFFLE_PUNCH_DURATION;
        yield return new WaitForSeconds(totalAnimationTime);

        UpdateAllVisuals();

        if (_currentState == GameState.Shuffling)
            _currentState = GameState.WaitingForInput;
    }

    #endregion

    #region Visual Updates

    private void UpdateAllVisuals()
    {
        Array.Clear(_groupSizeCache, 0, _groupSizeCache.Length);
        Array.Clear(_groupVisited, 0, _groupVisited.Length);

        for (int i = 0; i < _logic.Grid.Length; i++)
        {
            if (!_groupVisited[i] && _logic.Grid[i] != -1)
            {
                var group = _logic.GetMatchBuffer(i);
                int groupSize = group.Count;

                for (int j = 0; j < group.Count; j++)
                {
                    int idx = group[j];
                    _groupSizeCache[idx] = groupSize;
                    _groupVisited[idx] = true;
                }
            }
        }

        for (int i = 0; i < _visualBlocks.Length; i++)
        {
            Block b = _visualBlocks[i];
            if (b != null)
            {
                int count = _groupSizeCache[i];
                int color = _logic.Grid[i];
                b.UpdateVisuals(GetSpriteForGroup(color, count));
            }
        }
    }

    private Sprite GetSpriteForGroup(int colorIndex, int groupSize)
    {
        int offset;
        if (groupSize > _levelSettings.c_threshold) offset = 3;
        else if (groupSize > _levelSettings.b_threshold) offset = 2;
        else if (groupSize > _levelSettings.a_threshold) offset = 1;
        else offset = 0;

        int finalIndex = (colorIndex * 4) + offset;

        if (finalIndex >= 0 && finalIndex < _levelSettings.blockIcons.Length)
            return _levelSettings.blockIcons[finalIndex];

        return null;
    }

    #endregion
}