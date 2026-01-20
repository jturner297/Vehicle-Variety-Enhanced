using System;
using System.Collections.Generic;

public static class VehicleSelector
{
    // Storage: [List Object] -> [Context String] -> [Queue]
    private static Dictionary<HashSet<string>, Dictionary<string, Queue<string>>> _queues =
        new Dictionary<HashSet<string>, Dictionary<string, Queue<string>>>();

    private static Random _rnd = new Random();

    // --------------------------------------------------------
    // UNIFIED METHOD
    // --------------------------------------------------------
    // We removed the overload. Now 'null' (from ParkedMP) can ONLY be a string.
    public static string GetNext(HashSet<string> list, string context)
    {
        if (list == null || list.Count == 0) return null;

        // 1. Handle ParkedMP Compatibility
        // ParkedMP passes 'null'. We treat that as the "Parked" deck.
        if (string.IsNullOrEmpty(context))
        {
            context = "Parked";
        }

        // 2. Ensure the LIST entry exists
        if (!_queues.ContainsKey(list))
        {
            _queues[list] = new Dictionary<string, Queue<string>>();
        }

        // 3. Ensure the CONTEXT Queue exists (and is not empty)
        if (!_queues[list].ContainsKey(context) || _queues[list][context].Count == 0)
        {
            RefillQueue(list, context);
        }

        // 4. Safety Check (in case refill failed)
        if (_queues[list][context].Count == 0) return null;

        // 5. Deal the card
        return _queues[list][context].Dequeue();
    }

    // --------------------------------------------------------
    // INTERNAL LOGIC
    // --------------------------------------------------------
    private static void RefillQueue(HashSet<string> list, string context)
    {
        // Create a new independent batch
        List<string> batch = new List<string>(list);

        // Shuffle this specific batch
        Shuffle(batch);

        // Assign it to the specific Context slot
        _queues[list][context] = new Queue<string>(batch);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rnd.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}