using System;
using System.Collections.Generic;

public class StatSheet
{
    public event Action<StatType, float> OnStatChanged;

    private readonly Dictionary<StatType, Stat> _stats;

    public StatSheet()
    {
        _stats = new Dictionary<StatType, Stat>(new StatTypeComparer());
    }

    public void AddBaseStat(StatType type, float baseValue)
    {
        _stats[type] = new Stat(baseValue);
    }

    public float GetValue(StatType statType)
    {
        Stat stat = _stats[statType];

        if (stat.IsDirty)
        {
            Recalculate(stat);
        }

        return stat.CachedValue;
    }

    private void Recalculate(Stat stat)
    {
        float additive = 0f;
        float multiplier = 1f;

        for (int i = 0; i < stat.ModifierCount; i++)
        {
            ref StatModifier modifier = ref stat.Modifiers[i];

            if (modifier.Type == ModifierType.Additive)
            {
                additive += modifier.Value;
            }
            else if (modifier.Type == ModifierType.Multiplicative)
            {
                multiplier *= modifier.Value;
            }
        }

        stat.CachedValue = (stat.BaseValue * multiplier) + additive;

        stat.IsDirty = false;
    }

    public bool AddModifier( StatType statType, StatModifier modifier)
    {
        Stat stat = _stats[statType];

        if (stat.ModifierCount >= Stat.MaxModifiers) // DONKEY: handler modifier overflow policy
        {
            return false;
        }

        stat.Modifiers[stat.ModifierCount++] = modifier;

        stat.IsDirty = true;

        OnStatChanged?.Invoke(statType, GetValue(statType));

        return true;
    }

    public bool RemoveModifier( StatType statType, int modifierId)
    {
        Stat stat = _stats[statType];

        for (int i = 0; i < stat.ModifierCount; i++)
        {
            if (stat.Modifiers[i].Id != modifierId)
                continue;

            int lastIndex = --stat.ModifierCount;

            stat.Modifiers[i] = stat.Modifiers[lastIndex];

            stat.IsDirty = true;

            OnStatChanged?.Invoke(statType, GetValue(statType));

            return true;
        }

        return false;
    }
}