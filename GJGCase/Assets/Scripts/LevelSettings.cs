using UnityEngine;

[CreateAssetMenu(fileName = "LevelSettings", menuName = "Game/LevelSettings")]
public class LevelSettings : ScriptableObject
{
    [Header("Board Dimensions")]
    [Tooltip("M: Number of Rows")]
    [Range(2, 12)] public int m_rows = 10;

    [Tooltip("N: Number of Columns")]
    [Range(2, 12)] public int n_cols = 12;

    [Header("Game Rules")]
    [Range(1, 6)] public int k_colors = 6;

    [Header("Icon Thresholds")]
    [Tooltip("If group size > A, show Icon 1")]
    public int a_threshold = 4;
    [Tooltip("If group size > B, show Icon 2")]
    public int b_threshold = 7;
    [Tooltip("If group size > C, show Icon 3")]
    public int c_threshold = 9;

    [Header("Visual Validation")]
    public Sprite[] blockIcons;
}