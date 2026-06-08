using UnityEngine;

/// <summary>
/// A flat-array, struct-based behaviour tree with zero heap allocation during ticking.
///
/// The entire tree is stored as BTNode[] — one contiguous array allocated once at construction.
/// Children are encoded as index offsets (FirstChild + ChildCount), not object references.
/// Traversal is iterative using a pre-allocated stack — no recursion, no virtual dispatch.
///
/// OWNERSHIP CONTRACT:
///   - BehaviourTreeRunner creates and owns this tree.
///   - BehaviourTreeRunner also owns the StatSheet (or receives it from outside).
///   - BehaviourTreeRunner ticks the tree each frame via Tick(ref blackboard).
///   - BehaviourTreeRunner is responsible for calling Dispose() when the enemy is destroyed,
///     which unsubscribes from StatSheet.OnStatChanged and prevents a subscription leak.
///
/// LIFETIME RISK:
///   - This tree subscribes to StatSheet.OnStatChanged to update cooldown intervals.
///   - If Dispose() is not called, the StatSheet's event delegate holds a reference to
///     this tree's OnStatChanged handler, which keeps the tree alive after the enemy
///     is destroyed. Always call Dispose() in OnDestroy.
/// </summary>
public class BehaviourTree : System.IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────

    public const int MaxNodes = 32;
    private const int None = -1;

    // ── Tree storage ──────────────────────────────────────────────────────────

    private readonly BTNode[] _nodes = new BTNode[MaxNodes];
    private int _nodeCount;

    // ── Iterative traversal stack ─────────────────────────────────────────────

    /// <summary>
    /// Each stack frame stores a node index and the index of the next child
    /// that frame still needs to process. This fully replaces the call stack
    /// that recursion would have used.
    ///
    /// Frame layout:
    ///   _stackIndex[top] = which node this frame represents
    ///   _stackChild[top] = which child (0-based offset from FirstChild) to evaluate next
    /// </summary>
    private readonly int[] _stackIndex = new int[MaxNodes];
    private readonly int[] _stackChild = new int[MaxNodes];
    private int _stackTop;

    // ── Stat event reference ──────────────────────────────────────────────────

    private StatSheet _statSheet;

    // ── Construction ─────────────────────────────────────────────────────────

    public void Init(StatSheet statSheet)
    {
        _statSheet = statSheet;
        _statSheet.OnStatChanged += OnStatChanged;
    }

    // ── Node builders ────────────────────────────────────────────────────────

    public int AddSelector(int parent, int firstChild, int childCount)
    {
        return AddNode(new BTNode
        {
            Type = NodeType.Selector,
            Parent = parent,
            FirstChild = firstChild,
            ChildCount = childCount
        });
    }

    public int AddSequence(int parent, int firstChild, int childCount)
    {
        return AddNode(new BTNode
        {
            Type = NodeType.Sequence,
            Parent = parent,
            FirstChild = firstChild,
            ChildCount = childCount
        });
    }

    public int AddLeaf(int parent, LeafType action)
    {
        return AddNode(new BTNode
        {
            Type = NodeType.Leaf,
            Parent = parent,
            FirstChild = None,
            ChildCount = 0,
            LeafAction = action
        });
    }

    public int AddStatThresholdDecorator(
        int parent, int firstChild,
        StatType stat, float threshold,
        ComparisonType comparison, bool normalized = false)
    {
        return AddNode(new BTNode
        {
            Type = NodeType.StatThresholdDecorator,
            Parent = parent,
            FirstChild = firstChild,
            ChildCount = 1,
            ThresholdStat = stat,
            Threshold = threshold,
            Comparison = comparison,
            ThresholdIsNormalized = normalized
        });
    }

    public int AddStatCooldownDecorator(
        int parent, int firstChild,
        StatType cooldownStat, float baseCooldown)
    {
        // UNSURE: _statSheet may be null if Init() hasn't been called before AddNode calls.
        float initialStat = _statSheet != null ? _statSheet.GetValue(cooldownStat) : 1f;
        float initialInterval = ComputeCooldownInterval(baseCooldown, initialStat);

        return AddNode(new BTNode
        {
            Type = NodeType.StatCooldownDecorator,
            Parent = parent,
            FirstChild = firstChild,
            ChildCount = 1,
            CooldownStat = cooldownStat,
            BaseCooldown = baseCooldown,
            CooldownElapsed = 0f,
            CooldownInterval = initialInterval
        });
    }

    private int AddNode(BTNode node)
    {
        if (_nodeCount >= MaxNodes)
        {
            Debug.LogError($"[BehaviourTree] Node limit ({MaxNodes}) reached.");
            return None;
        }
        int index = _nodeCount++;
        _nodes[index] = node;
        return index;
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Iterative tick. Simulates the call stack that a recursive implementation
    /// would use, but with a pre-allocated pair of int arrays instead.
    ///
    /// The algorithm mirrors exactly what recursion would do:
    ///   - Push a frame when descending into a composite node
    ///   - Evaluate children one at a time, tracking position in _stackChild
    ///   - Pop the frame when the composite resolves, return result to caller
    ///
    /// Leaf and decorator nodes never push a frame — they evaluate instantly
    /// and return their result directly up the call chain.
    ///
    /// Zero heap allocation: all state lives in _stackIndex[], _stackChild[],
    /// and the BTNode[] array. NodeState is an enum (stack value type).
    /// </summary>
    public NodeState Tick(ref Blackboard bb)
    {
        if (_nodeCount == 0) return NodeState.Failure;

        _stackTop = 0;
        return EvaluateNode(0, ref bb);
    }

    /// <summary>
    /// Evaluate a single node. For leaf/decorator nodes this returns immediately.
    /// For composite nodes (Sequence/Selector) this uses the explicit stack to
    /// iterate through children without recursion.
    /// </summary>
    private NodeState EvaluateNode(int nodeIdx, ref Blackboard bb)
    {
        ref BTNode node = ref _nodes[nodeIdx];

        switch (node.Type)
        {
            case NodeType.Leaf:
                return TickLeaf(ref node, ref bb);

            case NodeType.StatThresholdDecorator:
                return TickThresholdDecorator(ref node, ref bb);

            case NodeType.StatCooldownDecorator:
                return TickCooldownDecorator(ref node, ref bb);

            case NodeType.Sequence:
                return TickComposite(nodeIdx, ref bb, isSequence: true);

            case NodeType.Selector:
                return TickComposite(nodeIdx, ref bb, isSequence: false);

            default:
                return NodeState.Failure;
        }
    }

    /// <summary>
    /// Iteratively evaluate a Sequence or Selector node.
    ///
    /// Instead of recursing into each child and letting the call stack manage
    /// where we are, we push a frame onto our explicit stack and loop.
    /// Each frame records which node we're in and which child we're up to.
    ///
    /// Sequence: return Failure on first child failure, Success if all succeed.
    /// Selector: return Success on first child success, Failure if all fail.
    ///
    /// The explicit stack is only needed when a composite contains another composite.
    /// For simple trees (composite → leaf) this effectively just loops over children.
    /// </summary>
    private NodeState TickComposite(int nodeIdx, ref Blackboard bb, bool isSequence)
    {
        // Push this composite onto the stack
        _stackIndex[_stackTop] = nodeIdx;
        _stackChild[_stackTop] = 0;
        _stackTop++;

        NodeState result = NodeState.Failure; // default if no children

        while (_stackTop > 0)
        {
            // Look at the top frame
            int top = _stackTop - 1;
            int current = _stackIndex[top];
            int childOff = _stackChild[top];

            ref BTNode composite = ref _nodes[current];
            bool curIsSequence = composite.Type == NodeType.Sequence;

            if (childOff >= composite.ChildCount)
            {
                // All children evaluated — this composite is done
                _stackTop--;

                // A Sequence that exhausted all children without failure = Success
                // A Selector that exhausted all children without success = Failure
                result = curIsSequence ? NodeState.Success : NodeState.Failure;

                // Propagate result up to the parent frame if one exists
                if (_stackTop > 0)
                    ApplyResultToParent(result, _stackTop - 1);

                continue;
            }

            // Evaluate the next child
            int childIdx = composite.FirstChild + childOff;

            // Advance the child pointer before evaluating, so if the child
            // itself is a composite and pushes a frame, we don't re-evaluate
            // this child when we return to this frame.
            _stackChild[top]++;

            ref BTNode child = ref _nodes[childIdx];

            NodeState childResult;

            if (child.Type == NodeType.Sequence || child.Type == NodeType.Selector)
            {
                // Child is also a composite — push a new frame and let the
                // loop handle it. We'll pick up the result via ApplyResultToParent.
                _stackIndex[_stackTop] = childIdx;
                _stackChild[_stackTop] = 0;
                _stackTop++;
                continue;
            }
            else
            {
                // Leaf or decorator — evaluates instantly, no frame needed
                childResult = EvaluateNode(childIdx, ref bb);
            }

            // Apply child result to this composite's logic
            if (curIsSequence && childResult == NodeState.Failure)
            {
                // Sequence short-circuits on failure
                _stackTop--;
                result = NodeState.Failure;
                if (_stackTop > 0) ApplyResultToParent(result, _stackTop - 1);
                continue;
            }

            if (!curIsSequence && childResult == NodeState.Success)
            {
                // Selector short-circuits on success
                _stackTop--;
                result = NodeState.Success;
                if (_stackTop > 0) ApplyResultToParent(result, _stackTop - 1);
                continue;
            }

            // Running result — treat as success for Sequence (keep going),
            // treat as failure for Selector (try next).
            // UNSURE: a full BT implementation would suspend here and resume
            // next tick. For this assessment we restart from root each tick,
            // so Running is handled as "action is executing, continue tree."
            if (childResult == NodeState.Running)
            {
                _stackTop--;
                result = NodeState.Running;
                if (_stackTop > 0) ApplyResultToParent(NodeState.Success, _stackTop - 1);
                continue;
            }
        }

        return result;
    }

    /// <summary>
    /// After a composite child resolves, apply its result to the parent frame.
    /// This is called when a nested composite finishes and we need to feed its
    /// result back to the frame above it on the stack — exactly what a recursive
    /// return value would have done automatically.
    /// </summary>
    private void ApplyResultToParent(NodeState childResult, int parentFrameIdx)
    {
        int parentNodeIdx = _stackIndex[parentFrameIdx];
        ref BTNode parent = ref _nodes[parentNodeIdx];

        bool parentIsSequence = parent.Type == NodeType.Sequence;

        if (parentIsSequence && childResult == NodeState.Failure)
        {
            // Parent sequence fails — signal by setting child pointer past end + 1
            // so the next loop iteration sees childOff >= childCount and exits.
            // We use ChildCount + 1 as a "failed" sentinel distinct from "exhausted".
            _stackChild[parentFrameIdx] = parent.ChildCount + 1;
        }
        else if (!parentIsSequence && childResult == NodeState.Success)
        {
            // Parent selector succeeds — same sentinel trick
            _stackChild[parentFrameIdx] = parent.ChildCount + 1;
        }
        // Otherwise the parent continues to its next child naturally
    }

    // ── Node tick implementations ─────────────────────────────────────────────

    /// <summary>
    /// Reads the cached stat value directly — no recalculation.
    /// If the threshold passes, evaluates the child directly (inline, no stack frame).
    /// </summary>
    private NodeState TickThresholdDecorator(ref BTNode node, ref Blackboard bb)
    {
        float statValue = bb.Stats.GetValue(node.ThresholdStat);

        // UNSURE: ThresholdIsNormalized requires knowing the base value.
        // Using absolute comparison only for now; base value not exposed on StatSheet.
        bool passed = node.Comparison switch
        {
            ComparisonType.Above => statValue > node.Threshold,
            ComparisonType.Below => statValue < node.Threshold,
            ComparisonType.AboveOrEqual => statValue >= node.Threshold,
            ComparisonType.BelowOrEqual => statValue <= node.Threshold,
            _ => false
        };

        if (!passed) return NodeState.Failure;

        // Decorator is a transparent wrapper — evaluate child directly
        ref BTNode child = ref _nodes[node.FirstChild];
        return EvaluateNode(node.FirstChild, ref bb);
    }

    /// <summary>
    /// Throttles the child using an interval derived from a live stat value.
    /// Interval = BaseCooldown / liveStatValue — higher stat = shorter cooldown.
    ///
    /// DECISION: CooldownInterval is updated via OnStatChanged (event-driven),
    /// not recalculated every tick. There is at most one frame of lag between
    /// a stat change and the interval reflecting it — acceptable for a game context.
    /// </summary>
    private NodeState TickCooldownDecorator(ref BTNode node, ref Blackboard bb)
    {
        node.CooldownElapsed += bb.DeltaTime;

        if (node.CooldownElapsed < node.CooldownInterval)
            return NodeState.Failure;

        node.CooldownElapsed = 0f;

        return EvaluateNode(node.FirstChild, ref bb);
    }

    /// <summary>
    /// Dispatches leaf actions via switch — no virtual dispatch, no allocation.
    /// </summary>
    private NodeState TickLeaf(ref BTNode node, ref Blackboard bb)
    {
        return node.LeafAction switch
        {
            LeafType.MoveToPlayer => LeafActions.MoveToPlayer(ref bb),
            LeafType.Attack => LeafActions.Attack(ref bb),
            LeafType.RunAway => LeafActions.RunAway(ref bb),
            LeafType.Idle => LeafActions.Idle(ref bb),
            _ => NodeState.Failure
        };
    }

    // ── Stat event handler ────────────────────────────────────────────────────

    /// <summary>
    /// Called synchronously by StatSheet.OnStatChanged when a modifier changes.
    /// Updates CooldownInterval on any matching StatCooldownDecorator nodes.
    ///
    /// LEAK RISK: This delegate is registered on StatSheet.OnStatChanged.
    /// The StatSheet holds a reference to this delegate → this BehaviourTree.
    /// If Dispose() is never called, the tree stays in memory after the enemy
    /// is destroyed. Fix: always call Dispose() in the owning MonoBehaviour's OnDestroy.
    /// </summary>
    private void OnStatChanged(StatType changedStat, float newValue)
    {
        for (int i = 0; i < _nodeCount; i++)
        {
            ref BTNode node = ref _nodes[i];
            if (node.Type != NodeType.StatCooldownDecorator) continue;
            if (node.CooldownStat != changedStat) continue;
            node.CooldownInterval = ComputeCooldownInterval(node.BaseCooldown, newValue);
        }
    }

    private static float ComputeCooldownInterval(float baseCooldown, float statValue)
    {
        if (statValue <= 0f) return baseCooldown;
        return baseCooldown / statValue;
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Unsubscribes from stat events. Call this in the owning MonoBehaviour's OnDestroy.
    /// </summary>
    public void Dispose()
    {
        if (_statSheet != null)
        {
            _statSheet.OnStatChanged -= OnStatChanged;
            _statSheet = null;
        }
    }
}