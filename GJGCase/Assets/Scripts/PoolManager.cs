using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    [SerializeField] private Block _blockPrefab;
    [SerializeField] private int _initialPoolSize = 100;
    [SerializeField] private Transform _poolContainer;

    private ObjectPool<Block> _pool;

    private void Awake()
    {
        InitializePool();
    }

    private void InitializePool()
    {

        _pool = new ObjectPool<Block>(
            createFunc: () => Instantiate(_blockPrefab, _poolContainer),
            actionOnGet: (block) =>
            {
                block.gameObject.SetActive(true);
                block.OnSpawned();
            },
            actionOnRelease: (block) =>
            {
                block.OnDespawned();
                block.gameObject.SetActive(false);
                block.transform.SetParent(_poolContainer);
            },
            actionOnDestroy: (block) => Destroy(block.gameObject),
            collectionCheck: false,
            defaultCapacity: _initialPoolSize,
            maxSize: 1000
        );

        var prewarmList = new List<Block>(_initialPoolSize);
        for (int i = 0; i < _initialPoolSize; i++)
        {
            prewarmList.Add(_pool.Get());
        }
        for (int i = 0; i < _initialPoolSize; i++)
        {
            _pool.Release(prewarmList[i]);
        }
    }

    public Block GetBlock() => _pool.Get();
    public void ReleaseBlock(Block block) => _pool.Release(block);
}