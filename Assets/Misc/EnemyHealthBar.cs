using UnityEngine;

/// <summary>
/// Draws a health bar above the enemy using two child GameObjects:
/// a grey background quad and a green/red foreground quad that scales with HP.
///
/// Attach this to the Enemy GameObject.
/// It finds the BehaviourTreeRunner on the same object to read HP each frame.
///
/// No allocation in Update — just reading a float and setting a Vector3 scale.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Bar GameObjects (assign in Inspector)")]
    [SerializeField] private Transform _barFill;       // the coloured foreground quad
    [SerializeField] private Transform _barBackground; // the grey background quad

    [Header("Settings")]
    [SerializeField] private float _maxHP        = 100f;
    [SerializeField] private Vector3 _offset     = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float _barWidth     = 1f;
    [SerializeField] private float _barHeight    = 0.15f;

    private BehaviourTreeRunner _runner;
    private SpriteRenderer      _fillRenderer;

    private static readonly Color HealthyColour = new Color(0.2f, 0.85f, 0.3f);
    private static readonly Color LowColour     = new Color(0.9f, 0.2f, 0.2f);

    private void Awake()
    {
        _runner       = GetComponent<BehaviourTreeRunner>();
        _fillRenderer = _barFill.GetComponent<SpriteRenderer>();

        // Position the background bar relative to the enemy
        _barBackground.localPosition = _offset;
        _barBackground.localScale    = new Vector3(_barWidth, _barHeight, 1f);

        _barFill.localPosition = _offset;
    }

    private void Update()
    {
        if (_runner == null || _runner.Stats == null) return;

        float hp      = _runner.Stats.GetCurrentValue(StatType.HP);
        float ratio   = Mathf.Clamp01(hp / _maxHP);

        // Scale fill width from the left edge
        // We shift position by half the lost width so it shrinks left-to-right
        float fillWidth = _barWidth * ratio;
        _barFill.localScale    = new Vector3(fillWidth, _barHeight, 1f);
        _barFill.localPosition = _offset + new Vector3(-(_barWidth - fillWidth) * 0.5f, 0f, 0f);

        // Colour: green above 30%, red below
        if (_fillRenderer != null)
            _fillRenderer.color = ratio > 0.3f ? HealthyColour : LowColour;
    }
}
