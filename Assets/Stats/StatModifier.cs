public struct StatModifier
{
    public ModifierType Type;
    public float Value;
    public int Id;

    public StatModifier( ModifierType type, float value, int id)
    {
        Type = type;
        Value = value;
        Id = id;
    }
}