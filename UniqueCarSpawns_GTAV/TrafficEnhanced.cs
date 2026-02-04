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
    private int _swapCooldown = 500;

    // --- DISTANCE TUNING ---
    private float _minSafeDist = 15f;
    private float _fovealDist = 350f;

    // Distance at which we force a swap even if the car is visible
    private bool _enableOnScreenSwap = true;
    private float _OnScreenSwapDist = 250f;

    // Limits
    private int MaxSwapsPerCycle = 2;
    private float ScoreThreshold = 100f;

    // MEMORY CAP: Keep 30 models ready in RAM
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

    // NEW: Tracks the active profile object to prevent memory thrashing
    private AmbientProfile _currentProfile;

    private List<Blip> _debugBlips = new List<Blip>();

    // MEMORY SYSTEMS
    private HashSet<int> _recentSwaps = new HashSet<int>();
    private HashSet<int> _permanentBlacklist = new HashSet<int>();

    // NEW: Background Loader Variables
    private string _currentZoneLabel = "";
    private List<Model> _hotMemoryList = new List<Model>();
    private Queue<string> _loadQueue = new Queue<string>();
    private int _loadingTicker = 0;

    // NEW: Anti-Clustering History
    private List<int> _spawnHistory = new List<int>();
    private int _historyDepth = 14;

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

        // 1. Identify the profile for the current location
        AmbientProfile activeProfile = _zoneRegistry.ContainsKey(zoneCode) ? _zoneRegistry[zoneCode] : _defaultProfile;

        // 2. CHECK PROFILE REFERENCE instead of Zone Name string
        // This prevents memory dumping when moving between zones that share the same list (e.g. Davis -> Rancho)
        if (activeProfile != _currentProfile)
        {
            _currentProfile = activeProfile;
            _currentZoneLabel = zoneCode;

            _loadQueue.Clear();

            // VARIETY HACK: Dump memory only when the PROFILE actually changes
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

        // AGGRESSIVE REFILL: If we have less than 10 queued, grab more immediately
        if (_loadQueue.Count < 10)
        {
            AddToLoadQueue(zoneCode);
        }

        _loadingTicker++;
        // TURBO SPEED: Process a new model every 3 ticks
        if (_loadingTicker > 3)
        {
            _loadingTicker = 0;
            if (_loadQueue.Count > 0)
            {
                // ROTATION: If memory is full, DELETE THE OLDEST immediately
                if (_hotMemoryList.Count >= _memoryCap)
                {
                    var oldModel = _hotMemoryList[0];
                    oldModel.MarkAsNoLongerNeeded();
                    _hotMemoryList.RemoveAt(0);
                }

                string modelName = _loadQueue.Dequeue();
                Model m = new Model(modelName);

                // Load it
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
            if (distSq < _minSafeDistSq || distSq > _fovealDistSq) continue;

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

            float score = GetCinematicScore(v, camPos, camDir, player.ForwardVector, player.Velocity, 0, isBlocked, isDistantCandidate);
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

    private bool AttemptSwap(Vehicle oldVeh, List<Model> readyModels)
    {
        // 1. Filter out models we have spawned recently (History Check)
        var validCandidates = readyModels.Where(m => !_spawnHistory.Contains(m.Hash)).ToList();

        // 2. Fallback
        if (validCandidates.Count == 0) validCandidates = readyModels;

        // 3. Pick random
        Model model = validCandidates[_rnd.Next(validCandidates.Count)];

        if (!model.IsLoaded) return false;

        if (!oldVeh.Exists()) return false;
        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) return false;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vector3 spawnPos = oldVeh.Position + new Vector3(0, 0, 0.2f);
        Vehicle newVeh = World.CreateVehicle(model, spawnPos, oldVeh.Heading);

        if (newVeh != null)
        {
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, false, 0);
            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, false, false);
            _recentSwaps.Add(newVeh.Handle);

            newVeh.Velocity = oldVeh.Velocity;
            newVeh.ForwardSpeed = oldVeh.Speed;
            newVeh.IsEngineRunning = oldVeh.IsEngineRunning;

            Function.Call(Hash.DECOR_SET_INT, newVeh, AMB_TAG, 1);
            driver.SetIntoVehicle(newVeh, VehicleSeat.Driver);

            oldVeh.Position = new Vector3(oldVeh.Position.X, oldVeh.Position.Y, -500f);
            oldVeh.Delete();

            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, true, 0);
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            if (_debugMode) AddDebugBlip(newVeh);

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            // Update History
            _spawnHistory.Add(model.Hash);
            if (_spawnHistory.Count > _historyDepth)
            {
                _spawnHistory.RemoveAt(0);
            }

            return true;
        }

        return false;
    }

    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir, Vector3 playerVel, int playerRoadID, bool isBlocked, bool isDistantCandidate)
    {
        Vector3 vPos = v.Position;
        float dist = vPos.DistanceTo(camPos);

        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        if (heightDiff > 15f) return 0f;

        Vector3 toCar = (vPos - camPos).Normalized;

        float score = 0f;

        if (isDistantCandidate && v.IsOnScreen)
            return 1000f + (dist / 10f);

        if (v.IsOnScreen) score += 300f;
        else
        {
            score += 100f;
            if (isBlocked) score += 50f;
            else
            {
                float angle = Vector3.Angle(camDir, toCar);
                if (angle < 55f) return 0f;
            }
        }

        if (isBlocked) score += (100f / (dist + 1f));
        else score += (dist / 10f);

        return score;
    }

    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z));
        var result = World.Raycast(camPos, roof, IntersectFlags.Map | IntersectFlags.Vehicles);
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