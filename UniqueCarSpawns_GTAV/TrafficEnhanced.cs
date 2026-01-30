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

    // Performance
    private int _checkInterval = 1000; // Check every second
    private int _swapChance = 30;      // 10% Chance. We are ASSISTING, not replacing.

    // Visibility Logic (Static Distances)
    private float _minSafeDist = 130f; // Absolute minimum swap distance
    private float _fovealDist = 240f;  // Max distance for high-detail swapping
    private float _periphDist = 60f;   // Peripheral vision safety buffer

    private int MaxSwapsPerCycle = 3; // Allows more scale than MP, but prevents lag
    private float ScoreThreshold = 40f;
    // Driving Style
    private int _driveStyle = 786603;

    // DEBUG
    private bool _debugMode = false;

    // =============================================================

    private Random _rnd = new Random();
    private int _nextCheck = 0;

    private float _minSafeDistSq;
    private float _fovealDistSq;
    private float _periphDistSq;

    private Dictionary<string, AmbientProfile> _zoneRegistry = new Dictionary<string, AmbientProfile>();
    private AmbientProfile _defaultProfile;

    private List<Blip> _debugBlips = new List<Blip>();

    public TrafficEnhanced()
    {
        // Pre-calculate squares to avoid Sqrt() calls in the loop
        _minSafeDistSq = _minSafeDist * _minSafeDist;
        _fovealDistSq = _fovealDist * _fovealDist;
        _periphDistSq = _periphDist * _periphDist;

        InitializeZones();

        // REGISTRATION: Register OUR tag AND the TrafficMP tag to ensure visibility
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);
        Function.Call(Hash.DECOR_REGISTER, MP_TAG, 3);

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void InitializeZones()
    {
        AmbientProfile Hippy = new AmbientProfile(10, 50, 40);
        AmbientProfile Gangster = new AmbientProfile(10, 30, 60);
        AmbientProfile Downtown = new AmbientProfile(35, 35, 30);
        AmbientProfile Vinewood = new AmbientProfile(40, 35, 15);
        AmbientProfile Coastal = new AmbientProfile(60, 30, 10);
        AmbientProfile Elite = new AmbientProfile(65, 25, 10);
        AmbientProfile VinewoodHills = new AmbientProfile(85, 10, 5);
        AmbientProfile Industry = new AmbientProfile(5, 45, 50);
        AmbientProfile CountrySide = new AmbientProfile(5, 40, 60);
        _defaultProfile = new AmbientProfile(15, 60, 25);

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

    private bool IsHidden(Vehicle v, float distSq, Vector3 camPos, Vector3 camDir)
    {
        // A. ENGINE CHECK
        // If the game engine says the car isn't on screen, trust it.
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f))
            return true;

        // B. DISTANCE CHECK
        // If it's super far away (240m+), it's just a blurry LOD. Safe to swap.
        if (distSq > _fovealDistSq) return true;

        // C. PERIPHERAL CHECK
        // If it's in the corner of the eye (Angle > 35) AND reasonably far (60m+).
        Vector3 toCar = (v.Position - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);
        if (angle > 35.0f && distSq > _periphDistSq) return true;

        // D. OCCLUSION CHECK
        // Raycast is expensive, so we do it last. Is there a building between us?
        return World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit;
    }

    private bool IsSwapped(Vehicle v)
    {
        // Returns true if tagged by US (TrafficEnhanced) or the HERO SCRIPT (TrafficMP)
        // If DecorInt is 0, it means we checked it and decided to keep it vanilla.
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG)) return true;
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, MP_TAG)) return true;
        return false;
    }

    private void AttemptSwap(Vehicle oldVeh)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, oldVeh.Position.X, oldVeh.Position.Y, oldVeh.Position.Z);
        AmbientProfile profile = _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone] : _defaultProfile;

        int roll = _rnd.Next(0, 100);
        HashSet<string> targetList;

        if (roll < profile.RichChance) targetList = VehList.models_rich;
        else if (roll < profile.RichChance + profile.MidChance) targetList = VehList.models_mid;
        else targetList = VehList.models_poor;

        string modelName = VehicleSelector.GetNext(targetList, "Ambient");
        if (modelName == null) return;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return;

        // Request with short timeout to prevent lag
        model.Request();
        int timeout = Game.GameTime + 100; // 100ms max wait
        while (!model.IsLoaded && Game.GameTime < timeout) Script.Yield();
        if (!model.IsLoaded) { model.MarkAsNoLongerNeeded(); return; }

        if (!oldVeh.Exists()) { model.MarkAsNoLongerNeeded(); return; }

        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) { model.MarkAsNoLongerNeeded(); return; }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vehicle newVeh = World.CreateVehicle(model, oldVeh.Position, oldVeh.Heading);

        if (newVeh != null)
        {
            newVeh.Velocity = oldVeh.Velocity;
            newVeh.ForwardSpeed = oldVeh.Speed;
            newVeh.IsEngineRunning = oldVeh.IsEngineRunning;

            // Tag as "Swapped" (1)
            Function.Call(Hash.DECOR_SET_INT, newVeh, AMB_TAG, 1);

            driver.SetIntoVehicle(newVeh, VehicleSeat.Driver);
            oldVeh.Delete();

            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVeh, 20.0f, _driveStyle);

            if (_debugMode) AddDebugBlip(newVeh);

            newVeh.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();
        }
        else
        {
            driver.MarkAsNoLongerNeeded();
            model.MarkAsNoLongerNeeded();
        }
    }

    private void ProcessAmbientTraffic()
    {
        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        // Use a list to find the best candidates this tick
        List<ScoredVehicle> candidates = new List<ScoredVehicle>();

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;

            // Critical Check: Is it already swapped or protected?
            if (IsSwapped(v) || IsExcluded(v)) continue;

            float distSq = v.Position.DistanceToSquared(playerPos);

            // Safety: Still use the "Hidden" check to prevent popping
            if (distSq < _minSafeDistSq) continue;
            if (!IsHidden(v, distSq, camPos, camDir)) continue;

            // Apply TrafficMP's scoring logic
            float score = CalculateDirectorScore(v, camPos, camDir, player.ForwardVector);

            if (score > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = score });
            }
        }

        // Sort by best score and swap the top few
        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);

        foreach (var choice in bestChoices)
        {
            AttemptSwap(choice.Vehicle);
        }
    }

    // 3. THE DIRECTOR SCORING FUNCTION
    private float CalculateDirectorScore(Vehicle v, Vector3 camPos, Vector3 camDir, Vector3 playerDir)
    {
        float score = 0f;
        float dist = v.Position.DistanceTo(camPos);

        Vector3 toCar = (v.Position - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);

        // Bonus for being in front of the camera but far enough away
        if (angle < 40f) score += 30f;

        // Bonus for oncoming traffic (Cinematic feel)
        float closingSpeed = Vector3.Dot(v.Velocity.Normalized, playerDir.Normalized);
        if (closingSpeed < -0.5f) score += 50f;

        // Bonus for distance (higher distance = safer swap)
        score += (dist / 10f);

        return score;
    }

    private struct ScoredVehicle
    {
        public Vehicle Vehicle;
        public float Score;
    }
    // =============================================================
    //                 DEBUG & UTILS
    // =============================================================

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F10)
        {
            _debugMode = !_debugMode;
            GTA.UI.Notification.PostTicker($"TrafficEnhanced Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true);

            foreach (var b in _debugBlips)
            {
                if (b.Exists()) b.Alpha = _debugMode ? 255 : 0;
            }

            if (_debugMode)
            {
                // Scan for existing swaps to re-blip them
                foreach (Vehicle v in World.GetAllVehicles())
                {
                    if (v.Exists() && Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG))
                    {
                        // Only blip actual swaps (value 1), not ignored cars (value 0)
                        if (Function.Call<int>(Hash.DECOR_GET_INT, v, AMB_TAG) == 1)
                        {
                            AddDebugBlip(v);
                        }
                    }
                }
            }
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

    private void AssignToProfile(AmbientProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    private bool IsExcluded(Vehicle v)
    {
        // PROTECTION: If it has a blip (TrafficMP, Personal Vehicle), DO NOT TOUCH IT.
        if (v.AttachedBlip != null) return true;

        // PROTECTION: If it is persistent or a mission entity, DO NOT TOUCH IT.
        if (v.IsPersistent || Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) return true;

        VehicleClass vc = v.ClassType;
        if (vc == VehicleClass.Emergency || vc == VehicleClass.Industrial || vc == VehicleClass.Utility ||
            vc == VehicleClass.Cycles || vc == VehicleClass.Boats || vc == VehicleClass.Helicopters ||
            vc == VehicleClass.Planes || vc == VehicleClass.Commercial) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        return false;
    }

    private class AmbientProfile
    {
        public int RichChance;
        public int MidChance;
        public int PoorChance;
        public AmbientProfile(int rich, int mid, int poor) { RichChance = rich; MidChance = mid; PoorChance = poor; }
    }
}