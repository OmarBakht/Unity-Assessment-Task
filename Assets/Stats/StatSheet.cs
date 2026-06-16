using System;
using System.Collections.Generic;
using UnityEngine;

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

        float newMaxVal = (stat.BaseValue * multiplier) + additive;

        if(stat.IsPool)
        {
            stat.CurrentValue = MathF.Min(stat.CurrentValue,newMaxVal);
        }

        stat.CachedValue = newMaxVal;

        stat.IsDirty = false;
    }

    public bool AddModifier( StatType statType, StatModifier modifier)
    {
        Stat stat = _stats[statType];

        if (stat.ModifierCount >= Stat.MaxModifiers) // overflow policy: toss out new modifer.
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

    public float GetCurrentValue(StatType statType)
    {
        return _stats[statType].CurrentValue;
    }

    public void ApplyDelta(StatType statType, float delta)
    {
        if (_stats[statType].IsPool == false)
        {
            return; 
        }

        Stat stat = _stats[statType];
        float maxVal = GetValue(statType);

        stat.CurrentValue = Mathf.Clamp(stat.CurrentValue + delta, 0f, maxVal);
        OnStatChanged?.Invoke(statType, stat.CurrentValue);
    }

    public void InitPool(StatType statType)
    {
        Stat stat = _stats[statType];
        stat.IsPool = true;
        stat.CurrentValue = GetValue(statType);
    }
    public bool IsPool(StatType statType) 
    { 
        return _stats[statType].IsPool; 
    }
}