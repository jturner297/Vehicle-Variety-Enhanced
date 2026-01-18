using System;
using System.Collections.Generic;
using System.Linq;

public static class VehicleSelector
{
    private static Dictionary<HashSet<string>, Queue<string>> _queues = new Dictionary<HashSet<string>, Queue<string>>();

    // THIS is the secret sauce: Remembering the last 4 cars
    private static Dictionary<HashSet<string>, List<string>> _recentHistory = new Dictionary<HashSet<string>, List<string>>();
    private static Random _rnd = new Random();
    private const int HISTORY_SIZE = 4;

    public static string GetNext(HashSet<string> list, HashSet<string> exclusions = null)
    {
        if (list == null || list.Count == 0) return null;

        for (int attempts = 0; attempts < 10; attempts++)
        {
            if (!_queues.ContainsKey(list) || _queues[list].Count == 0)
            {
                RefillQueue(list, exclusions);
            }

            if (_queues[list].Count == 0) return null;

            string candidate = _queues[list].Peek();

            if (exclusions != null && exclusions.Contains(candidate))
            {
                string skipped = _queues[list].Dequeue();
                _queues[list].Enqueue(skipped);
                continue;
            }

            string selected = _queues[list].Dequeue();
            AddToHistory(list, selected);
            return selected;
        }
        return null;
    }

    private static void RefillQueue(HashSet<string> list, HashSet<string> exclusions)
    {
        List<string> batch = new List<string>(list);
        if (batch.Count == 0) return;

        Shuffle(batch);

        // Anti-Repeat: If the new batch starts with a car we saw recently, SWAP IT.
        if (_recentHistory.ContainsKey(list))
        {
            List<string> history = _recentHistory[list];
            if (batch.Count > 1 && history.Contains(batch[0]))
            {
                int safeIndex = _rnd.Next(1, batch.Count);
                string temp = batch[0];
                batch[0] = batch[safeIndex];
                batch[safeIndex] = temp;
            }
        }
        _queues[list] = new Queue<string>(batch);
    }

    private static void AddToHistory(HashSet<string> list, string model)
    {
        if (!_recentHistory.ContainsKey(list)) _recentHistory[list] = new List<string>();
        _recentHistory[list].Add(model);
        if (_recentHistory[list].Count > HISTORY_SIZE) _recentHistory[list].RemoveAt(0);
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