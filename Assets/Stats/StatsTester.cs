using UnityEngine;

public class StatTester : MonoBehaviour
{
    private StatSheet stats;

    private void Start()
    {
        stats = new();
        stats.OnStatChanged += LogStat;

        stats.AddBaseStat(StatType.HP, 100);
        stats.AddBaseStat(StatType.Speed, 10);
        stats.AddBaseStat(StatType.Armor, 5);

        Debug.Log("Initial Stats:");

        stats.AddModifier(
            StatType.HP,
            new StatModifier(
                ModifierType.Additive,
                20,
                1));


        Debug.Log("Stats after additive modifiers:");

        stats.AddModifier(
            StatType.HP,
            new StatModifier(
                ModifierType.Multiplicative,
                1.1f,
                2));

        Debug.Log("Stats after both modifiers:");

        stats.RemoveModifier(
            StatType.HP,
            1);

        Debug.Log("Stats after removing additive modifier:");

        stats.RemoveModifier(
            StatType.HP,
            2);

        Debug.Log("Stats after removing both modifier:");
    }
    private void OnDestroy()
    {
        stats.OnStatChanged -= LogStat;
    }
    private void LogStat(StatType type, float value)
    {
        Debug.Log($"{type}: {value}");
    }
}