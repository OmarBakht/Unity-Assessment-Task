using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen debug panel showing live stat values, active tree branch,
/// and buttons to apply/remove modifiers.
///
/// Attach to the Canvas GameObject. Wire up references in the Inspector.
///
/// All UI updates are driven by polling in Update() — acceptable for a debug panel.
/// The BT itself does not poll; it reacts to events. This UI is separate from
/// the zero-alloc tick path.
/// </summary>
public class EnemyDebugUI : MonoBehaviour
{
    [Header("Scene reference")]
    [SerializeField] private BehaviourTreeRunner _runner;

    [Header("Stat text labels")]
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _speedText;
    [SerializeField] private TextMeshProUGUI _armorText;

    [Header("State text")]
    [SerializeField] private TextMeshProUGUI _branchText;   // which BT branch is active
    [SerializeField] private TextMeshProUGUI _hintText;     // drag hint

    [Header("Buttons")]
    [SerializeField] private Button _slowButton;
    [SerializeField] private Button _damageButton;

    [Header("Button labels (Text children of each button)")]
    [SerializeField] private TextMeshProUGUI _slowButtonText;
    [SerializeField] private TextMeshProUGUI _damageButtonText;

    private void Start()
    {
        if (_slowButton  != null) _slowButton.onClick.AddListener(OnSlowClicked);
        if (_damageButton != null) _damageButton.onClick.AddListener(OnDamageClicked);
        if (_hintText    != null) _hintText.text = "Click and drag the blue circle to move the player";
    }

    private void Update()
    {
        if (_runner == null || _runner.Stats == null) return;

        StatSheet stats = _runner.Stats;

        float hp    = stats.GetValue(StatType.HP);
        float speed = stats.GetValue(StatType.Speed);
        float armor = stats.GetValue(StatType.Armor);

        if (_hpText    != null) _hpText.text    = $"HP:    {hp:F1}";
        if (_speedText != null) _speedText.text = $"Speed: {speed:F2}";
        if (_armorText != null) _armorText.text = $"Armor: {armor:F1}";

        // Determine which branch the tree is taking based on HP threshold
        if (_branchText != null)
        {
            if (hp > 30f)
                _branchText.text = "Branch: ATTACK\n(HP above 30 — chasing player)";
            else
                _branchText.text = "Branch: FLEE\n(HP below 30 — running away)";

            _branchText.color = hp > 30f
                ? new Color(0.2f, 0.85f, 0.3f)
                : new Color(0.9f, 0.2f, 0.2f);
        }

        // Update button labels to reflect toggle state
        if (_slowButtonText  != null)
            _slowButtonText.text  = _runner.SlowActive   ? "Remove Slow"   : "Apply Slow (Speed x0.4)";
        if (_damageButtonText != null)
            _damageButtonText.text = _runner.DamageActive ? "Restore HP"    : "Deal 80 Damage";
    }

    private void OnSlowClicked()
    {
        _runner.ToggleSlow();
    }

    private void OnDamageClicked()
    {
        _runner.ToggleDamage();
    }
}
