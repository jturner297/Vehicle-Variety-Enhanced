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
    private int _checkInterval = 250;
    private int _swapCooldown = 0;

    // --- DISTANCE TUNING (AGGRESSIVE) ---
    // We push the "Min" out to 180m so swaps are tiny on screen.
    private float _minSafeDist = 15f;
    private float _fovealDist = 350f;

    // Limits
    private int MaxSwapsPerCycle = 1;
    private float ScoreThreshold = 100f;

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

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;

    private List<Blip> _debugBlips = new List<Blip>();
    private HashSet<int> _recentSwaps = new HashSet<int>();

    public TrafficEnhanced()
    {
        // Calculate squares once for performance
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
        if (Game.GameTime > _cleanupTimer)
        {
            _recentSwaps.RemoveWhere(h => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h));
            _cleanupTimer = Game.GameTime + 10000;
            if (_debugMode) CleanupBlips();
        }

        if (Game.GameTime < _nextCheck) return;

        try { ProcessAmbientTraffic(); }
        catch (Exception) { }

        _nextCheck = Game.GameTime + _checkInterval;
    }

    private void ProcessAmbientTraffic()
    {
        if (Game.GameTime < _nextSwapTime) return;

        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 playerVel = player.Velocity;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        int playerRoadID = GetVehicleNodeID(playerPos);

        List<ScoredVehicle> candidates = new List<ScoredVehicle>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;
            if (_recentSwaps.Contains(v.Handle)) continue;
            if (IsSwapped(v) || IsExcluded(v)) continue;

            float distSq = v.Position.DistanceToSquared(camPos);
            // STRICT DISTANCE CHECK
            if (distSq < _minSafeDistSq || distSq > _fovealDistSq) continue;

            float score = GetCinematicScore(v, camPos, camDir, player.ForwardVector, playerVel, playerRoadID);
            if (score <= 0) continue;

            if (score > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = score });
            }
        }

        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);

        foreach (var choice in bestChoices)
        {
            if (AttemptSwap(choice.Vehicle))
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

        // --- 1. PRESERVED: Height/Slope Check ---
        float heightDiff = Math.Abs(vPos.Z - camPos.Z);
        double slopeAngle = Math.Atan2(heightDiff, dist) * (180 / Math.PI);
        if (slopeAngle > 45) return 0f;

        // --- 2. NEW: "Just Passed" Filter ---
        // If the car is behind us and close, we likely just saw it. Don't touch it.
        Vector3 toCar = (vPos - camPos).Normalized;
        float dotProduct = Vector3.Dot(camDir, toCar); // 1.0 is front, -1.0 is back

        // If it is BEHIND the camera (dot < 0) AND within 60 meters
        if (dotProduct < 0 && dist < 60f)
        {
            return 0f; // We just passed this guy. Leave him alone.
        }

        // --- 3. HEAVY RESOURCE USAGE: The Raycast Check ---
        // We check this for EVERY candidate now. 
        // Is the car physically blocked by the world (buildings, walls, other trucks)?
        bool isPhysicallyBlocked = !IsVehicleVisibleSmart(v, camPos);

        // --- 4. SCORING LOGIC ---
        float score = 0f;

        if (v.IsOnScreen)
        {
            // CASE A: Car is On-Screen
            if (isPhysicallyBlocked)
            {
                // It is on screen, but a wall/bus is blocking our view.
                // This is the PERFECT swap. High density, zero pop-in.
                score += 300f;
            }
            else
            {
                // It is On-Screen and Openly Visible.
                // HARD REJECT. Never swap a car we can see.
                return 0f;
            }
        }
        else
        {
            // CASE B: Car is Off-Screen
            // Since it's off-screen, we don't *need* it to be blocked by a wall, 
            // but if it IS blocked, it's even safer (in case we whip the camera around).

            score += 100f; // Base score for being off-screen

            if (isPhysicallyBlocked)
            {
                score += 50f; // Extra safety bonus
            }

            // Peripheral Safety Check (The "Edge" Buffer)
            // If it's NOT blocked by a wall, we need to make sure it's not 
            // hovering 1 inch outside our monitor bezel.
            if (!isPhysicallyBlocked)
            {
                // If we can "see" it (line of sight is clear), but it's just barely off-screen...
                // We require it to be further away or at a wider angle to prevent "Edge Pop".
                float angle = Vector3.Angle(camDir, toCar);
                if (angle < 55f) return 0f; // Too close to the edge of the monitor
            }
        }

        // --- 5. Distance Weighting ---
        // If we are fully blocked/hidden, we actually want CLOSER cars 
        // because they have the highest impact on density.
        if (isPhysicallyBlocked)
        {
            // Inverse distance bonus: Closer = Higher Score
            score += (100f / (dist + 1f));
        }
        else
        {
            // If not blocked (just off-screen), prefer slight distance
            score += (dist / 10f);
        }

        return score;
    }

    private bool IsVehicleVisibleSmart(Vehicle v, Vector3 camPos)
    {
        // If the game engine says it's off screen, we still might want to know if 
        // line-of-sight is blocked (for safety), so we run the raycasts anyway.

        // Test the ROOF (Most common visibility point)
        Vector3 min, max;
        v.Model.GetDimensions(out min, out max);
        Vector3 roof = v.GetOffsetPosition(new Vector3(0, 0, max.Z));

        // Raycast: Camera -> Car Roof
        var result = World.Raycast(camPos, roof, IntersectFlags.Map | IntersectFlags.Vehicles);

        // If we hit something...
        if (result.DidHit)
        {
            // AND that something is NOT the car itself...
            if (result.HitEntity != v)
            {
                return false; // View is blocked by a wall or another car!
            }
        }

        // (Repeat for Bumper/Sides if you want "Heavy" checking, 
        // but Roof is usually the best indicator for traffic)

        return true; // Line of sight is clear
    }

    private int GetVehicleNodeID(Vector3 pos)
    {
        return Function.Call<int>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_ID, pos.X, pos.Y, pos.Z, 1, 1, 1073741824, 0);
    }

    private bool AttemptSwap(Vehicle oldVeh)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, oldVeh.Position.X, oldVeh.Position.Y, oldVeh.Position.Z);
        AmbientProfile profile = _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone] : _defaultProfile;

        int totalWeight = profile.RichChance + profile.MidChance + profile.PoorChance + profile.CountryChance;
        if (totalWeight <= 0) return false;

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

        if (targetList == null || targetList.Count == 0) return false;

        string modelName = targetList.ElementAt(_rnd.Next(targetList.Count));

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

        // Spawn Invisible & Off-Ground to prevent physics collision
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

            // Warp Old Vehicle to Hell (Z -500)
            oldVeh.Position = new Vector3(oldVeh.Position.X, oldVeh.Position.Y, -500f);
            oldVeh.Delete();

            Function.Call(Hash.SET_ENTITY_COLLISION, newVeh, true, true);
            Function.Call(Hash.SET_ENTITY_VISIBLE, newVeh, true, 0);

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

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