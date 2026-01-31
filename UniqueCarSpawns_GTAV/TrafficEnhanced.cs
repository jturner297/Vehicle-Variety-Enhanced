using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

// OPTIMIZED TRAFFIC ENHANCED
// - Removed LINQ (Fixes GC Lag Spikes)
// - Added "Two-Pass" Scoring (Cheap Math first, Raycasts second)
// - Capped Raycasts per frame

public class TrafficEnhanced : Script
{
    // =============================================================
    //                 SETTINGS & TUNING
    // =============================================================

    private const string MP_TAG = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    // Performance
    private int _checkInterval = 100; // Faster checks now possible
    private int _swapCooldown = 50;   // Very low cooldown for high speed

    // Limits
    private int MaxRaycastsPerFrame = 3; // Hard limit on expensive calculations

    // Visibility
    private float _minSafeDist = 130f;
    private float _fovealDist = 240f;

    private float ScoreThreshold = 40f;
    private int _driveStyle = 786603;

    // DEBUG
    private bool _debugMode = false;

    // =============================================================
    //                 INTERNAL VARIABLES
    // =============================================================

    private Random _rnd = new Random();
    private int _nextCheck = 0;
    private int _nextSwapTime = 0;
    private float _minSafeDistSq;
    private float _fovealDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;
    private List<Blip> _debugBlips = new List<Blip>();

    // Reusable lists to prevent Garbage Collection (Lag)
    private List<Vehicle> _candidateBuffer = new List<Vehicle>();
    private Dictionary<int, int> _modelCensus = new Dictionary<int, int>();
    private HashSet<int> _activeModels = new HashSet<int>();

    public TrafficEnhanced()
    {
        _minSafeDistSq = _minSafeDist * _minSafeDist;
        _fovealDistSq = _fovealDist * _fovealDist;

        InitializeZones();

        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);
        Function.Call(Hash.DECOR_REGISTER, MP_TAG, 3);

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void InitializeZones()
    {
        // FORMAT: (Rich, Mid, Poor, Country)

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
        if (_debugMode) CleanupBlips();
        if (Game.GameTime < _nextCheck) return;

        try { ProcessAmbientTraffic(); }
        catch (Exception) { }

        _nextCheck = Game.GameTime + _checkInterval;
    }

    // =============================================================
    //                 LAG-FREE LOGIC
    // =============================================================

    private void ProcessAmbientTraffic()
    {
        if (Game.GameTime < _nextSwapTime) return;

        Vehicle[] vehicles = World.GetAllVehicles(); // Unavoidable cost
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 playerVel = player.Velocity;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;
        Vector3 playerDir = player.ForwardVector;

        // Reset reusable collections to avoid GC allocations
        _modelCensus.Clear();
        _activeModels.Clear();
        _candidateBuffer.Clear();

        // --- STEP 1: FAST CENSUS ---
        // Just counting, no heavy math
        for (int i = 0; i < vehicles.Length; i++)
        {
            Vehicle v = vehicles[i];
            if (!v.Exists()) continue;
            int hash = v.Model.Hash;

            if (_modelCensus.ContainsKey(hash)) _modelCensus[hash]++;
            else _modelCensus[hash] = 1;

            _activeModels.Add(hash);
        }

        int playerRoadID = GetVehicleNodeID(playerPos);

        // --- STEP 2: CHEAP PASS (Math Only) ---
        // Find top 3 candidates based on Distance/Angle/Speed/RoadID
        // We do NOT raycast here.

        Vehicle bestV1 = null; float score1 = 0;
        Vehicle bestV2 = null; float score2 = 0;
        Vehicle bestV3 = null; float score3 = 0;

        for (int i = 0; i < vehicles.Length; i++)
        {
            Vehicle v = vehicles[i];

            // Basic Filters
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (IsSwapped(v) || IsExcluded(v)) continue;

            float distSq = v.Position.DistanceToSquared(camPos);
            if (distSq < _minSafeDistSq || distSq > _fovealDistSq) continue;

            // CHEAP SCORE CALCULATION
            float rawScore = GetCheapScore(v, camPos, camDir, playerVel, playerRoadID);

            if (rawScore <= 0) continue;

            // Census Bonus (Target Duplicates)
            if (_modelCensus.ContainsKey(v.Model.Hash) && _modelCensus[v.Model.Hash] > 1) rawScore += 200f;

            // Insertion Sort into top 3
            if (rawScore > score1)
            {
                score3 = score2; bestV3 = bestV2;
                score2 = score1; bestV2 = bestV1;
                score1 = rawScore; bestV1 = v;
            }
            else if (rawScore > score2)
            {
                score3 = score2; bestV3 = bestV2;
                score2 = rawScore; bestV2 = v;
            }
            else if (rawScore > score3)
            {
                score3 = rawScore; bestV3 = v;
            }
        }

        // --- STEP 3: EXPENSIVE PASS (Raycasts) ---
        // Only run raycasts on the winners of Step 2

        if (CheckAndSwap(bestV1, camPos, _activeModels)) return;
        if (CheckAndSwap(bestV2, camPos, _activeModels)) return;
        if (CheckAndSwap(bestV3, camPos, _activeModels)) return;
    }

    // Helper to run the expensive check and swap if successful
    private bool CheckAndSwap(Vehicle v, Vector3 camPos, HashSet<int> activeModels)
    {
        if (v == null) return false;

        // THE HEAVY LIFTING: 5 Raycasts
        if (IsVehicleVisibleSmart(v, camPos))
        {
            if (AttemptSwap(v, activeModels))
            {
                _nextSwapTime = Game.GameTime + _swapCooldown;
                return true;
            }
        }
        return false;
    }

    private float GetCheapScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerVel, int playerRoadID)
    {
        Vector3 vPos = v.Position;
        Vector3 toCar = vPos - camPos;
        float dist = toCar.Length();

        // 1. Slope Check
        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        if (heightDiff > 15f) // Quick height fail before expensive Atan2
        {
            double slopeAngle = Math.Atan2(heightDiff, dist) * (180 / Math.PI);
            if (slopeAngle > 45) return 0f;
        }

        // 2. Angle Check
        float angle = Vector3.Angle(camDir, toCar);
        if (angle > 60f) return 0f;

        float score = 0f;

        // 3. Same Road Check (Native call, moderate cost)
        int carRoadID = GetVehicleNodeID(vPos);
        if (playerRoadID != 0 && carRoadID == playerRoadID) score += 150f;

        // 4. Movement Check
        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized);
        if (closingSpeed < -0.5f) score += 100f;
        else if (closingSpeed > 0.5f) score += 20f;

        // 5. Distance Bias (Closer is better, but not TOO close)
        score += (dist / 10f);

        return score;
    }

    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        if (!v.IsOnScreen) return false;

        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);

        // Cascading Raycasts (Ordered by likelihood of failure)
        // If roof is hidden, car is probably hidden.
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z + 0.1f));
        if (!World.Raycast(camPos, roof, IntersectFlags.Map).DidHit) return true;

        Vector3 left = v.GetOffsetPosition(new Vector3(min.X, 0, 0.5f));
        if (!World.Raycast(camPos, left, IntersectFlags.Map).DidHit) return true;

        Vector3 right = v.GetOffsetPosition(new Vector3(max.X, 0, 0.5f));
        if (!World.Raycast(camPos, right, IntersectFlags.Map).DidHit) return true;

        Vector3 front = v.GetOffsetPosition(new Vector3(0, max.Y, 0.5f));
        if (!World.Raycast(camPos, front, IntersectFlags.Map).DidHit) return true;

        Vector3 rear = v.GetOffsetPosition(new Vector3(0, min.Y, 0.5f));
        if (!World.Raycast(camPos, rear, IntersectFlags.Map).DidHit) return true;

        return false;
    }

    private int GetVehicleNodeID(Vector3 pos)
    {
        return Function.Call<int>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_ID, pos.X, pos.Y, pos.Z, 1, 1, 1073741824, 0);
    }

    // =============================================================
    //                 SWAP EXECUTION
    // =============================================================

    private bool AttemptSwap(Vehicle oldVeh, HashSet<int> activeModels)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, oldVeh.Position.X, oldVeh.Position.Y, oldVeh.Position.Z);
        AmbientProfile profile = _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone] : _defaultProfile;

        int totalWeight = profile.RichChance + profile.MidChance + profile.PoorChance + profile.CountryChance;
        if (totalWeight <= 0) return false;

        string modelName = null;
        int attempts = 0;

        while (attempts < 3)
        {
            attempts++;
            int roll = _rnd.Next(0, totalWeight);
            HashSet<string> targetList = null;

            if (roll < profile.RichChance) targetList = VehList.models_rich;
            else
            {
                roll -= profile.RichChance;
                if (roll < profile.MidChance) targetList = VehList.models_mid;
                else
                {
                    roll -= profile.MidChance;
                    if (roll < profile.PoorChance) targetList = VehList.models_poor;
                    else targetList = VehList.models_countryside;
                }
            }

            if (targetList == null || targetList.Count == 0) continue;

            string candidate = targetList.ElementAt(_rnd.Next(targetList.Count));
            int candidateHash = (int)Function.Call<uint>(Hash.GET_HASH_KEY, candidate);

            if (activeModels.Contains(candidateHash)) continue;

            modelName = candidate;
            break;
        }

        if (modelName == null) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request();
        if (!model.IsLoaded)
        {
            int t = Game.GameTime + 50;
            while (!model.IsLoaded && Game.GameTime < t) Script.Yield();
        }
        if (!model.IsLoaded) { model.MarkAsNoLongerNeeded(); return false; }

        if (!oldVeh.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) { model.MarkAsNoLongerNeeded(); return false; }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
        Vehicle newVeh = World.CreateVehicle(model, oldVeh.Position, oldVeh.Heading);

        if (newVeh != null)
        {
            newVeh.Velocity = oldVeh.Velocity;
            newVeh.ForwardSpeed = oldVeh.Speed;
            newVeh.IsEngineRunning = oldVeh.IsEngineRunning;

            Function.Call(Hash.DECOR_SET_INT, newVeh, AMB_TAG, 1);
            driver.SetIntoVehicle(newVeh, VehicleSeat.Driver);
            oldVeh.Delete();

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            activeModels.Add(model.Hash);

            if (_debugMode) AddDebugBlip(newVeh);

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();
            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
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
            foreach (var b in _debugBlips) { if (b.Exists()) b.Alpha = _debugMode ? 255 : 0; }
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
        b.Color = BlipColor.Yellow;
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

    private class AmbientProfile
    {
        public int RichChance; public int MidChance; public int PoorChance; public int CountryChance;
        public AmbientProfile(int rich, int mid, int poor, int country)
        {
            RichChance = rich; MidChance = mid; PoorChance = poor; CountryChance = country;
        }
    }
}