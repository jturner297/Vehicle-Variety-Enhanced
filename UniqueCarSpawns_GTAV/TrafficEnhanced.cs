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
    private int _swapCooldown = 1000;

    // --- DISTANCE TUNING ---
    private float _minSafeDist = 15f;
    private float _fovealDist = 350f;

    // Distance at which we force a swap even if the car is visible
    private bool _enableOnScreenSwap = true;
    private float _OnScreenSwapDist = 250f;

    // Limits
    private int MaxSwapsPerCycle = 1;

    // NEW THRESHOLD: 600
    // This effectively ignores unique cars (scoring < 400) and only targets
    // Duplicates (Score 800+) or Horizon cars (Score 1000+)
    private float ScoreThreshold = 500f;

    // MEMORY CAP
    private int _memoryCap = 45;

    private int _driveStyle = 786603;
    private bool _debugMode = false;

    // =============================================================
    //                 INTERNAL VARIABLES
    // =============================================================

    private Random _rnd = new Random();
    private int _nextCheck = 0;
    private int _nextSwapTime = 0;
    private int _cleanupTimer = 0;

    private float _minSafeDistSq;
    private float _fovealDistSq;
    private float _forceSwapDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;
    private AmbientProfile _currentProfile;

    private List<Blip> _debugBlips = new List<Blip>();

    private HashSet<int> _recentSwaps = new HashSet<int>();
    private HashSet<int> _permanentBlacklist = new HashSet<int>();

    private string _currentZoneLabel = "";
    private List<Model> _hotMemoryList = new List<Model>();
    private Queue<string> _loadQueue = new Queue<string>();
    private int _loadingTicker = 0;

    private List<int> _spawnHistory = new List<int>();
    private int _historyDepth = 28;

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
        AmbientProfile Vinewood = new AmbientProfile(60, 50, 30, 0);
        AmbientProfile Coastal = new AmbientProfile(100, 70, 20, 0);
        AmbientProfile Elite = new AmbientProfile(100, 70, 20, 0);
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
            if (_debugMode) CleanupBlips();
        }

        if (Game.GameTime < _nextCheck) return;

        try { ProcessAmbientTraffic(); }
        catch (Exception) { }

        _nextCheck = Game.GameTime + _checkInterval;
    }

    private void ManageZoneMemory()
    {
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

        if (profile.RichChance > 0) wishList.AddRange(GetRandomBatch(VehList.models_rich, 3));
        if (profile.MidChance > 0) wishList.AddRange(GetRandomBatch(VehList.models_mid, 3));
        if (profile.PoorChance > 0) wishList.AddRange(GetRandomBatch(VehList.models_poor, 3));
        if (profile.CountryChance > 0) wishList.AddRange(GetRandomBatch(VehList.models_countryside, 3));

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
        return source.OrderBy(x => _rnd.Next()).Take(count);
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

        // NEW: 1. Build Global Frequency Map
        // Counts how many times each vehicle model appears in the current world list
        var modelFrequencies = vehicles
            .Where(v => v.Exists())
            .GroupBy(v => v.Model.Hash)
            .ToDictionary(g => g.Key, g => g.Count());

        List<ScoredVehicle> candidates = new List<ScoredVehicle>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (_recentSwaps.Contains(v.Handle)) continue;
            if (_permanentBlacklist.Contains(v.Handle)) continue;
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

            // PASS 1: Check if this car is a duplicate
            bool isDuplicate = false;
            if (modelFrequencies.ContainsKey(v.Model.Hash))
            {
                if (modelFrequencies[v.Model.Hash] > 1) isDuplicate = true;
            }

            float score = GetDeduplicationScore(v, camPos, camDir, isBlocked, isDistantCandidate, isDuplicate);

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
    }

    // Replaced AttemptSwap method (use this body to overwrite the existing method)
    private bool AttemptSwap(Vehicle oldVeh, List<Model> readyModels)   
    {
        if (!oldVeh.Exists()) return false;
        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) return false;

        // Build set of model hashes already present in the world to avoid duplicates.
        var worldModelHashes = new HashSet<int>(
            World.GetAllVehicles()
                 .Where(v => v != null && v.Exists())
                 .Select(v => v.Model.Hash)
        );

        // Primary candidates: loaded models that are NOT currently on the road and not in spawn history.
        var validCandidates = readyModels
            .Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash) && !_spawnHistory.Contains(m.Hash))
            .ToList();

        // If none, try a less strict set: models not currently on the road (ignore spawn history).
        if (validCandidates.Count == 0)
        {
            validCandidates = readyModels
                .Where(m => m.IsLoaded && !worldModelHashes.Contains(m.Hash))
                .ToList();
        }

        // If still none, don't swap — this enforces the rule: prefer models not already on the road.
        if (validCandidates.Count == 0)
        {
            return false;
        }

        // Improve RNG determinism with Fisher-Yates shuffle and then pick the first candidate.
        Shuffle(validCandidates);
        Model model = validCandidates[0];

        if (!model.IsLoaded) return false;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vector3 spawnPos = oldVeh.Position;
        Vehicle newVeh = World.CreateVehicle(model, spawnPos, oldVeh.Heading);

        if (newVeh != null && newVeh.Exists())
        {
            // Briefly hide while we snap into place
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, false, 0);

            // Attempt to obtain ground Z using the non-obsolete API.
            // Use a safe fallback to the spawn Z if anything goes wrong.
            float groundZ;
            try
            {
                // New API: GetGroundHeight(Vector3, out float, GetGroundHeightMode)
                // Use numeric cast for mode to avoid depending on a specific enum member name.
                World.GetGroundHeight(spawnPos, out groundZ, (GetGroundHeightMode)0);
            }
            catch
            {
                groundZ = spawnPos.Z;
            }

            // Ensure collision enabled before placing so physics can immediately resolve.
            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);

            // Place without offset to avoid incremental physics nudges.
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, newVeh, spawnPos.X, spawnPos.Y, groundZ, false, false, true);

            // If the old vehicle was essentially stopped, force-on-ground to stabilize vehicle.
            if (oldVeh.Speed < 1.0f)
            {
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newVeh);
            }

            // Transfer kinematic state
            newVeh.Velocity = oldVeh.Velocity;
            newVeh.ForwardSpeed = oldVeh.Speed;
            newVeh.IsEngineRunning = oldVeh.IsEngineRunning;

            // Mark decor then put driver in
            Function.Call(Hash.DECOR_SET_INT, newVeh, AMB_TAG, 1);
            driver.SetIntoVehicle(newVeh, VehicleSeat.Driver);

            // Safe removal of the old vehicle (don't teleport it away, just delete)
            try
            {
                oldVeh.IsEngineRunning = false;
                oldVeh.Delete();
            }
            catch { }

            // Reveal and ensure collision is active.
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, true, 0);
            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);

            // Let AI drive (preserve previous drive style)
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            if (_debugMode) AddDebugBlip(newVeh);

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            _recentSwaps.Add(newVeh.Handle);
            _spawnHistory.Add(model.Hash);
            if (_spawnHistory.Count > _historyDepth) _spawnHistory.RemoveAt(0);

            return true;
        }

        return false;
    }

    // Fisher-Yates shuffle improves uniformity of random selection over OrderBy(_rnd.Next()).
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

    // NEW SCORING METHOD: Prioritizes Duplicates
    private float GetDeduplicationScore(Vehicle v, Vector3 camPos, Vector3 camDir, bool isBlocked, bool isDistantCandidate, bool isDuplicate)
    {
        Vector3 vPos = v.Position;
        float dist = vPos.DistanceTo(camPos);

        // 1. Base Score
        float score = 0f;

        // 2. Duplicate Bonus (The most important factor)
        // If it's a duplicate, we boost it significantly so it beats the threshold
        if (isDuplicate) score += 600f;
        else score -= 200f; // Penalty for unique cars

        // 3. Distance Bonus
        if (isDistantCandidate && v.IsOnScreen)
            return 1000f + (dist / 10f); // Always swap horizon cars

        // 4. Visibility Modifiers
        if (v.IsOnScreen)
        {
            score += 300f; // "Easy Access" bonus
        }
        else
        {
            score += 100f;
            if (isBlocked) score += 200f; // "Hidden" bonus - very safe swap
            else
            {
                // Strict Angle Check
                Vector3 toCar = (vPos - camPos).Normalized;
                float angle = Vector3.Angle(camDir, toCar);
                if (angle < 70f) return 0f; // Must be in periphery
            }
        }

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
            foreach (var b in _debugBlips) { if (b.Exists()) b.Alpha = 255; else b.Alpha = 0; }
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _debugBlips) if (b.Exists()) b.Delete();
    }

    private void AddDebugBlip(Vehicle v)
    {
        if (v.AttachedBlip != null) return;
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.Green;
        b.Scale = 0.6f;
        b.Name = "Ambient Swap";
        b.IsShortRange = true;
        Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false);
        _debugBlips.Add(b);
    }

    private void CleanupBlips()
    {
        for (int i = _debugBlips.Count - 1; i >= 0; i--)
        {
            Blip b = _debugBlips[i];
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists())
            {
                if (b.Exists()) b.Delete();
                _debugBlips.RemoveAt(i);
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
}