using UnityEngine;

/// <summary>
/// Passed by ref into every node tick. Carries all shared context the tree needs.
/// 
/// This is a struct so passing it costs nothing on the heap.
/// The ref keyword in the tick signature ensures no copy is made either —
/// every node reads from the exact same memory location.
/// 
/// Ownership: the BehaviourTreeRunner MonoBehaviour owns the Blackboard.
/// It writes to it each frame before calling Tick(), then passes it by ref.
/// The tree never stores the blackboard - it only reads during the tick.
/// </summary>
public struct Blackboard
{
    /// <summary>
    /// Reference to the statsheet.
    /// The BT reads cached values from this - it does not recalculate anything.
    /// 
    /// UNSURE: Storing a class reference inside a struct is fine in C# (the reference
    /// itself lives on the stack as part of the struct), but it means the struct is
    /// not truly blittable. This should be fine for our purposes.
    /// </summary>
    public StatSheet Stats;

    /// <summary>
    /// The Transform of the enemy this tree is running on.
    /// </summary>
    public Transform Self;

    /// <summary>
    /// The Transform of the player (target).
    /// </summary>
    public Transform Target;

    /// <summary>
    /// Delta time for this tick — stored here so leaf nodes don't call Time.deltaTime.
    /// </summary>
    public float DeltaTime;

    /// <summary>
    /// Attack range — used by the Attack leaf to decide success/failure.
    /// </summary>
    public float AttackRange;

    /// <summary>
    /// Movement speed cap — leaf nodes may read this when moving.
    /// </summary>
    public float MoveSpeed;

    public float RegenAccumulator;
}
