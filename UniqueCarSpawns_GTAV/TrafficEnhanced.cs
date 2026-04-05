using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

public class TrafficEnhanced : Script
{
    // =============================================================
    //                 SETTINGS & TUNING
    // =============================================================

    private const string MP_TAG = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    // Performance & Throttling
    private int _checkInterval = 250;
    private int _swapCooldown = 0; // Ready to run at 0 safely with the new strict heuristic

    // --- DISTANCE TUNING ---
    private float _minSafeDist = 15f;
    private float _fovealDist = 350f;

    // Distance at which we force a swap even if the car is visible
    private bool _enableOnScreenSwap = true;
    private float _OnScreenSwapDist = 250f;

    // Limits
    private int MaxSwapsPerCycle = 1;

    // THRESHOLD
    // The target must hit this to be swapped.
    private float ScoreThreshold = 650f;

    // MEMORY CAP
    private int _memoryCap = 15;

    private int _driveStyle = 786603;
    private bool _debugMode = false;

    // =============================================================
    //                 INTERNAL VARIABLES
    // =============================================================

    private Random _rnd = new Random();
    // Exhaustive, non-repeating pools per vehicle category to avoid RNG clustering
    private Dictionary<string, List<string>> _exhaustivePools = new Dictionary<string, List<string>>();
    private Dictionary<string, int> _exhaustiveIndices = new Dictionary<string, int>();
    private Dictionary<string, string> _exhaustiveLastTaken = new Dictionary<string, string>();
    private int _nextCheck = 0;
    private int _nextSwapTime = 0;
    private int _cleanupTimer = 0;
    // Frequent, lightweight cleanup for debug blips to avoid visible orphaned markers
    private int _blipCleanupTimer = 0;

    private float _minSafeDistSq;
    private float _fovealDistSq;
    private float _forceSwapDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;
    private AmbientProfile _currentProfile;

    // Map of swapped-vehicle handle -> diagnostic blip. We keep a mapping even when
    // debug mode is off so we can recreate blips if the engine strips them later.
    private Dictionary<int, Blip> _swapBlips = new Dictionary<int, Blip>();

    private HashSet<int> _recentSwaps = new HashSet<int>();
    private HashSet<int> _permanentBlacklist = new HashSet<int>();
    // Models (hashes) that should never be used as swap targets or replacements
    private HashSet<int> _modelSwapBlacklist = new HashSet<int>();

    private string _currentZoneLabel = "";
    private List<Model> _hotMemoryList = new List<Model>();
    private Queue<string> _loadQueue = new Queue<string>();
    private int _loadingTicker = 0;

    // Fade-in entries for newly spawned vehicles to reduce visible pop-in
    private class FadeEntry { public int Handle; public int StartTime; public int Duration; public FadeEntry(int h, int s, int d) { Handle = h; StartTime = s; Duration = d; } }
    private List<FadeEntry> _fadeEntries = new List<FadeEntry>();

    private List<int> _spawnHistory = new List<int>();
    private int _historyDepth = 60;
    private Dictionary<int, int> _spawnTimestamps = new Dictionary<int, int>();
    private int _spawnTTL = 5 * 60 * 1000; // 5 minutes
    // Observed-model short-term memory to prevent temporal recurrence (deja-vu)
    private Dictionary<int, int> _observedModelTimestamps = new Dictionary<int, int>();
    private int _observedTTL = 2 * 60 * 1000; // 2 minutes
    // Map model hash -> last observed vehicle handle to distinguish the same instance
    private Dictionary<int, int> _observedModelLastHandle = new Dictionary<int, int>();

    public TrafficEnhanced()
    {
        _minSafeDistSq = _minSafeDist * _minSafeDist;
        _fovealDistSq = _fovealDist * _fovealDist;
        _forceSwapDistSq = _OnScreenSwapDist * _OnScreenSwapDist;

        InitializeZones();

        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);
        Function.Call(Hash.DECOR_REGISTER, MP_TAG, 3);

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void InitializeZones()
    {
        AmbientProfile Hippy = new AmbientProfile(10, 70, 100, 0);
        AmbientProfile Gangster = new AmbientProfile(0, 50, 100, 0);
        AmbientProfile Downtown = new AmbientProfile(25, 45, 30, 0);
        AmbientProfile Vinewood = new AmbientProfile(80, 50, 30, 0);
        AmbientProfile Coastal = new AmbientProfile(100, 60, 20, 0);
        AmbientProfile Elite = new AmbientProfile(110, 50, 20, 0);
        AmbientProfile VinewoodHills = new AmbientProfile(100, 30, 5, 0);
        AmbientProfile Industry = new AmbientProfile(0, 70, 100, 0);
        AmbientProfile CountrySide = new AmbientProfile(0, 30, 80, 100);

        _defaultProfile = new AmbientProfile(15, 60, 25, 0);

        AssignToProfile(Hippy, "MIRR", "EAST_V");
        AssignToProfile(Gangster, "CHAMH", "DAVIS", "RANCHO", "STRAW", "STAD");
        AssignToProfile(Downtown, "VINE", "PBOX", "TEXTI", "SKID", "DOWNT", "LOSPUER", "DELSOL", "KOREAT", "AIRP");
        AssignToProfile(Vinewood, "WVINE", "DTVINE", "BURTON", "HAWICK", "ALTA");
        AssignToProfile(Coastal, "VCANA", "VESP", "PBLUFF", "BHAMCA", "CHU", "DELPE");
        AssignToProfile(Elite, "ROCKF", "RICHM", "MOVIE", "GOLF", "MORN");
        AssignToProfile(VinewoodHills, "RGLEN", "CHIL", "BAYTRE", "GALLI", "OBSERV");
        AssignToProfile(Industry, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO", "TERMINA", "ELYSIAN");
        AssignToProfile(CountrySide, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE", "TONGVAV", "SLAB");
    }

    private void OnTick(object sender, EventArgs e)
    {
        ManageZoneMemory();

        if (Game.GameTime > _cleanupTimer)
        {
            _recentSwaps.RemoveWhere(h => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h));
            _permanentBlacklist.RemoveWhere(h => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h));

            _cleanupTimer = Game.GameTime + 10000;
            // NOTE: Blip cleanup used to only run when debug mode was enabled which
            // could leave orphaned blips when debug was toggled off. Blips are now
            // cleaned up on a separate, more frequent timer to avoid visible ghosts
            // while keeping the heavier housekeeping on the original interval.
        }

        // Update any active fade-ins so spawned vehicles gradually become visible
        UpdateFades();

        // Run a lightweight blip cleanup more frequently than the general cleanup
        // so transient ambient despawns don't leave orphaned debug blips.
        if (Game.GameTime > _blipCleanupTimer)
        {
            try { CleanupBlips(); } catch { }
            _blipCleanupTimer = Game.GameTime + 2000; // every 2 seconds
        }

        if (Game.GameTime < _nextCheck) return;

        try { ProcessAmbientTraffic(); }
        catch (Exception) { }

        _nextCheck = Game.GameTime + _checkInterval;
    }

    private void ManageZoneMemory()
    {
        if (_spawnTimestamps.Count > 0)
        {
            var stale = _spawnTimestamps.Where(kv => Game.GameTime - kv.Value > _spawnTTL).Select(kv => kv.Key).ToList();
            foreach (var k in stale) _spawnTimestamps.Remove(k);
        }

        // Prune observed-model short-term memory
        if (_observedModelTimestamps.Count > 0)
        {
            var staleObs = _observedModelTimestamps.Where(kv => Game.GameTime - kv.Value > _observedTTL).Select(kv => kv.Key).ToList();
            foreach (var k in staleObs) _observedModelTimestamps.Remove(k);
        }
        // Keep the last-handle map in sync with timestamps
        if (_observedModelLastHandle.Count > 0)
        {
            var staleLast = _observedModelLastHandle.Keys.Where(k => !_observedModelTimestamps.ContainsKey(k)).ToList();
            foreach (var k in staleLast) _observedModelLastHandle.Remove(k);
        }

        Vector3 pPos = Game.Player.Character.Position;
        string zoneCode = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pPos.X, pPos.Y, pPos.Z);

        AmbientProfile activeProfile = _zoneRegistry.ContainsKey(zoneCode) ? _zoneRegistry[zoneCode] : _defaultProfile;

        if (activeProfile != _currentProfile)
        {
            _currentProfile = activeProfile;
            _currentZoneLabel = zoneCode;
            _loadQueue.Clear();

            if (_hotMemoryList.Count > 10)
            {
                int removeCount = _hotMemoryList.Count / 2;
                for (int i = 0; i < removeCount; i++)
                {
                    if (_hotMemoryList.Count > 0)
                    {
                        _hotMemoryList[0].MarkAsNoLongerNeeded();
                        _hotMemoryList.RemoveAt(0);
                    }
                }
            }
        }

        if (_loadQueue.Count < 10)
        {
            AddToLoadQueue(zoneCode);
        }

        _loadingTicker++;
        if (_loadingTicker > 3)
        {
            _loadingTicker = 0;
            if (_loadQueue.Count > 0)
            {
                if (_hotMemoryList.Count >= _memoryCap)
                {
                    var oldModel = _hotMemoryList[0];
                    oldModel.MarkAsNoLongerNeeded();
                    _hotMemoryList.RemoveAt(0);
                }

                string modelName = _loadQueue.Dequeue();
                Model m = new Model(modelName);

                if (m.IsValid && m.IsInCdImage)
                {
                    m.Request();
                    _hotMemoryList.Add(m);
                }
            }
        }
    }

    private void AddToLoadQueue(string zone)
    {
        AmbientProfile profile = _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone] : _defaultProfile;

        List<string> wishList = new List<string>();
        // Allocate a proportional number of entries per tier according to the profile chances
        // Preserve previous overall scale by requesting 3 items per non-zero category and
        // distributing them proportionally to the configured chances.
        var categories = new[] {
            new { Key = "rich", Weight = profile.RichChance, Source = VehList.models_rich },
            new { Key = "mid", Weight = profile.MidChance, Source = VehList.models_mid },
            new { Key = "poor", Weight = profile.PoorChance, Source = VehList.models_poor },
            new { Key = "countryside", Weight = profile.CountryChance, Source = VehList.models_countryside }
        };

        int nonZeroCategories = categories.Count(c => c.Weight > 0);
        if (nonZeroCategories > 0)
        {
            int totalDesired = 3 * nonZeroCategories; // legacy-preserving scale

            // Compute raw fractional allocations
            double totalWeight = categories.Where(c => c.Weight > 0).Sum(c => (double)c.Weight);
            var allocations = new Dictionary<string, int>();
            var fractions = new List<Tuple<string, double>>();

            foreach (var c in categories)
            {
                if (c.Weight <= 0) { allocations[c.Key] = 0; continue; }
                double raw = (c.Weight / totalWeight) * totalDesired;
                int floor = (int)Math.Floor(raw);
                allocations[c.Key] = floor;
                fractions.Add(Tuple.Create(c.Key, raw - floor));
            }

            // Distribute remaining slots by largest fractional parts
            int assigned = allocations.Values.Sum();
            int remaining = totalDesired - assigned;
            foreach (var t in fractions.OrderByDescending(x => x.Item2))
            {
                if (remaining <= 0) break;
                allocations[t.Item1]++;
                remaining--;
            }

            // Request from pools according to computed allocations
            if (allocations["rich"] > 0) wishList.AddRange(GetExhaustiveBatch(VehList.models_rich, Math.Max(0, allocations["rich"]), "rich"));
            if (allocations["mid"] > 0) wishList.AddRange(GetExhaustiveBatch(VehList.models_mid, Math.Max(0, allocations["mid"]), "mid"));
            if (allocations["poor"] > 0) wishList.AddRange(GetExhaustiveBatch(VehList.models_poor, Math.Max(0, allocations["poor"]), "poor"));
            if (allocations["countryside"] > 0) wishList.AddRange(GetExhaustiveBatch(VehList.models_countryside, Math.Max(0, allocations["countryside"]), "countryside"));
        }

        foreach (string name in wishList)
        {
            if (!_loadQueue.Contains(name))
            {
                bool alreadyLoaded = false;
                foreach (var loaded in _hotMemoryList)
                {
                    if (loaded.Hash == Function.Call<int>(Hash.GET_HASH_KEY, name))
                    {
                        alreadyLoaded = true;
                        break;
                    }
                }

                if (!alreadyLoaded) _loadQueue.Enqueue(name);
            }
        }
    }

    private IEnumerable<string> GetRandomBatch(HashSet<string> source, int count)
    {
        // Backward-compatible fallback: if no pool key is supplied use a quick random sample
        return source.OrderBy(x => _rnd.Next()).Take(count);
    }

    // New: provide an exhaustive, non-repeating cycle over the provided source set.
    // poolKey should be a stable identifier for the vehicle category (e.g. "rich", "mid").
    private IEnumerable<string> GetExhaustiveBatch(HashSet<string> source, int count, string poolKey)
    {
        if (source == null || source.Count == 0) return Enumerable.Empty<string>();

        // Ensure pool exists and matches the current source size (rebuild if vehicle lists changed)
        if (!_exhaustivePools.ContainsKey(poolKey) || _exhaustivePools[poolKey].Count != source.Count)
        {
            RebuildPool(source, poolKey);
        }

        var pool = _exhaustivePools[poolKey];
        int idx = _exhaustiveIndices.ContainsKey(poolKey) ? _exhaustiveIndices[poolKey] : 0;
        var results = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            if (idx >= pool.Count)
            {
                // Exhausted: reshuffle and reset index. Prevent immediate repeat of last item if possible.
                string last = _exhaustiveLastTaken.ContainsKey(poolKey) ? _exhaustiveLastTaken[poolKey] : null;
                RebuildPool(source, poolKey);
                pool = _exhaustivePools[poolKey];
                idx = 0;
                if (!string.IsNullOrEmpty(last) && pool.Count > 1 && pool[0] == last)
                {
                    // Swap first with another element to avoid immediate repetition
                    int swapWith = 1;
                    var tmp = pool[0]; pool[0] = pool[swapWith]; pool[swapWith] = tmp;
                }
            }

            string item = pool[idx++];
            results.Add(item);
            _exhaustiveLastTaken[poolKey] = item;
        }

        _exhaustiveIndices[poolKey] = idx;
        return results;
    }

    private void RebuildPool(HashSet<string> source, string poolKey)
    {
        var list = source.ToList();
        // Shuffle using cryptographic RNG to reduce bias and repeated patterns
        CryptoShuffle(list);

        _exhaustivePools[poolKey] = list;
        _exhaustiveIndices[poolKey] = 0;
        _exhaustiveLastTaken[poolKey] = null;
    }

    private void CryptoShuffle(List<string> list)
    {
        if (list == null || list.Count <= 1) return;

        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            byte[] buf = new byte[4];
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextCryptoInt(rng, i + 1, buf);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    private int NextCryptoInt(System.Security.Cryptography.RandomNumberGenerator rng, int maxExclusive, byte[] buffer)
    {
        if (maxExclusive <= 1) return 0;
        uint limit = (uint.MaxValue / (uint)maxExclusive) * (uint)maxExclusive;
        while (true)
        {
            rng.GetBytes(buffer);
            uint val = BitConverter.ToUInt32(buffer, 0);
            if (val < limit) return (int)(val % (uint)maxExclusive);
        }
    }

    private void ProcessAmbientTraffic()
    {
        if (Game.GameTime < _nextSwapTime) return;

        var readyModels = _hotMemoryList.Where(m => m.IsLoaded).ToList();
        if (readyModels.Count < 3) return;

        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        bool isZoomed = GameplayCamera.FieldOfView < 50f;

        // Count model frequencies to establish duplicate counts
        var modelFrequencies = vehicles
            .Where(v => v.Exists())
            .GroupBy(v => v.Model.Hash)
            .ToDictionary(g => g.Key, g => g.Count());

        List<ScoredVehicle> candidates = new List<ScoredVehicle>();
        // Collect observed models this tick (modelHash -> handle) then commit after scoring
        var observedThisTick = new Dictionary<int, int>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (_recentSwaps.Contains(v.Handle)) continue;
            if (_permanentBlacklist.Contains(v.Handle)) continue;
            // If this vehicle's model is explicitly blacklisted from swapping, skip it
            if (_modelSwapBlacklist.Contains(v.Model.Hash))
            {
                _permanentBlacklist.Add(v.Handle);
                continue;
            }
            if (IsSwapped(v) || IsExcluded(v))
            {
                _permanentBlacklist.Add(v.Handle);
                continue;
            }

            float distSq = v.Position.DistanceToSquared(camPos);

            // 100m Safety Buffer
            if (distSq < 10000f || distSq > _fovealDistSq) continue;

            bool isBlocked = !IsVehicleVisibleSmart(v, camPos);
            bool isDistantCandidate = _enableOnScreenSwap && (distSq >= _forceSwapDistSq);

            if (v.IsOnScreen && !isBlocked)
            {
                if (!isDistantCandidate || isZoomed)
                {
                    _permanentBlacklist.Add(v.Handle);
                    continue;
                }
            }

            // Get exact duplicate count including short-term observed memory to avoid deja-vu.
            // Only count an observed bonus if the last observed handle differs from the current
            // vehicle handle and the previous observation is older than a 10s grace period.
            int worldCount = modelFrequencies.ContainsKey(v.Model.Hash) ? modelFrequencies[v.Model.Hash] : 0;
            int observedBonus = 0;
            if (_observedModelTimestamps.ContainsKey(v.Model.Hash))
            {
                int age = Game.GameTime - _observedModelTimestamps[v.Model.Hash];
                int lastHandle = _observedModelLastHandle.ContainsKey(v.Model.Hash) ? _observedModelLastHandle[v.Model.Hash] : -1;
                // Only treat as a temporal duplicate if the previous observation is within TTL
                // and older than the 10s grace window, and it's a different physical instance.
                if (age > 10000 && age <= _observedTTL && lastHandle != v.Handle)
                {
                    observedBonus = 1;
                }
            }

            // Defer logging of currently observed models until after we determine if they are
            // temporal duplicates. Only log non-duplicates so that a surviving infiltrator
            // does not overwrite the historic handle and evade detection on the next tick.
            try
            {
                if (distSq <= _fovealDistSq && observedBonus == 0)
                {
                    observedThisTick[v.Model.Hash] = v.Handle;
                }
            }
            catch { }

            int duplicateCount = worldCount + observedBonus;
            if (duplicateCount <= 1) continue; // functional gate early-exit

            float score = GetDeduplicationScore(v, camPos, camDir, isBlocked, isDistantCandidate, duplicateCount);

            if (score > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = score });
            }
        }

        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);

        foreach (var choice in bestChoices)
        {
            if (AttemptSwap(choice.Vehicle, readyModels))
            {
                _nextSwapTime = Game.GameTime + _swapCooldown;
                break;
            }
        }

        // Commit observed models collected this tick into the short-term memory after
        // scoring and swap attempts. Store both timestamp and last handle so that
        // subsequent evaluations can distinguish the same physical instance.
        try
        {
            int now = Game.GameTime;
            foreach (var kv in observedThisTick)
            {
                _observedModelTimestamps[kv.Key] = now;
                _observedModelLastHandle[kv.Key] = kv.Value;
            }
        }
        catch { }
    }

    private bool AttemptSwap(Vehicle oldVeh, List<Model> readyModels)
    {
        if (!oldVeh.Exists()) return false;
        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) return false;

        // Build set of model hashes currently present in the world but exclude the player's
        // own vehicle instances so the model the player is driving is not globally banned
        // from being spawned as a replacement. Other instances of the same model still
        // count toward duplication and will be considered for swapping.
        var worldModelHashes = new HashSet<int>(
            World.GetAllVehicles()
                 .Where(v => v != null && v.Exists() && !(v.Driver != null && v.Driver.IsPlayer))
                 .Select(v => v.Model.Hash)
        );

        int now = Game.GameTime;

        // Restrict candidate models to those appropriate for the current zone/profile.
        // Build an allowlist of model hashes from the active profile's categories.
        var activeProfile = _currentProfile ?? _defaultProfile;
        var allowedNames = new HashSet<string>();
        try
        {
            if (activeProfile.RichChance > 0) allowedNames.UnionWith(VehList.models_rich);
            if (activeProfile.MidChance > 0) allowedNames.UnionWith(VehList.models_mid);
            if (activeProfile.PoorChance > 0) allowedNames.UnionWith(VehList.models_poor);
            if (activeProfile.CountryChance > 0) allowedNames.UnionWith(VehList.models_countryside);
        }
        catch { }

        var allowedHashes = new HashSet<int>();
        foreach (var n in allowedNames)
        {
            try { allowedHashes.Add(Function.Call<int>(Hash.GET_HASH_KEY, n)); } catch { }
        }

        var candidatePool = readyModels.Where(m => allowedHashes.Contains(m.Hash)).ToList();
        // If there are no profile-appropriate models loaded, abort swap to avoid cross-profile bleed.
        if (candidatePool.Count == 0) return false;

        var tier1 = candidatePool.Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash)
                                          && !_spawnHistory.Contains(m.Hash)
                                          && (!_spawnTimestamps.ContainsKey(m.Hash) || now - _spawnTimestamps[m.Hash] > _spawnTTL)
                                          && !_modelSwapBlacklist.Contains(m.Hash))
                                .ToList();

        var tier2 = candidatePool.Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash)
                                          && (!_spawnTimestamps.ContainsKey(m.Hash) || now - _spawnTimestamps[m.Hash] > _spawnTTL)
                                          && !_modelSwapBlacklist.Contains(m.Hash))
                                .ToList();

        var tier3 = candidatePool.Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash)
                                          && !_spawnHistory.Contains(m.Hash)
                                          && !_modelSwapBlacklist.Contains(m.Hash))
                                .ToList();

        var tier4 = candidatePool.Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash)).ToList();

        // Ensure tier4 also respects the blacklist
        tier4 = tier4.Where(m => !_modelSwapBlacklist.Contains(m.Hash)).ToList();

        // Tier5: Deadlock breaker — allow any loaded model from the profile pool that isn't the same as the
        // vehicle we're replacing and isn't explicitly blacklisted.
        var tier5 = candidatePool.Where(m => m.IsLoaded && m.Hash != oldVeh.Model.Hash && !_modelSwapBlacklist.Contains(m.Hash)).ToList();

        List<Model> validCandidates = tier1.Count > 0 ? tier1 : (tier2.Count > 0 ? tier2 : (tier3.Count > 0 ? tier3 : (tier4.Count > 0 ? tier4 : tier5)));

        if (validCandidates == null || validCandidates.Count == 0) return false;

        // Prioritize Least-Recently-Spawned (LRS) models to maximize visible variety.
        // Models with no spawn timestamp (never spawned) are treated as the oldest.
        Model model = null;
        try
        {
            var tsMap = new Dictionary<Model, int>();
            foreach (var m in validCandidates)
            {
                int ts = _spawnTimestamps.ContainsKey(m.Hash) ? _spawnTimestamps[m.Hash] : -1;
                tsMap[m] = ts;
            }

            int minTs = tsMap.Values.Min();
            var oldest = tsMap.Where(kv => kv.Value == minTs).Select(kv => kv.Key).ToList();

            if (oldest.Count == 1)
            {
                model = oldest[0];
            }
            else if (oldest.Count > 1)
            {
                model = oldest[_rnd.Next(oldest.Count)];
            }
        }
        catch
        {
            // Fallback to random selection on any unexpected error
            Shuffle(validCandidates);
            model = validCandidates[0];
        }

        if (!model.IsLoaded) return false;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vector3 spawnPos = oldVeh.Position;
        Vehicle newVeh = World.CreateVehicle(model, spawnPos, oldVeh.Heading);

        if (newVeh != null && newVeh.Exists())
        {
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, false, 0);

            float groundZ;
            try
            {
                World.GetGroundHeight(spawnPos, out groundZ, (GetGroundHeightMode)0);
            }
            catch
            {
                groundZ = spawnPos.Z;
            }

            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, newVeh, spawnPos.X, spawnPos.Y, groundZ, false, false, true);

            if (oldVeh.Speed < 1.0f)
            {
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newVeh);
            }

            newVeh.Velocity = oldVeh.Velocity;
            newVeh.ForwardSpeed = oldVeh.Speed;
            newVeh.IsEngineRunning = oldVeh.IsEngineRunning;

            Function.Call(Hash.DECOR_SET_INT, newVeh, AMB_TAG, 1);
            driver.SetIntoVehicle(newVeh, VehicleSeat.Driver);

            // Verify the driver was successfully placed into the new vehicle before
            // deleting the original. If the transfer failed, clean up the partial
            // new vehicle and abort to avoid deleting a vehicle the player can see.
            bool swapSucceeded = false;
            try
            {
                swapSucceeded = (newVeh != null && newVeh.Exists() && driver != null && driver.Exists() && driver.IsInVehicle() && driver.CurrentVehicle == newVeh);
            }
            catch { swapSucceeded = false; }

            if (!swapSucceeded)
            {
                try {
                    if (newVeh != null && newVeh.Exists())
                    {
                        RemoveFadeEntryForHandle(newVeh.Handle);
                        newVeh.Delete();
                    }
                } catch { }
                // Abort the swap - do not delete the original vehicle.
                return false;
            }

            try
            {
                oldVeh.IsEngineRunning = false;
                oldVeh.Delete();
            }
            catch { }

            // Start a smooth fade-in instead of an instant visibility snap to reduce pop-in.
            StartFadeIn(newVeh, 800);
            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            // Always register the swapped vehicle so we can track it even when debug
            // mode is off. The blip will be created with alpha 0 when debug is off
            // and restored if the engine strips it later.
            try { AddDebugBlip(newVeh); } catch { }

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            _recentSwaps.Add(newVeh.Handle);
            _spawnHistory.Add(model.Hash);
            _spawnTimestamps[model.Hash] = Game.GameTime;
            if (_spawnHistory.Count > _historyDepth) _spawnHistory.RemoveAt(0);

            return true;
        }

        return false;
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private float GetDeduplicationScore(Vehicle v, Vector3 camPos, Vector3 camDir, bool isBlocked, bool isDistantCandidate, int duplicateCount)
    {
        // 1. FUNCTIONAL GATE: The ultimate filter. 
        // If it's the only one of its kind, DO NOT TOUCH IT. 
        if (duplicateCount <= 1)
            return -1000f;

        float score = 0f;

        // 2. SEVERITY MULTIPLIER (Duplicates only)
        // 2 cars = 400 base. 3 cars = 650. 4 cars = 900.
        score += 400f + ((duplicateCount - 1) * 250f);

        // 3. IMMERSION & VISIBILITY (Prevent Pop-in)
        if (v.IsOnScreen && !isBlocked)
        {
            if (isDistantCandidate)
            {
                // Horizon duplicates get a boost so we swap them before they get close
                score += 400f;
            }
            else
            {
                // HARD PENALTY: Never swap a visible, close car. It will visibly pop.
                return -1000f;
            }
        }
        else if (isBlocked)
        {
            // PERFECT TARGET: The car is physically blocked by geometry (buildings/walls).
            score += 500f;
        }
        else if (!v.IsOnScreen)
        {
            // GREAT TARGET: Car is behind the camera.
            score += 300f;

            // Strict Angle Check: Ensure it's firmly out of peripheral vision
            Vector3 toCar = (v.Position - camPos).Normalized;
            float angle = Vector3.Angle(camDir, toCar);
            if (angle < 90f)
            {
                score -= 200f;
            }
        }

        // 4. DISTANCE WEIGHTING
        // Slightly favor duplicates that are further away.
        float dist = v.Position.DistanceTo(camPos);
        score += (dist / 5f);

        return score;
    }

    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z));

        var result = World.Raycast(camPos, roof, IntersectFlags.Map | IntersectFlags.Vehicles);

        if (result.DidHit && result.HitEntity != null)
        {
            if (result.HitEntity == Game.Player.Character.CurrentVehicle)
            {
                return true;
            }
        }

        return !result.DidHit || result.HitEntity == v;
    }

    private bool IsSwapped(Vehicle v)
    {
        return Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG) || Function.Call<bool>(Hash.DECOR_EXIST_ON, v, MP_TAG);
    }

    private bool IsExcluded(Vehicle v)
    {
        if (v.AttachedBlip != null) return true;
        if (v.IsPersistent || Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) return true;

        VehicleClass vc = v.ClassType;
        if (vc == VehicleClass.Emergency || vc == VehicleClass.Industrial || vc == VehicleClass.Utility ||
            vc == VehicleClass.Cycles || vc == VehicleClass.Boats || vc == VehicleClass.Helicopters ||
            vc == VehicleClass.Planes || vc == VehicleClass.Commercial || vc == VehicleClass.Motorcycles) return true;

        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        return false;
    }

    private void AssignToProfile(AmbientProfile profile, params string[] zones)
    {
        foreach (string z in zones) _zoneRegistry[z] = profile;
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F10)
        {
            _debugMode = !_debugMode;
            GTA.UI.Notification.PostTicker($"TrafficEnhanced Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true);
            // Toggle visibility of existing debug blips according to the new mode.
            foreach (var kv in _swapBlips.ToList())
            {
                try
                {
                    var b = kv.Value;
                    if (b != null && b.Exists()) b.Alpha = (_debugMode ? 255 : 0);
                }
                catch { }
            }
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var kv in _swapBlips.ToList()) { var b = kv.Value; if (b != null && b.Exists()) { try { b.Delete(); } catch { } } }
        // Ensure any partially faded vehicles are restored to full opacity
        try
        {
            foreach (var fe in _fadeEntries)
            {
                if (Function.Call<bool>(Hash.DOES_ENTITY_EXIST, fe.Handle))
                {
                    try { Function.Call(Hash.SET_ENTITY_ALPHA, fe.Handle, 255, false); } catch { }
                }
            }
        }
        catch { }
        _fadeEntries.Clear();
    }

    private void AddDebugBlip(Vehicle v)
    {
        if (v == null || !v.Exists()) return;
        try
        {
            // If the vehicle already has a blip, normalize it and store the reference.
            Blip b = v.AttachedBlip ?? v.AddBlip();
            b.Sprite = BlipSprite.Standard;
            b.Color = BlipColor.Green;
            b.Scale = 0.6f;
            b.Name = "Ambient Swap";
            b.IsShortRange = true;
            Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false);
            try { b.Alpha = (_debugMode ? 255 : 0); } catch { }

            _swapBlips[v.Handle] = b;
        }
        catch { }
    }

    private void UpdateFades()
    {
        if (_fadeEntries.Count == 0) return;

        int now = Game.GameTime;
        for (int i = _fadeEntries.Count - 1; i >= 0; i--)
        {
            var fe = _fadeEntries[i];
            // Validate entity still exists
            if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, fe.Handle))
            {
                _fadeEntries.RemoveAt(i);
                continue;
            }

            int elapsed = now - fe.StartTime;
            if (elapsed >= fe.Duration)
            {
                // Ensure fully opaque and remove from list
                try { Function.Call(Hash.SET_ENTITY_ALPHA, fe.Handle, 255, false); } catch { }
                _fadeEntries.RemoveAt(i);
            }
            else
            {
                float t = Math.Max(0f, Math.Min(1f, (float)elapsed / fe.Duration));
                int alpha = (int)(t * 255f);
                try { Function.Call(Hash.SET_ENTITY_ALPHA, fe.Handle, alpha, false); } catch { }
            }
        }
    }

    private void StartFadeIn(Vehicle v, int durationMs)
    {
        if (v == null || !v.Exists()) return;
        try
        {
            // Make visible but start fully transparent
            Function.Call(Hash.SET_ENTITY_VISIBLE, v, true, 0);
            Function.Call(Hash.SET_ENTITY_ALPHA, v, 0, false);
            _fadeEntries.Add(new FadeEntry(v.Handle, Game.GameTime, durationMs));
        }
        catch { }
    }

    private void RemoveFadeEntryForHandle(int handle)
    {
        for (int i = _fadeEntries.Count - 1; i >= 0; i--)
        {
            if (_fadeEntries[i].Handle == handle) _fadeEntries.RemoveAt(i);
        }
    }

    private void CleanupBlips()
    {
        // Iterate tracked swapped vehicles and ensure their blips remain attached.
        var keys = _swapBlips.Keys.ToList();
        foreach (var handle in keys)
        {
            Blip b = _swapBlips.ContainsKey(handle) ? _swapBlips[handle] : null;

            // Use direct entity existence check to avoid transient misses from World.GetAllVehicles
            bool exists = false;
            try { exists = Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle); } catch { exists = false; }

            if (!exists)
            {
                // Vehicle truly gone: remove any leftover blip and forget it.
                try { if (b != null && b.Exists()) b.Delete(); } catch { }
                _swapBlips.Remove(handle);
                continue;
            }

            // Vehicle exists according to the engine. Obtain a Vehicle wrapper directly from handle
            Vehicle veh = null;
            try { veh = Entity.FromHandle(handle) as Vehicle; } catch { veh = null; }

            if (veh == null || !veh.Exists())
            {
                // Defensive: if wrapper couldn't be created, skip recreation attempt this tick.
                continue;
            }

            // Vehicle still exists. If the engine stripped the blip, recreate it and preserve debug visibility.
            if (b == null || !b.Exists() || b.Entity == null || !b.Entity.Exists() || b.Entity.Handle != handle)
            {
                try { if (b != null && b.Exists()) b.Delete(); } catch { }
                try
                {
                    Blip newb = veh.AttachedBlip ?? veh.AddBlip();
                    newb.Sprite = BlipSprite.Standard;
                    newb.Color = BlipColor.Green;
                    newb.Scale = 0.6f;
                    newb.Name = "Ambient Swap";
                    newb.IsShortRange = true;
                    Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, newb, false);
                    try { newb.Alpha = (_debugMode ? 255 : 0); } catch { }
                    _swapBlips[handle] = newb;
                }
                catch { }
            }
            else
            {
                // Ensure alpha syncs to debug mode
                try { b.Alpha = (_debugMode ? 255 : 0); } catch { }
            }
        }
    }

    private struct ScoredVehicle { public Vehicle Vehicle; public float Score; }

    private class AmbientProfile
    {
        public int RichChance; public int MidChance; public int PoorChance; public int CountryChance;
        public AmbientProfile(int rich, int mid, int poor, int country)
        {
            RichChance = rich; MidChance = mid; PoorChance = poor; CountryChance = country;
        }
    }

    // Public helpers to manage the swap blacklist
    public void AddModelToSwapBlacklist(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        int hash = Function.Call<int>(Hash.GET_HASH_KEY, modelName);
        _modelSwapBlacklist.Add(hash);
    }

    public void AddModelToSwapBlacklist(int modelHash)
    {
        _modelSwapBlacklist.Add(modelHash);
    }

    public void RemoveModelFromSwapBlacklist(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        int hash = Function.Call<int>(Hash.GET_HASH_KEY, modelName);
        _modelSwapBlacklist.Remove(hash);
    }

    public void RemoveModelFromSwapBlacklist(int modelHash)
    {
        _modelSwapBlacklist.Remove(modelHash);
    }

    public bool IsModelSwapBlacklisted(Model m)
    {
        if (m == null) return false;
        return _modelSwapBlacklist.Contains(m.Hash);
    }
}