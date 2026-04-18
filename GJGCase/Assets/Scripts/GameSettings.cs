using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Game/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Animation Settings")]
    [Range(0.5f, 5f), Tooltip("Block fall speed multiplier (higher = faster)")]
    public float fallSpeed = 1.5f;

    [Header("Delays")]
    public float waitShort = 0.1f;
    public float waitShuffle = 0.5f;
    public float waitShuffleScale = 0.4f;
    public float waitShufflePunch = 0.2f;
}