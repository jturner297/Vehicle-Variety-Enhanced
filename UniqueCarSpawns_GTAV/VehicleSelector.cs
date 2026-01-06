using System;
using System.Collections.Generic;
using System.Linq;

public static class VehicleSelector
{
    // The "Memory" of what spawned last (Shared between Traffic and Parked)
    private static Dictionary<HashSet<string>, Queue<string>> _queues = new Dictionary<HashSet<string>, Queue<string>>();
    private static Dictionary<HashSet<string>, string> _lastSpawned = new Dictionary<HashSet<string>, string>();
    private static Random _rnd = new Random();

    /// <summary>
    /// Gets the next unique vehicle from the list, handling shuffling and queues automatically.
    /// </summary>
    public static string GetNext(HashSet<string> list, HashSet<string> exclusions = null)
    {
        if (list == null || list.Count == 0) return null;

        // If we ran out of cars in the current batch (or it's the first run), refill the queue
        if (!_queues.ContainsKey(list) || _queues[list].Count == 0)
        {
            List<string> batch = new List<string>(list);

            // 1. Remove Excluded Models (Used by TrafficMP)
            if (exclusions != null && exclusions.Count > 0)
            {
                batch.RemoveAll(x => exclusions.Contains(x));
            }

            if (batch.Count == 0) return null;

            // 2. Shuffle the batch
            Shuffle(batch);

            // 3. Anti-Repeat Logic 
            // Ensure the first car of the NEW batch isn't the same as the last car of the OLD batch
            if (_lastSpawned.ContainsKey(list) && batch.Count > 1)
            {
                if (batch[0] == _lastSpawned[list])
                {
                    // Swap first with last to break the streak
                    string temp = batch[0];
                    batch[0] = batch[batch.Count - 1];
                    batch[batch.Count - 1] = temp;
                }
            }

            _queues[list] = new Queue<string>(batch);
        }

        // Dequeue the next car
        string selection = _queues[list].Dequeue();
        _lastSpawned[list] = selection;
        return selection;
    }

    // Standard Fisher-Yates Shuffle
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