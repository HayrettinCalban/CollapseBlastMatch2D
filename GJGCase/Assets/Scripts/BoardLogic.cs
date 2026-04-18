using System;
using System.Collections.Generic;

public class BoardLogic
{
    public int[] Grid { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public event Action<List<int>> OnBlocksMatched;
    public event Action OnGravityApplied;
    public event Action OnBoardRefilled;
    public event Action OnBoardShuffled;

    private readonly int _colorCount;
    private readonly bool[] _visited;
    private readonly Queue<int> _bfsQueue;
    private readonly List<int> _currentMatchBuffer;
    private readonly List<int> _shuffleBuffer;

    private readonly System.Random _random;

    public BoardLogic(int width, int height, int colorCount, int? seed = null)
    {
        Width = width;
        Height = height;
        _colorCount = colorCount;

        int totalCells = Width * Height;
        Grid = new int[totalCells];
        _visited = new bool[totalCells];

        _bfsQueue = new Queue<int>(totalCells);
        _currentMatchBuffer = new List<int>(totalCells);
        _shuffleBuffer = new List<int>(totalCells);

        _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        FillBoardRandomly();
    }

    private void FillBoardRandomly()
    {
        for (int i = 0; i < Grid.Length; i++)
        {
            Grid[i] = _random.Next(0, _colorCount);
        }
    }

    public int GetIndex(int x, int y) => y * Width + x;

    public void GetCoordinates(int index, out int x, out int y)
    {
        x = index % Width;
        y = index / Width;
    }

    public List<int> GetMatchBuffer(int index)
    {
        int targetColor = Grid[index];
        _currentMatchBuffer.Clear();

        if (targetColor == -1) return _currentMatchBuffer;

        Array.Clear(_visited, 0, _visited.Length);
        _bfsQueue.Clear();

        _bfsQueue.Enqueue(index);
        _visited[index] = true;
        _currentMatchBuffer.Add(index);

        while (_bfsQueue.Count > 0)
        {
            int current = _bfsQueue.Dequeue();
            GetCoordinates(current, out int cx, out int cy);

            CheckNeighbor(cx + 1, cy, targetColor);
            CheckNeighbor(cx - 1, cy, targetColor);
            CheckNeighbor(cx, cy + 1, targetColor);
            CheckNeighbor(cx, cy - 1, targetColor);
        }

        return _currentMatchBuffer;
    }

    private void CheckNeighbor(int checkX, int checkY, int targetColor)
    {
        if (checkX >= 0 && checkX < Width && checkY >= 0 && checkY < Height)
        {
            int idx = GetIndex(checkX, checkY);
            if (!_visited[idx] && Grid[idx] == targetColor)
            {
                _visited[idx] = true;
                _bfsQueue.Enqueue(idx);
                _currentMatchBuffer.Add(idx);
            }
        }
    }

    public void RemoveBlocks(List<int> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Grid[blocks[i]] = -1;
        }
        OnBlocksMatched?.Invoke(blocks);
    }

    public void ApplyGravity()
    {
        for (int x = 0; x < Width; x++)
        {
            int writeRow = 0;
            for (int y = 0; y < Height; y++)
            {
                int readIdx = GetIndex(x, y);
                if (Grid[readIdx] != -1)
                {
                    if (writeRow != y)
                    {
                        int writeIdx = GetIndex(x, writeRow);
                        Grid[writeIdx] = Grid[readIdx];
                        Grid[readIdx] = -1;
                    }
                    writeRow++;
                }
            }
        }
        OnGravityApplied?.Invoke();
    }

    public void RefillBoard()
    {
        for (int i = 0; i < Grid.Length; i++)
        {
            if (Grid[i] == -1)
            {
                Grid[i] = _random.Next(0, _colorCount);
            }
        }
        OnBoardRefilled?.Invoke();
    }

    public bool IsDeadlocked()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = GetIndex(x, y);
                int color = Grid[index];
                if (color == -1) continue;

                if (x < Width - 1 && Grid[GetIndex(x + 1, y)] == color) return false;
                if (y < Height - 1 && Grid[GetIndex(x, y + 1)] == color) return false;
            }
        }
        return true;
    }

    public void SmartShuffle()
    {
        _shuffleBuffer.Clear();
        for (int i = 0; i < Grid.Length; i++)
        {
            _shuffleBuffer.Add(Grid[i]);
        }

        for (int i = _shuffleBuffer.Count - 1; i > 0; i--)
        {
            int j = _random.Next(0, i + 1);
            (_shuffleBuffer[i], _shuffleBuffer[j]) = (_shuffleBuffer[j], _shuffleBuffer[i]);
        }

        for (int i = 0; i < Grid.Length; i++)
        {
            Grid[i] = _shuffleBuffer[i];
        }

        if (IsDeadlocked())
        {
            int rx = _random.Next(0, Width - 1);
            int ry = _random.Next(0, Height - 1);
            int idx1 = GetIndex(rx, ry);
            int idx2 = GetIndex(rx + 1, ry);

            Grid[idx2] = Grid[idx1];
        }

        OnBoardShuffled?.Invoke();
    }
}