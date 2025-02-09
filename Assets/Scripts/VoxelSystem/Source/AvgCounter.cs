using System.Collections.Generic;

public static class AvgCounter
{

    private static Dictionary<string, Counter> kvp = new Dictionary<string, Counter>();

    public static void AddCounter(string name)
    {
        kvp.TryAdd(name, new Counter());
    }

    public static void UpdateCounter(string name, float time)
    {
        if (kvp.TryGetValue(name, out Counter counter))
        {
            counter.Add(time);
        }
        else
        {
            kvp.TryAdd(name, new Counter());
        }
    }

    public static Counter GetCounter(string name)
    {
        kvp.TryGetValue(name, out Counter counter);
        return counter;
    }

    public static void RemoveCounter(string name)
    {
        kvp.Remove(name);
    }
}
public class Counter
{
    public float Time, MinTime, MaxTime;
    private float TotalTime;
    private int Count;
    public float AVG 
    {  
        get
        {
            if(Count == 0)
                return 0;
            return TotalTime / Count;
        }
    }

    public Counter()
    {
        TotalTime = 0;
        Count = 0;
    }

    public void Add(float value)
    {
        Time = value;
        if(TotalTime > float.MaxValue - value || Count == int.MaxValue - 1)
        {
            TotalTime /= 2f;
            Count /= 2;
        }
        TotalTime += value;
        if (Count == 0)
        {
            MinTime = TotalTime;
            MaxTime = TotalTime;
        }
        if (MinTime > value)
            MinTime = value;
        if (MaxTime < value)
            MaxTime = value;
        Count++;
    }
}
