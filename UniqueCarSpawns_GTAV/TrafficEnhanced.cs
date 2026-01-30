using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficEnhanced : Script
{
    // =============================================================
    //                 SETTINGS & TUNING
    // =============================================================

    private const string MP_TAG = "TMP_Swap_ID";
    private const string AMB_TAG = "Ambient_Swap_ID";

    // Performance
    private int _checkInterval = 1000;
    private int _swapChance = 10;

    // Smart Frustum (Visibility Logic)
    private float _minSafeDist = 130f; // BUMPED UP: 40f -> 50f for extra safety
    private float _fovealDist = 240f;
    private float _periphDist = 60f;

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
        _minSafeDistSq = _minSafeDist * _minSafeDist;
        _fovealDistSq = _fovealDist * _fovealDist;
        _periphDistSq = _periphDist * _periphDist;

        InitializeZones();
        Function.Call(Hash.DECOR_REGISTER, AMB_TAG, 3);

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
                foreach (Vehicle v in World.GetAllVehicles())
                {
                    if (v.Exists() && Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG))
                    {
                        AddDebugBlip(v);
                    }
                }
            }
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _debugBlips) if (b.Exists()) b.Delete();
    }

    private void ProcessAmbientTraffic()
    {
        Vehicle[] vehicles = World.GetAllVehicles();
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        foreach (Vehicle v in vehicles)
        {
            if (!v.Exists() || v.Driver == null || v.Driver.IsPlayer) continue;

            if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, MP_TAG)) continue;
            if (Function.Call<bool>(Hash.DECOR_EXIST_ON, v, AMB_TAG)) continue;
            if (IsExcluded(v)) continue;

            float distSq = v.Position.DistanceToSquared(playerPos);

            // Replaced with Robust Check
            if (!IsSafeToSwap(v, distSq, camPos, camDir)) continue;

            if (_rnd.Next(0, 100) > _swapChance)
            {
                Function.Call(Hash.DECOR_SET_INT, v, AMB_TAG, 0);
                continue;
            }

            AttemptSwap(v);
        }
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

        if (!model.Request(10)) return;
        if (!oldVeh.Exists()) return;

        Ped driver = oldVeh.Driver;
        if (driver == null || !driver.Exists()) return;

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

    private void AddDebugBlip(Vehicle v)
    {
        if (v.AttachedBlip != null) return;

        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.Green;
        b.Scale = 0.6f;
        b.Name = "Ambient Swap";
        b.IsShortRange = true;
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

    // =============================================================
    //                 THE INTELLIGENT SCANNER
    // =============================================================
    private bool IsSafeToSwap(Vehicle v, float distSq, Vector3 camPos, Vector3 camDir)
    {
        // 1. ABSOLUTE SAFETY BUBBLE
        if (distSq < _minSafeDistSq) return false;

        // 2. SPHERE CHECK (The "Corner" Fix)
        // Ask Game Engine: "Is a 4m bubble around this car visible on screen?"
        // If FALSE, it means the ENTIRE car is hidden/off-screen. Safe to swap immediately.
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f))
        {
            return true;
        }

        // --- If we are here, the car IS visible to the camera ---

        // 3. DISTANCE CULLING
        // If it's visible but super far away, we can swap it (too small to notice details).
        if (distSq > _fovealDistSq) return true;

        // 4. PERIPHERAL VISION CHECK
        // If it's visible, close, but in the corner of our eye?
        Vector3 toCar = (v.Position - camPos).Normalized;
        float angle = Vector3.Angle(camDir, toCar);

        if (angle > 35.0f) // Outside central focus
        {
            if (distSq > _periphDistSq) return true;
        }

        // 5. RAYCAST (Last Resort for Windows/Fences)
        // If we reached here: The car is Visible, Close, and In Focus.
        // The ONLY way we swap is if it's behind a solid object (Wall/Bus) that IS_SPHERE_VISIBLE missed.

        // Check Low (Bumper)
        bool hideLow = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;
        if (hideLow)
        {
            // Check High (Roof) - Only if low was hidden
            return World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.9f), IntersectFlags.Map).DidHit;
        }

        return false;
    }

    private bool IsExcluded(Vehicle v)
    {
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