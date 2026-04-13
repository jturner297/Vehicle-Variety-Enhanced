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
    private int _swapCooldown = 0;

    // --- DISTANCE TUNING ---
    private float _minSafeDist = 15f;
    private float _fovealDist = 350f;

    // Distance at which we force a swap even if the car is visible
    private bool _enableOnScreenSwap = true; // RE-ENABLED per your request!
    private float _OnScreenSwapDist = 250f;

    // Limits
    private int MaxSwapsPerCycle = 1;

    // THRESHOLD
    private float ScoreThreshold = 900f;

    // MEMORY CAP
    private int _memoryCap = 15;

    private int _driveStyle = 786603;
    private bool _debugMode = false;

    // =============================================================
    //                 INTERNAL VARIABLES
    // =============================================================

    private Random _rnd = new Random();
    private Dictionary<string, List<string>> _exhaustivePools = new Dictionary<string, List<string>>();
    private Dictionary<string, int> _exhaustiveIndices = new Dictionary<string, int>();
    private Dictionary<string, string> _exhaustiveLastTaken = new Dictionary<string, string>();
    private int _nextCheck = 0;
    private int _nextSwapTime = 0;
    private int _cleanupTimer = 0;
    private int _blipCleanupTimer = 0;

    private int _lastSuccessfulSwap = 0;
    private int _boredomIntervalMs = 60 * 1000; // 60s to prevent shotgunning

    private float _minSafeDistSq;
    private float _fovealDistSq;
    private float _forceSwapDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;
    private AmbientProfile _currentProfile;

    private Dictionary<int, Blip> _swapBlips = new Dictionary<int, Blip>();

    private HashSet<int> _recentSwaps = new HashSet<int>();
    private HashSet<int> _permanentBlacklist = new HashSet<int>();
    private HashSet<int> _modelSwapBlacklist = new HashSet<int>();

    private string _currentZoneLabel = "";
    private List<Model> _hotMemoryList = new List<Model>();
    private Queue<string> _loadQueue = new Queue<string>();
    private int _loadingTicker = 0;

    private class FadeEntry { public int Handle; public int StartTime; public int Duration; public FadeEntry(int h, int s, int d) { Handle = h; StartTime = s; Duration = d; } }
    private List<FadeEntry> _fadeEntries = new List<FadeEntry>();

    private List<int> _spawnHistory = new List<int>();
    private int _historyDepth = 60;
    private Dictionary<int, int> _spawnTimestamps = new Dictionary<int, int>();
    private int _spawnTTL = 5 * 60 * 1000; // 5 minutes

    // Observed-model short-term memory to prevent temporal recurrence (deja-vu)
    private Dictionary<int, int> _observedModelTimestamps = new Dictionary<int, int>();
    private Dictionary<int, int> _observedModelLastHandle = new Dictionary<int, int>();

    // THE STRIKE SYSTEM
    private Dictionary<int, int> _observedModelCounts = new Dictionary<int, int>();
    private int _observationTolerance = 2; // The script gets mad on the 3rd car
    private int _observedTTL = 1 * 60 * 1000; // 1 minutes

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
        }

        UpdateFades();

        if (Game.GameTime > _blipCleanupTimer)
        {
            try { CleanupBlips(); } catch { }
            _blipCleanupTimer = Game.GameTime + 2000;
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

        if (_observedModelTimestamps.Count > 0)
        {
            var staleObs = _observedModelTimestamps.Where(kv => Game.GameTime - kv.Value > _observedTTL).Select(kv => kv.Key).ToList();
            foreach (var k in staleObs) _observedModelTimestamps.Remove(k);
        }

        if (_observedModelLastHandle.Count > 0)
        {
            var staleLast = _observedModelLastHandle.Keys.Where(k => !_observedModelTimestamps.ContainsKey(k)).ToList();
            foreach (var k in staleLast) _observedModelLastHandle.Remove(k);
        }

        if (_observedModelCounts.Count > 0)
        {
            var staleCounts = _observedModelCounts.Keys.Where(k => !_observedModelTimestamps.ContainsKey(k)).ToList();
            foreach (var k in staleCounts) _observedModelCounts.Remove(k);
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

        var categories = new[] {
            new { Key = "rich", Weight = profile.RichChance, Source = VehList.models_rich },
            new { Key = "mid", Weight = profile.MidChance, Source = VehList.models_mid },
            new { Key = "poor", Weight = profile.PoorChance, Source = VehList.models_poor },
            new { Key = "countryside", Weight = profile.CountryChance, Source = VehList.models_countryside }
        };

        int nonZeroCategories = categories.Count(c => c.Weight > 0);
        if (nonZeroCategories > 0)
        {
            int totalDesired = 3 * nonZeroCategories;

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

            int assigned = allocations.Values.Sum();
            int remaining = totalDesired - assigned;
            foreach (var t in fractions.OrderByDescending(x => x.Item2))
            {
                if (remaining <= 0) break;
                allocations[t.Item1]++;
                remaining--;
            }

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

    private IEnumerable<string> GetExhaustiveBatch(HashSet<string> source, int count, string poolKey)
    {
        if (source == null || source.Count == 0) return Enumerable.Empty<string>();

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
                string last = _exhaustiveLastTaken.ContainsKey(poolKey) ? _exhaustiveLastTaken[poolKey] : null;
                RebuildPool(source, poolKey);
                pool = _exhaustivePools[poolKey];
                idx = 0;
                if (!string.IsNullOrEmpty(last) && pool.Count > 1 && pool[0] == last)
                {
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

        List<ScoredVehicle> candidates = new List<ScoredVehicle>();
        var observedThisTick = new Dictionary<int, int>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (_recentSwaps.Contains(v.Handle)) continue;
            if (_permanentBlacklist.Contains(v.Handle)) continue;

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

            // ==========================================
            // 1. HUMAN AWARENESS MEMORY (Do this FIRST!)
            // ==========================================
            try
            {
                bool isClose = distSq <= 10000f; // 100 meters
                bool isVisibleToPlayer = v.IsOnScreen && !isBlocked;

                if (isClose || isVisibleToPlayer)
                {
                    observedThisTick[v.Model.Hash] = v.Handle;
                }
            }
            catch { }

            // ==========================================
            // 2. THE OBJECT PERMANENCE LOCK
            // ==========================================
            if (v.IsOnScreen && !isBlocked)
            {
                if (!isDistantCandidate || isZoomed)
                {
                    _permanentBlacklist.Add(v.Handle);
                    continue; // Skip the rest of the swap logic! This car is safe forever.
                }
            }

            // ==========================================
            // 3. SYSTEM 1: PROXIMITY CLUSTER DETECTION 
            // ==========================================
            int clusterCount = 0;
            float clusterRadiusSq = 80f * 80f; // 80 meters

            foreach (Vehicle otherVeh in vehicles)
            {
                if (otherVeh != null && otherVeh.Exists() && otherVeh.Model.Hash == v.Model.Hash)
                {
                    if (v.Position.DistanceToSquared(otherVeh.Position) <= clusterRadiusSq)
                    {
                        clusterCount++;
                    }
                }
            }

            // ==========================================
            // 4. SYSTEM 2: ANNOYANCE BAN-LIST MEMORY
            // ==========================================
            int observedBonus = 0;
            if (_observedModelTimestamps.ContainsKey(v.Model.Hash))
            {
                int age = Game.GameTime - _observedModelTimestamps[v.Model.Hash];
                int lastHandle = _observedModelLastHandle.ContainsKey(v.Model.Hash) ? _observedModelLastHandle[v.Model.Hash] : -1;

                // 10-second grace window protects cars currently driving next to you from engine ID shuffles
                if (age > 10000 && age <= _observedTTL && lastHandle != v.Handle)
                {
                    int strikeCount = _observedModelCounts.ContainsKey(v.Model.Hash) ? _observedModelCounts[v.Model.Hash] : 1;
                    if (strikeCount >= _observationTolerance)
                    {
                        observedBonus = 1; // It hit the strike limit. BAN IT.
                    }
                }
            }

            // ==========================================
            // 5. THE TWO-SYSTEM EVALUATION
            // ==========================================
            bool isClusterThreat = clusterCount > 1;
            float clusterBaseScore = isClusterThreat ? (400f + ((clusterCount - 1) * 250f)) : 0f;

            bool isBannedThreat = observedBonus > 0;
            float bannedBaseScore = isBannedThreat ? 2000f : 0f; // Massive score to guarantee swapping

            // If it's neither a cluster nor banned, we do not care about it.
            if (!isClusterThreat && !isBannedThreat) continue;

            // Take whichever threat score is higher
            float startingScore = Math.Max(clusterBaseScore, bannedBaseScore);
            float finalScore = startingScore;

            // ==========================================
            // 6. THE SAFETY BOUNCER (Distance & Visibility)
            // ==========================================
            if (v.IsOnScreen && !isBlocked)
            {
                if (isDistantCandidate && !isZoomed)
                {
                    finalScore += 400f; // Far away, fair game to morph
                }
                else
                {
                    finalScore = -1000f;
                }
            }
            else if (isBlocked)
            {
                finalScore += 500f; // Perfectly safe behind a building
            }
            else if (!v.IsOnScreen)
            {
                finalScore += 300f; // Safe off-screen
                Vector3 toCar = (v.Position - camPos).Normalized;
                float angle = Vector3.Angle(camDir, toCar);
                if (angle < 90f) finalScore -= 200f; // Bad peripheral angle penalty
            }

            // Add distance tie-breaker
            finalScore += (v.Position.DistanceTo(camPos) / 5f);

            // If it survived the safety bouncer, queue it for execution!
            if (finalScore > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = finalScore });
            }
        }

        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);
        bool swappedThisCycle = false;
        foreach (var choice in bestChoices)
        {
            if (AttemptSwap(choice.Vehicle, readyModels))
            {
                _nextSwapTime = Game.GameTime + _swapCooldown;
                swappedThisCycle = true;
                break;
            }
        }

        // Boredom fallback: 60-second patience
        if (!swappedThisCycle && Game.GameTime - _lastSuccessfulSwap >= _boredomIntervalMs)
        {
            try
            {
                var fallback = vehicles
                    .Where(v => v != null && v.Exists() && v.Driver != null && !v.Driver.IsPlayer)
                    .Where(v => !_recentSwaps.Contains(v.Handle) && !_permanentBlacklist.Contains(v.Handle))
                    .Where(v => !_modelSwapBlacklist.Contains(v.Model.Hash))
                    .Select(v => new { Veh = v, Dist = v.Position.DistanceTo(Game.Player.Character.Position), IsBlocked = !IsVehicleVisibleSmart(v, GameplayCamera.Position) })
                    .Where(x => x.Veh.Position.DistanceToSquared(GameplayCamera.Position) >= 10000f && x.Veh.Position.DistanceToSquared(GameplayCamera.Position) <= _fovealDistSq)
                    .Where(x => x.IsBlocked || !x.Veh.IsOnScreen)
                    .OrderByDescending(x => x.Dist)
                    .Select(x => x.Veh)
                    .FirstOrDefault();

                if (fallback != null)
                {
                    if (AttemptSwap(fallback, readyModels))
                    {
                        _nextSwapTime = Game.GameTime + _swapCooldown;
                        swappedThisCycle = true;
                    }
                }
            }
            catch { }
        }

        // Commit Human Awareness memory and strike tally
        try
        {
            int now = Game.GameTime;
            foreach (var kv in observedThisTick)
            {
                int hash = kv.Key;
                int handle = kv.Value;

                int lastH = _observedModelLastHandle.ContainsKey(hash) ? _observedModelLastHandle[hash] : -1;
                if (lastH != handle)
                {
                    int currentStrikes = _observedModelCounts.ContainsKey(hash) ? _observedModelCounts[hash] : 0;
                    _observedModelCounts[hash] = currentStrikes + 1;
                }

                _observedModelTimestamps[hash] = now;
                _observedModelLastHandle[hash] = handle;
            }
        }
        catch { }
    }

    private bool AttemptSwap(Vehicle oldVeh, List<Model> readyModels)
    {
        if (!oldVeh.Exists()) return false;
        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) return false;

        var worldModelHashes = new HashSet<int>(
            World.GetAllVehicles()
                 .Where(v => v != null && v.Exists() && !(v.Driver != null && v.Driver.IsPlayer))
                 .Select(v => v.Model.Hash)
        );

        int now = Game.GameTime;

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

        // ==========================================
        // THE AWARENESS FILTER (The Fix!)
        // ==========================================
        // Grab every car model we have looked at recently.
        var recentlySeenHashes = new HashSet<int>(_observedModelCounts.Keys);

        // Filter the candidate pool to explicitly ban recently seen cars
        var candidatePool = readyModels.Where(m =>
            allowedHashes.Contains(m.Hash) &&
            !recentlySeenHashes.Contains(m.Hash)
        ).ToList();

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

        tier4 = tier4.Where(m => !_modelSwapBlacklist.Contains(m.Hash)).ToList();

        var tier5 = candidatePool.Where(m => m.IsLoaded && m.Hash != oldVeh.Model.Hash && !_modelSwapBlacklist.Contains(m.Hash)).ToList();

        List<Model> validCandidates = tier1.Count > 0 ? tier1 : (tier2.Count > 0 ? tier2 : (tier3.Count > 0 ? tier3 : (tier4.Count > 0 ? tier4 : tier5)));

        if (validCandidates == null || validCandidates.Count == 0) return false;

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

            bool swapSucceeded = false;
            try
            {
                swapSucceeded = (newVeh != null && newVeh.Exists() && driver != null && driver.Exists() && driver.IsInVehicle() && driver.CurrentVehicle == newVeh);
            }
            catch { swapSucceeded = false; }

            if (!swapSucceeded)
            {
                try
                {
                    if (newVeh != null && newVeh.Exists())
                    {
                        RemoveFadeEntryForHandle(newVeh.Handle);
                        newVeh.Delete();
                    }
                }
                catch { }
                return false;
            }

            try
            {
                oldVeh.IsEngineRunning = false;
                oldVeh.Delete();
            }
            catch { }

            StartFadeIn(newVeh, 800);
            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            try { AddDebugBlip(newVeh); } catch { }

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            _recentSwaps.Add(newVeh.Handle);
            _spawnHistory.Add(model.Hash);
            _spawnTimestamps[model.Hash] = Game.GameTime;
            _lastSuccessfulSwap = Game.GameTime;
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
            if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, fe.Handle))
            {
                _fadeEntries.RemoveAt(i);
                continue;
            }

            int elapsed = now - fe.StartTime;
            if (elapsed >= fe.Duration)
            {
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
        var keys = _swapBlips.Keys.ToList();
        foreach (var handle in keys)
        {
            Blip b = _swapBlips.ContainsKey(handle) ? _swapBlips[handle] : null;

            Vehicle veh = null;
            try { veh = Entity.FromHandle(handle) as Vehicle; } catch { veh = null; }

            // THE FIX: If the vehicle no longer exists in the world, we MUST delete the blip!
            if (veh == null || !veh.Exists() || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
            {
                try { if (b != null && b.Exists()) b.Delete(); } catch { }
                _swapBlips.Remove(handle);
                continue; // Now we safely move on
            }

            // If the vehicle DOES exist, ensure the blip is attached and synced
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