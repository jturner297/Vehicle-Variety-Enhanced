using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public class TrafficEnhanced : Script
{
    // =============================================================
    //                 SETTINGS & TUNING
    // =============================================================

    private const string MP_TAG = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    // Performance & Throttling
    private int _checkInterval = 250; // How often to SCAN (Cpu saver)
    private int _swapCooldown = 0;    // How long to WAIT after a successful swap

    // Visibility Logic (Static Distances)
    private float _minSafeDist = 130f; // Absolute minimum swap distance
    private float _fovealDist = 240f;  // Max distance for high-detail swapping

    // Limits
    private int MaxSwapsPerCycle = 1;
    private float ScoreThreshold = 40f;

    // Driving Style (Normal + Avoids Traffic)
    private int _driveStyle = 786603;

    // DEBUG
    private bool _debugMode = false;

    // =============================================================
    //                 INTERNAL VARIABLES
    // =============================================================

    private Random _rnd = new Random();
    private int _nextCheck = 0;
    private int _nextSwapTime = 0;

    // Optimization: Pre-calculated squares
    private float _minSafeDistSq;
    private float _fovealDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;

    private List<Blip> _debugBlips = new List<Blip>();

    public TrafficEnhanced()
    {
        _minSafeDistSq = _minSafeDist * _minSafeDist;
        _fovealDistSq = _fovealDist * _fovealDist;

        InitializeZones();

        // Register Decorators
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);
        Function.Call(Hash.DECOR_REGISTER, MP_TAG, 3);

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    // =============================================================
    //                 ZONE CONFIGURATION
    // =============================================================

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
    //                 SMART SWAPPING LOGIC
    // =============================================================

    private void ProcessAmbientTraffic()
    {
        if (Game.GameTime < _nextSwapTime) return;

        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 playerVel = player.Velocity;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        // Get Player's Road ID for "Same Road" logic
        int playerRoadID = GetVehicleNodeID(playerPos);

        // --- CENSUS: Count existing models to prevent duplicates ---
        Dictionary<int, int> modelCensus = new Dictionary<int, int>();
        HashSet<int> activeModels = new HashSet<int>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists()) continue;
            int hash = v.Model.Hash;
            if (modelCensus.ContainsKey(hash)) modelCensus[hash]++;
            else modelCensus[hash] = 1;
            activeModels.Add(hash);
        }

        // --- FIND CANDIDATES ---
        List<ScoredVehicle> candidates = new List<ScoredVehicle>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (IsSwapped(v) || IsExcluded(v)) continue;

            float distSq = v.Position.DistanceToSquared(camPos);
            if (distSq < _minSafeDistSq || distSq > _fovealDistSq) continue;

            // Score with Super Raycast & Road Checks
            float score = GetCinematicScore(v, camPos, camDir, player.ForwardVector, playerVel, playerRoadID);

            if (score <= 0) continue;

            // Bonus: Swap duplicates of existing cars aggressively
            if (modelCensus.ContainsKey(v.Model.Hash) && modelCensus[v.Model.Hash] > 1)
            {
                score += 200f;
            }

            if (score > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = score });
            }
        }

        // --- EXECUTE SWAPS ---
        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);

        foreach (var choice in bestChoices)
        {
            // Pass 'activeModels' to ensure we don't spawn a car that is already here
            if (AttemptSwap(choice.Vehicle, activeModels))
            {
                _nextSwapTime = Game.GameTime + _swapCooldown;
                break;
            }
        }
    }

    private float GetCinematicScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir, Vector3 playerVel, int playerRoadID)
    {
        Vector3 vPos = v.Position;
        float dist = vPos.DistanceTo(camPos);

        // 1. SLOPE CHECK (Bridge protection)
        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        double slopeAngle = Math.Atan2(heightDiff, dist) * (180 / Math.PI);
        if (slopeAngle > 45) return 0f;

        // 2. FOV CHECK
        Vector3 toCar = (vPos - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);
        if (angle > 60f) return 0f;

        // 3. LOGIC BONUSES
        float score = 0f;

        // Same Road Bonus
        int carRoadID = GetVehicleNodeID(vPos);
        if (playerRoadID != 0 && carRoadID == playerRoadID) score += 150f;

        // Movement Bonus
        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerVel.Normalized);
        if (closingSpeed < -0.5f) score += 100f; // Oncoming
        else if (closingSpeed > 0.5f) score += 20f;  // Overtake

        // 4. SUPER RAYCAST (Visibility)
        if (IsVehicleVisibleSmart(v, camPos))
        {
            score += 50f;
            score += (dist / 10f);
        }
        else
        {
            return 0f; // Invisible = worthless
        }

        return score;
    }

    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        if (!v.IsOnScreen) return false;

        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);

        // Check Roof, Front, Rear, Left, Right
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z + 0.1f));
        if (!World.Raycast(camPos, roof, IntersectFlags.Map).DidHit) return true;

        Vector3 front = v.GetOffsetPosition(new Vector3(0, max.Y, 0.5f));
        if (!World.Raycast(camPos, front, IntersectFlags.Map).DidHit) return true;

        Vector3 rear = v.GetOffsetPosition(new Vector3(0, min.Y, 0.5f));
        if (!World.Raycast(camPos, rear, IntersectFlags.Map).DidHit) return true;

        Vector3 left = v.GetOffsetPosition(new Vector3(min.X, 0, 0.5f));
        if (!World.Raycast(camPos, left, IntersectFlags.Map).DidHit) return true;

        Vector3 right = v.GetOffsetPosition(new Vector3(max.X, 0, 0.5f));
        if (!World.Raycast(camPos, right, IntersectFlags.Map).DidHit) return true;

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

        // Try 3 times to find a NON-DUPLICATE vehicle
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

            // Pick random from list
            string candidate = targetList.ElementAt(_rnd.Next(targetList.Count));
            int candidateHash = (int)Function.Call<uint>(Hash.GET_HASH_KEY, candidate);

            // CENSUS CHECK: Is this car already here?
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
            // Quick wait
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

            // Add to active models so we don't spawn it again this frame
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

    // =============================================================
    //                 HELPERS
    // =============================================================

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