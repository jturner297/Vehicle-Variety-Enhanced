using System;
using System.Collections.Generic;
using System.Linq;

public static class VehicleSelector
{
    // The "Deck" of cards (Queue) for each car list
    private static Dictionary<HashSet<string>, Queue<string>> _queues = new Dictionary<HashSet<string>, Queue<string>>();

    // THE FIX: History Buffer. Remember the last 4 cars, not just 1.
    private static Dictionary<HashSet<string>, List<string>> _recentHistory = new Dictionary<HashSet<string>, List<string>>();

    private static Random _rnd = new Random();
    private const int HISTORY_SIZE = 4; // How many cars to remember to avoid duplicates

    public static string GetNext(HashSet<string> list, HashSet<string> exclusions = null)
    {
        if (list == null || list.Count == 0) return null;

        // 1. Ensure Queue Exists and has items
        // We loop here to handle the case where we pull an "Excluded" car and need to try again immediately
        for (int attempts = 0; attempts < 10; attempts++)
        {
            if (!_queues.ContainsKey(list) || _queues[list].Count == 0)
            {
                RefillQueue(list, exclusions);
            }

            // If refill failed (e.g. all cars are excluded), give up
            if (_queues[list].Count == 0) return null;

            // 2. Peek at the next car
            string candidate = _queues[list].Peek();

            // 3. EXCLUSION CHECK (The "Parked filled the queue" Fix)
            // If Traffic asks for a car, but the next one in the queue is "Deveste" (which Parked put there),
            // we must SKIP it for Traffic, but keep it for Parked.
            // Strategy: Cycle it to the back of the queue and try again.
            if (exclusions != null && exclusions.Contains(candidate))
            {
                // Move this banned car to the back of the line
                string skipped = _queues[list].Dequeue();
                _queues[list].Enqueue(skipped);
                continue; // Try the loop again with the new front car
            }

            // 4. Success - Dequeue and Return
            string selected = _queues[list].Dequeue();
            AddToHistory(list, selected);
            return selected;
        }

        return null; // Should rarely happen unless entire list is excluded
    }

    private static void RefillQueue(HashSet<string> list, HashSet<string> exclusions)
    {
        // Create a fresh batch
        List<string> batch = new List<string>(list);

        // NOTE: We do NOT remove exclusions here permanently, because ParkedMP might need them later.
        // We handle strict exclusion filtering at the Dequeue step above.

        if (batch.Count == 0) return;

        Shuffle(batch);

        // 5. SMART ANTI-REPEAT
        // Ensure the start of the NEW batch doesn't match the history of the OLD batch
        if (_recentHistory.ContainsKey(list))
        {
            List<string> history = _recentHistory[list];

            // If the first car in our new batch was seen recently...
            if (batch.Count > 1 && history.Contains(batch[0]))
            {
                // Swap it with a random car from the middle/end of the new batch
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
        if (!_recentHistory.ContainsKey(list))
        {
            _recentHistory[list] = new List<string>();
        }

        _recentHistory[list].Add(model);

        // Keep history size limited
        if (_recentHistory[list].Count > HISTORY_SIZE)
        {
            _recentHistory[list].RemoveAt(0); // Remove oldest
        }
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