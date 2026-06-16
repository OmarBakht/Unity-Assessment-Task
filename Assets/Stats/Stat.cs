public class Stat
{
    public const int MaxModifiers = 8;

    public float BaseValue;
    public float CachedValue;
    public float CurrentValue;

    public bool IsDirty;
    public bool IsPool;

    public readonly StatModifier[] Modifiers = new StatModifier[MaxModifiers];

    public int ModifierCount;

    public Stat(float baseValue)
    {
        BaseValue = baseValue;
        CachedValue = baseValue;
        IsDirty = false;
    }
}