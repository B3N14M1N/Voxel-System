using System.Collections.Generic;

/// <summary>
/// Provides static methods to track and manage performance counters.
/// </summary>
public static class AvgCounter
{
    private static Dictionary<string, Counter> kvp = new Dictionary<string, Counter>();

    /// <summary>
    /// Creates a new counter with the specified name.
    /// </summary>
    /// <param name="name">The name to identify this counter</param>
    public static void AddCounter(string name)
    {
        kvp.TryAdd(name, new Counter());
    }

    /// <summary>
    /// Updates the specified counter with a new time value, creating it if it doesn't exist.
    /// </summary>
    /// <param name="name">The name of the counter to update</param>
    /// <param name="time">The time value to add to the counter</param>
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

    /// <summary>
    /// Retrieves a counter by its name.
    /// </summary>
    /// <param name="name">The name of the counter to retrieve</param>
    /// <returns>The counter object, or null if not found</returns>
    public static Counter GetCounter(string name)
    {
        kvp.TryGetValue(name, out Counter counter);
        return counter;
    }

    /// <summary>
    /// Removes a counter from the collection.
    /// </summary>
    /// <param name="name">The name of the counter to remove</param>
    public static void RemoveCounter(string name)
    {
        kvp.Remove(name);
    }
}

/// <summary>
/// Tracks performance statistics including current, minimum, maximum, and average times.
/// </summary>
public class Counter
{
    public float Time, MinTime, MaxTime;
    private float TotalTime;
    private int Count;
    
    /// <summary>
    /// Gets the average time across all recorded values.
    /// </summary>
    public float AVG 
    {  
        get
        {
            if (Count == 0)
                return 0;
            return TotalTime / Count;
        }
    }

    /// <summary>
    /// Creates a new counter with zero values.
    /// </summary>
    public Counter()
    {
        TotalTime = 0;
        Count = 0;
    }

    /// <summary>
    /// Adds a value to the counter and updates statistics.
    /// </summary>
    /// <param name="value">The value to add to the counter</param>
    public void Add(float value)
    {
        Time = value;
        if (TotalTime > float.MaxValue - value || Count == int.MaxValue - 1)
        {
            TotalTime /= 2f;
            Count /= 2;
        }
        TotalTime += value;
        
        if (Count == 0)
        {
            MinTime = value;
            MaxTime = value;
        }
        
        if (MinTime > value)
            MinTime = value;
            
        if (MaxTime < value)
            MaxTime = value;
            
        Count++;
    }
}
