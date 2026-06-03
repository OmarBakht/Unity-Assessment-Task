using System.Collections.Generic;

public class StatTypeComparer : IEqualityComparer<StatType>
{
    public bool Equals(StatType x, StatType y)
    {
        return x == y;
    }

    public int GetHashCode(StatType obj)
    {
        return (int)obj;
    }
}