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

    // Visibility Logic (Static Distances)
    private float _minSafeDist = 130f; // Absolute minimum swap distance
    private float _fovealDist = 240f;  // Max distance for high-detail swapping
    private float _periphDist = 60f;   // Peripheral vision safety buffer

    private int MaxSwapsPerCycle = 1; // Swaps per tick
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

        // REGISTRATION
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);
        Function.Call(Hash.DECOR_REGISTER, MP_TAG, 3);

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void InitializeZones()
    {
        AmbientProfile Hippy = new AmbientProfile(10, 50, 40, 0);
        AmbientProfile Gangster = new AmbientProfile(10, 30, 60, 0);
        AmbientProfile Downtown = new AmbientProfile(25, 45, 30, 0);
        AmbientProfile Vinewood = new AmbientProfile(40, 35, 15, 0);
        AmbientProfile Coastal = new AmbientProfile(60, 30, 10, 0);
        AmbientProfile Elite = new AmbientProfile(65, 25, 10, 0);
        AmbientProfile VinewoodHills = new AmbientProfile(100, 10, 5, 0);


        AmbientProfile Industry = new AmbientProfile(5, 60, 100, 0);


        AmbientProfile CountrySide = new AmbientProfile(5, 20, 70, 100);

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
        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        // --- STEP 1: THE CENSUS ---
        // Count how many of each model currently exist in the world.
        // We will use this to ruthlessly target duplicates.
        Dictionary<int, int> modelCensus = new Dictionary<int, int>();
        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists()) continue;
            int hash = v.Model.Hash;
            if (modelCensus.ContainsKey(hash)) modelCensus[hash]++;
            else modelCensus[hash] = 1;
        }

        // --- STEP 2: FIND CANDIDATES ---
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

            // Apply Scoring Logic
            float score = CalculateDirectorScore(v, camPos, camDir, player.ForwardVector);

            // --- DIVERSITY BONUS ---
            // If this model appears more than once in the world, prioritize swapping it!
            // +200 Score ensures we target duplicates before unique cars.
            if (modelCensus.ContainsKey(v.Model.Hash) && modelCensus[v.Model.Hash] > 1)
            {
                score += 200f;
            }

            if (score > ScoreThreshold)
            {
                candidates.Add(new ScoredVehicle { Vehicle = v, Score = score });
            }
        }

        // --- STEP 3: EXECUTE SWAPS ---
        // Sort by best score (Duplicates -> Oncoming -> Distant)
        var bestChoices = candidates.OrderByDescending(c => c.Score).Take(MaxSwapsPerCycle);

        foreach (var choice in bestChoices)
        {
            AttemptSwap(choice.Vehicle);
        }
    }

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

    private void AttemptSwap(Vehicle oldVeh)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, oldVeh.Position.X, oldVeh.Position.Y, oldVeh.Position.Z);
        AmbientProfile profile = _zoneRegistry.ContainsKey(zone) ? _zoneRegistry[zone] : _defaultProfile;

        // --- OPTIMIZED WEIGHTED SELECTION (Subtraction Method) ---
        // This method is cleaner and less prone to math errors than if/elif chains.

        int totalWeight = profile.RichChance + profile.MidChance + profile.PoorChance + profile.CountryChance;
        if (totalWeight <= 0) return; // Prevent divide by zero

        int roll = _rnd.Next(0, totalWeight);
        HashSet<string> targetList = null;

        // 1. Check Rich
        if (roll < profile.RichChance)
        {
            targetList = VehList.models_rich;
        }
        else
        {
            // Subtract the previous weight from the roll and check the next bucket
            roll -= profile.RichChance;

            // 2. Check Mid
            if (roll < profile.MidChance)
            {
                targetList = VehList.models_mid;
            }
            else
            {
                roll -= profile.MidChance;

                // 3. Check Poor
                if (roll < profile.PoorChance)
                {
                    targetList = VehList.models_poor;
                }
                else
                {
                    // 4. Must be Country
                    targetList = VehList.models_countryside;
                }
            }
        }

        if (targetList == null) return;

        // ---------------------------------------------------------

        string modelName = VehicleSelector.GetNext(targetList, "Ambient");
        if (modelName == null) return;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return;

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

    // =============================================================
    //                 HELPERS & UTILS
    // =============================================================

    private bool IsHidden(Vehicle v, float distSq, Vector3 camPos, Vector3 camDir)
    {
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f)) return true;
        if (distSq > _fovealDistSq) return true;
        Vector3 toCar = (v.Position - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);
        if (angle > 35.0f && distSq > _periphDistSq) return true;
        return World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.5f), IntersectFlags.Map).DidHit;
    }

    private bool IsSwapped(Vehicle v)
    {
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG)) return true;
        if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, MP_TAG)) return true;
        return false;
    }

    private bool IsExcluded(Vehicle v)
    {
        if (v.AttachedBlip != null) return true;
        if (v.IsPersistent || Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) return true;
        VehicleClass vc = v.ClassType;
        if (vc == VehicleClass.Emergency || vc == VehicleClass.Industrial || vc == VehicleClass.Utility ||
            vc == VehicleClass.Cycles || vc == VehicleClass.Boats || vc == VehicleClass.Helicopters ||
            vc == VehicleClass.Planes || vc == VehicleClass.Commercial) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        return false;
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F10)
        {
            _debugMode = !_debugMode;
            GTA.UI.Notification.PostTicker($"TrafficEnhanced Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true);
            foreach (var b in _debugBlips) { if (b.Exists()) b.Alpha = _debugMode ? 255 : 0; }
            if (_debugMode)
            {
                foreach (Vehicle v in World.GetAllVehicles())
                {
                    if (v.Exists() && Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG))
                    {
                        if (Function.Call<int>(Hash.DECOR_GET_INT, v, AMB_TAG) == 1) AddDebugBlip(v);
                    }
                }
            }
        }
    }

    private void OnAborted(object sender, EventArgs e) { foreach (var b in _debugBlips) if (b.Exists()) b.Delete(); }

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
            if (!b.Exists() || b.Entity == null || !b.Entity.Exists()) { if (b.Exists()) b.Delete(); _debugBlips.RemoveAt(i); }
        }
    }

    private void AssignToProfile(AmbientProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    private struct ScoredVehicle { public Vehicle Vehicle; public float Score; }

    private class AmbientProfile
    {
        public int RichChance; public int MidChance; public int PoorChance; public int CountryChance; // New Ingredient;
        public AmbientProfile(int rich, int mid, int poor, int country) { RichChance = rich; MidChance = mid; PoorChance = poor; CountryChance = country; }
    }
}