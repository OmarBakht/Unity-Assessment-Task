/// <summary>
/// node in the flat-array behaviour tree.
/// union struct - every node type shares the same struct.
/// if a NodeType dosent have the field, it is left unsed/zero. like in beshna package data objects
/// 
/// Layout example for a Sequence node:
///     Type          = NodeType.Sequence
///     FirstChild    = 3         (tree[3] is the first child)
///     ChildCount    = 3         (children occupy tree[3], tree[4], tree[5])
///     Parent        = 0         (tree[0] is this node's parent)
///     CurrentChild  = 0         (Sequence tracks which child it's evaluating)
///     (all decorator fields are unused / zero)
/// </summary>
public struct BTNode
{
    /// ------------ Tree structure fields (all node types) ------------

    public NodeType Type;

    /// <summary>
    /// Index into the flat array where this node's children begin.
    /// Set to -1 for leaf nodes (no children).
    /// </summary>
    public int FirstChild;

    /// <summary>
    /// How many consecutive slots starting at FirstChild are children.
    /// </summary>
    public int ChildCount;

    /// <summary>
    /// Index of this node's parent. -1 for the root.
    /// </summary>
    public int Parent;

    // ------------ Runtime traversal state ------------

    /// <summary>
    /// Sequence/Selector: index of the child currently being evaluated.
    /// Reset to 0 at the start of each tick pass through this node.
    /// </summary>
    public int CurrentChild;

    // ------------ Leaf-specific fields ------------

    /// <summary>
    /// Which action this leaf performs. Ignored for non-leaf nodes.
    /// </summary>
    public LeafType LeafAction;

    // ------------ StatThresholdDecorator fields ------------

    /// <summary>
    /// Which stat to read from the blackboard's StatSheet.
    /// </summary>
    public StatType ThresholdStat;

    /// <summary>
    /// The value to compare the live stat against.
    /// </summary>
    public float Threshold;

    /// <summary>
    /// Whether the stat must be above or below the threshold to pass.
    /// </summary>
    public ComparisonType Comparison;

    /// <summary>
    /// If true, threshold is treated as a fraction of base value (e.g. 0.3 = 30% of base HP).
    /// If false, it's an absolute value comparison.
    /// </summary>
    public bool ThresholdIsNormalized;

    // ------------ StatCooldownDecorator fields ------------

    /// <summary>
    /// Which stat drives the cooldown interval. Typically Speed.
    /// </summary>
    public StatType CooldownStat;

    /// <summary>
    /// Base interval in seconds at a stat value of 1.0.
    /// Actual interval = BaseCooldown / liveStatValue  (higher speed = shorter cooldown).
    /// </summary>
    public float BaseCooldown;

    /// <summary>
    /// Elapsed time since this decorator last allowed its child to run.
    /// </summary>
    public float CooldownElapsed;

    /// <summary>
    /// The resolved interval last time we got a stat change event.
    /// Updated via OnStatChanged, not recalculated every tick.
    /// </summary>
    public float CooldownInterval;
}
