using UnityEngine;

/// <summary>
/// Static implementations of all leaf node actions.
/// Static methods — no instance, no allocation.
/// All context needed comes from the Blackboard passed by ref.
/// Updated for 2D: movement is clamped to the XY plane (z stays 0).
/// </summary>
public static class LeafActions
{
    public static NodeState MoveToPlayer(ref Blackboard bb)
    {
        if (bb.Self == null || bb.Target == null)
            return NodeState.Failure;

        // 2D distance — ignore Z axis
        Vector2 selfPos   = bb.Self.position;
        Vector2 targetPos = bb.Target.position;
        float   distance  = Vector2.Distance(selfPos, targetPos);

        if (distance <= bb.AttackRange)
            return NodeState.Success; // already in range

        Vector2 direction = (targetPos - selfPos).normalized;

        // Move in XY only — keep Z at 0 for 2D scene
        bb.Self.position += (Vector3)(direction * bb.MoveSpeed * bb.DeltaTime);

        return NodeState.Running;
    }

    public static NodeState Attack(ref Blackboard bb)
    {
        //Debug.Log("ATTACK!");
        if (bb.Self == null || bb.Target == null)
            return NodeState.Failure;

        Vector2 selfPos   = bb.Self.position;
        Vector2 targetPos = bb.Target.position;
        float   distance  = Vector2.Distance(selfPos, targetPos);

        if (distance > bb.AttackRange)
            return NodeState.Failure;

        // UNSURE: in a real game this would trigger animation + damage.
        // Logging removed from hot path — state is shown via EnemyDebugUI instead.
        return NodeState.Success;
    }

    public static NodeState RunAway(ref Blackboard bb)
    {
        if (bb.Self == null || bb.Target == null)
            return NodeState.Failure;

        Vector2 selfPos   = bb.Self.position;
        Vector2 targetPos = bb.Target.position;
        Vector2 flee      = (selfPos - targetPos).normalized;

        bb.Self.position += (Vector3)(flee * bb.MoveSpeed * bb.DeltaTime);

        return NodeState.Success;
    }

    public static NodeState Idle(ref Blackboard bb)
    {
        return NodeState.Success;
    }

    public static NodeState Regen(ref Blackboard bb)
    {
        Debug.Log("REGEN");
        bb.RegenAccumulator += bb.DeltaTime;

        if(bb.RegenAccumulator >= 1f)
        {
            bb.RegenAccumulator = 0f;
            float regenAmount = bb.Stats.GetValue(StatType.RegenRate);
            bb.Stats.ApplyDelta(StatType.HP, regenAmount);
        }

        return NodeState.Success;
    }
}
