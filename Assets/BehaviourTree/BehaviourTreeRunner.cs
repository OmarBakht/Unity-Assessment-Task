using UnityEngine;

/// <summary>
/// MonoBehaviour that owns the BehaviourTree and StatSheet, builds the tree,
/// writes the Blackboard each frame, and ticks the tree.
///
/// OWNERSHIP SUMMARY:
///   - This component creates the StatSheet and the BehaviourTree.
///   - It subscribes the tree to stat events via BehaviourTree.Init().
///   - It ticks the tree in Update() by passing a Blackboard by ref.
///   - It calls tree.Dispose() in OnDestroy() to prevent a subscription leak.
///
/// WHAT HAPPENS IF StatSheet IS DESTROYED WHILE TREE IS MID-TICK:
///   StatSheet is a plain C# class — it isn't a Unity Object, so it can't be
///   "destroyed" in the Unity sense. It lives as long as something holds a reference.
///   This component holds that reference. If this component is destroyed mid-tick
///   (which can't happen in normal Unity single-threaded Update — OnDestroy only fires
///   between frames), the reference remains valid for the duration of that tick.
///   If you ever move ticking to a Job or thread, you'd need a validity flag.
/// </summary>
public class BehaviourTreeRunner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _target;

    [Header("Stats")]
    [SerializeField] private float _baseHP    = 100f;
    [SerializeField] private float _baseSpeed = 3f;
    [SerializeField] private float _baseArmor = 10f;

    [Header("Combat")]
    [SerializeField] private float _attackRange = 1.5f;

    private BehaviourTree _tree;
    private StatSheet     _statSheet;
    private Blackboard    _blackboard;

    // Modifier IDs — fixed IDs so we can remove them by ID later
    private const int SlowModifierId   = 99;
    private const int DamageModifierId = 98;

    private bool _slowActive;
    private bool _damageActive;

    // ── Public accessors for UI ───────────────────────────────────────────────

    /// <summary>Read-only access to the stat sheet for the debug UI.</summary>
    public StatSheet Stats => _statSheet;

    public bool SlowActive   => _slowActive;
    public bool DamageActive => _damageActive;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _statSheet = BuildStatSheet();
        _tree      = BuildTree(_statSheet);

        _blackboard = new Blackboard
        {
            Stats       = _statSheet,
            Self        = transform,
            Target      = _target,
            AttackRange = _attackRange,
            MoveSpeed   = _baseSpeed
        };
    }

    private void Update()
    {
        if (_statSheet == null) return;

        // Keep MoveSpeed in sync with the live Speed stat so movement
        // reflects any slow/haste modifiers applied via the UI.
        _blackboard.MoveSpeed = _statSheet.GetValue(StatType.Speed);
        _blackboard.DeltaTime = Time.deltaTime;

        // Pass blackboard by ref — no copy, no allocation
        _tree.Tick(ref _blackboard);
    }

    private void OnDestroy()
    {
        // CRITICAL: prevents the subscription leak described in BehaviourTree.OnStatChanged.
        // Without this, _statSheet.OnStatChanged holds a delegate reference to _tree,
        // keeping _tree (and transitively this runner) alive in memory after destruction.
        _tree?.Dispose();
    }

    // ── Public modifier API (called by EnemyDebugUI) ──────────────────────────

    public void ToggleSlow()
    {
        if (_slowActive)
        {
            _statSheet.RemoveModifier(StatType.Speed, SlowModifierId);
            _slowActive = false;
        }
        else
        {
            _statSheet.AddModifier(StatType.Speed,
                new StatModifier(ModifierType.Multiplicative, 0.4f, SlowModifierId));
            _slowActive = true;
        }
    }

    public void ToggleDamage()
    {
        if (_damageActive)
        {
            _statSheet.RemoveModifier(StatType.HP, DamageModifierId);
            _damageActive = false;
        }
        else
        {
            _statSheet.AddModifier(StatType.HP,
                new StatModifier(ModifierType.Additive, -80f, DamageModifierId));
            _damageActive = true;
        }
    }

    // ── Tree construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds the tree matching the example from the assessment brief:
    ///
    ///   Selector [0]
    ///   ├── Sequence [1]                    (attack when healthy)
    ///   │   ├── StatThresholdDecorator [2]  (HP above 30%)
    ///   │   ├── Leaf: MoveToPlayer [3]
    ///   │   └── Leaf: Attack [4]
    ///   └── Sequence [5]                    (flee when low HP)
    ///       ├── StatThresholdDecorator [6]  (HP below 30%)
    ///       └── Leaf: RunAway [7]
    ///
    /// </summary>
    private BehaviourTree BuildTree(StatSheet statSheet)
    {
        var tree = new BehaviourTree();
        tree.Init(statSheet);

        // [0] Root Selector — children at 1, 2
        tree.AddSelector(parent: -1, firstChild: 1, childCount: 2);

        // [1] Attack Sequence — children at 3, 4
        tree.AddSequence(parent: 0, firstChild: 3, childCount: 2);

        // [2] Flee Sequence — children at 5, 6
        tree.AddSequence(parent: 0, firstChild: 5, childCount: 2);

        // [3] HP above 30 Decorator — child at 7
        tree.AddStatThresholdDecorator(parent: 1, firstChild: 7,
            stat: StatType.HP, threshold: 30f, comparison: ComparisonType.Above);

        // [4] Attack Leaf
        tree.AddLeaf(parent: 1, action: LeafType.Attack);

        // [5] HP below 30 Decorator — child at 8
        tree.AddStatThresholdDecorator(parent: 2, firstChild: 8,
            stat: StatType.HP, threshold: 30f, comparison: ComparisonType.Below);

        // [6] RunAway Leaf — direct child of Sequence [2]
        tree.AddLeaf(parent: 2, action: LeafType.RunAway);

        // [7] MoveToPlayer Leaf — child of Decorator [3]
        tree.AddLeaf(parent: 3, action: LeafType.MoveToPlayer);

        // [8] RunAway Leaf — child of Decorator [5]
        tree.AddLeaf(parent: 5, action: LeafType.RunAway);

        return tree;
    }

    private StatSheet BuildStatSheet()
    {
        var sheet = new StatSheet();
        sheet.AddBaseStat(StatType.HP,    _baseHP);
        sheet.AddBaseStat(StatType.Speed, _baseSpeed);
        sheet.AddBaseStat(StatType.Armor, _baseArmor);
        return sheet;
    }
}
