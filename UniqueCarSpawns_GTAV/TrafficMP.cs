using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Drawing;

public class TrafficMP : Script
{
    // =============================================================
    //                 TUNING DASHBOARD
    // =============================================================

    // 1. SAFETY BUBBLE (Meters)
    private int MinTransformDist = 60;

    // 2. SMART FRUSTUM
    private int FovealDist = 180;
    private int PeripheralDist = 80;

    // 3. PERFORMANCE
    private int CheckInterval = 250;
    private int MaxSwapsPerCycle = 1;

    // 4. LOGIC
    private bool ShowBlips = true;

    // =============================================================

    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string MARKER_PLATE = "TMP_SWAP";
    private VehicleDrivingFlags DriveStyle = (VehicleDrivingFlags)786603 | (VehicleDrivingFlags)262144;

    private int _nextCheckTime = 0;
    private float _minTransformDistSq;
    private float _fovealDistSq;
    private float _peripheralDistSq;

    private bool _debugMode = false; // F11 to Toggle

    // Registry & Zones
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    private List<Blip> _activeBlips = new List<Blip>();
    private Random _rnd = new Random();

    public TrafficMP()
    {
        _minTransformDistSq = MinTransformDist * MinTransformDist;
        _fovealDistSq = FovealDist * FovealDist;
        _peripheralDistSq = PeripheralDist * PeripheralDist;

        InitializeZones();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_debugMode) DrawDebugInfo();

        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            CleanupBlips();
            RunIntelligentReplacement();
        }
        catch (Exception) { }

        _nextCheckTime = Game.GameTime + CheckInterval;
    }

    private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.F11)
        {
            _debugMode = !_debugMode;
            GTA.UI.Notification.PostTicker($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}", true);
            foreach (var b in _activeBlips) if (b.Exists()) b.Alpha = _debugMode || ShowBlips ? 255 : 0;
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _activeBlips) if (b.Exists()) b.Delete();
    }

    private void RunIntelligentReplacement()
    {
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position;
        Vector3 camDir = GameplayCamera.Direction;

        Vehicle[] allVehicles = World.GetAllVehicles();
        List<Vehicle> candidates = new List<Vehicle>();

        // 1. SCAN (No Duplicate Logic anymore)
        foreach (Vehicle v in allVehicles)
        {
            if (v == null || !v.Exists()) continue;

            // Skip already swapped cars, player cars, etc.
            if (v.Driver == null || v.Driver.IsPlayer || v.Mods.LicensePlate == MARKER_PLATE) continue;

            float distSq = v.Position.DistanceToSquared(playerPos);

            // Basic Filters
            if (distSq < _minTransformDistSq) continue;
            if (IsExcludedCategory(v)) continue;

            // If it is visible/safe to swap, add it to the list.
            // We don't care if it's unique or a duplicate.
            if (IsSafeToSwap(v, distSq, camPos, camDir))
            {
                candidates.Add(v);
            }
        }

        // 2. SHUFFLE (Randomize Victims)
        // This ensures we don't just swap the nearest ones every time.
        if (candidates.Count > 0)
        {
            ShuffleList(candidates);
        }

        // 3. SWAP
        int swapsDone = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (swapsDone >= MaxSwapsPerCycle) break;

            if (TransformVehicle(candidates[i]))
            {
                swapsDone++;
            }
        }
    }

    // ==========================================
    //           SELECTION LOGIC (Card Deck)
    // ==========================================
    private bool TransformVehicle(Vehicle oldVehicle)
    {
        // 1. Get List for Zone
        SelectionLayer layer = GetLayerForLocation(oldVehicle.Position);
        if (layer.List == null) return false;

        // 2. Use VehicleSelector (Deck of Cards Logic)
        // This guarantees variety in what we SPAWN, even if we are aggressive about removing vanilla cars.
        string modelName = VehicleSelector.GetNext(layer.List, _excludedModels);

        if (string.IsNullOrEmpty(modelName)) return false;

        Model model = new Model(modelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request(5);
        if (!model.IsLoaded) return false;

        Ped driver = oldVehicle.Driver;
        if (driver == null || !driver.Exists()) return false;

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);

        Vehicle newVehicle = World.CreateVehicle(model, oldVehicle.Position, oldVehicle.Heading);

        if (newVehicle != null)
        {
            newVehicle.Velocity = oldVehicle.Velocity;
            newVehicle.ForwardSpeed = oldVehicle.Speed;
            newVehicle.IsEngineRunning = true;
            newVehicle.Mods.LicensePlate = MARKER_PLATE;

            driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
            oldVehicle.Delete();

            CarMod.ApplyStyle(newVehicle, layer.Behavior, modelName);

            driver.BlockPermanentEvents = true;
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, DriveStyle);

            if (ShowBlips || _debugMode) CreateBlip(newVehicle, modelName);
            if (_debugMode) GTA.UI.Notification.PostTicker($"~y~Swap: {modelName}", true);

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            model.MarkAsNoLongerNeeded();

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));

            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rnd.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private SelectionLayer GetLayerForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (string.IsNullOrEmpty(zone) || _bannedZones.Contains(zone)) return new SelectionLayer();

        if (_zoneRegistry.ContainsKey(zone))
        {
            return _zoneRegistry[zone].PickWeightedLayer();
        }
        return new SelectionLayer();
    }

    // ==========================================
    //      COMPLEX HORIZON & TERRAIN SCANNER
    // ==========================================
    private bool IsSafeToSwap(Vehicle v, float distSq, Vector3 camPos, Vector3 camDir)
    {
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f)) return true;

        if (distSq > _fovealDistSq) return true;

        Vector3 toCar = (v.Position - camPos).Normalized;
        if (Vector3.Angle(camDir, toCar) > 25.0f)
        {
            if (distSq > _peripheralDistSq) return true;
        }

        bool hideLow = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;
        bool hideMid = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.9f), IntersectFlags.Map).DidHit;

        if (hideLow && hideMid) return true;

        return false;
    }

    private bool IsExcludedCategory(Vehicle v)
    {
        if (v.Model.IsTrain || v.Model.IsBoat || v.Model.IsHelicopter || v.Model.IsPlane) return true;
        VehicleClass vc = v.ClassType;
        if (IgnoreEmergency && (vc == VehicleClass.Emergency || v.Driver.IsInPoliceVehicle)) return true;
        if (IgnoreService && (vc == VehicleClass.Service || vc == VehicleClass.Commercial || v.Model.IsBus)) return true;
        if (v.Model.Hash == unchecked((int)VehicleHash.Taxi)) return true;
        if (IgnoreBig && (vc == VehicleClass.Industrial || vc == VehicleClass.Utility || vc == VehicleClass.Military)) return true;
        return false;
    }

    private void CreateBlip(Vehicle v, string name)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.PersonalVehicleCar;
        b.Color = BlipColor.Purple;
        b.Scale = 0.7f;
        b.Name = name;
        b.IsShortRange = true;
        if (!_debugMode && !ShowBlips) b.Alpha = 0;
        _activeBlips.Add(b);
    }

    private void CleanupBlips()
    {
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            if (!_activeBlips[i].Exists() || _activeBlips[i].Entity == null || !_activeBlips[i].Entity.Exists())
            {
                if (_activeBlips[i].Exists()) _activeBlips[i].Delete();
                _activeBlips.RemoveAt(i);
            }
        }
    }

    private void DrawDebugInfo()
    {
        Vehicle[] vehs = World.GetAllVehicles();
        foreach (Vehicle v in vehs)
        {
            if (v.Mods.LicensePlate == MARKER_PLATE && v.IsOnScreen)
            {
                World.DrawMarker(MarkerType.Chevron1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow);
            }
        }
    }

    // ==========================================
    //           ZONE SETUP (DISCONNECTED)
    // ==========================================
    private void InitializeZones()
    {
        // IMPORTANT: We use 'new HashSet<string>(VehList.xxx)' to create COPIES.
        // This ensures VehicleSelector creates a unique Queue for TrafficMP.

        ZoneProfile ruralProfile = new ZoneProfile("RURAL");
        ruralProfile.AddIngredient(VehList.models_rural, 50, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(VehList.models_general_common, 30, SpawnBehavior.Spec);
        ruralProfile.AddIngredient(VehList.models_general_rare, 10, SpawnBehavior.Stock);
        ruralProfile.AddIngredient(VehList.models_wacky, 10, SpawnBehavior.RandomSpec);

        ZoneProfile richProfile = new ZoneProfile("RICH");
        richProfile.AddIngredient(VehList.models_supers_common, 5, SpawnBehavior.Spec);
        richProfile.AddIngredient(VehList.models_classics_common, 5, SpawnBehavior.Spec);
        richProfile.AddIngredient(VehList.models_veh_rich, 70, SpawnBehavior.Spec);
        richProfile.AddIngredient(VehList.models_veh_mid, 20, SpawnBehavior.Spec);

        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(VehList.models_lowriders, 50, SpawnBehavior.RandomSpec);
        ghettoProfile.AddIngredient(VehList.models_general_common, 40, SpawnBehavior.Spec);
        ghettoProfile.AddIngredient(VehList.models_general_rare, 10, SpawnBehavior.Stock);

        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(VehList.models_city, 30, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(VehList.models_general_common, 40, SpawnBehavior.Spec);
        urbanProfile.AddIngredient(VehList.models_general_rare, 30, SpawnBehavior.Stock);

        ZoneProfile generalProfile = new ZoneProfile("GENERAL");
        generalProfile.AddIngredient(VehList.models_general_common, 50, SpawnBehavior.Spec);
        generalProfile.AddIngredient(VehList.models_general_rare, 50, SpawnBehavior.Stock);


        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP");
        AssignToProfile(generalProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO");
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    public struct SelectionLayer { public HashSet<string> List; public SpawnBehavior Behavior; }

    public class ZoneProfile
    {
        public string Name;
        private struct Ingredient { public HashSet<string> List; public int Weight; public SpawnBehavior Behavior; }
        private List<Ingredient> _ingredients = new List<Ingredient>();
        private int _totalWeight = 0;
        private Random _rnd = new Random();

        public ZoneProfile(string name) { Name = name; }

        public void AddIngredient(HashSet<string> list, int weight, SpawnBehavior behavior)
        {
            if (list == null) return;
            _ingredients.Add(new Ingredient { List = list, Weight = weight, Behavior = behavior });
            _totalWeight += weight;
        }

        public SelectionLayer PickWeightedLayer()
        {
            if (_ingredients.Count == 0) return new SelectionLayer();

            int roll = _rnd.Next(0, _totalWeight);
            int current = 0;
            foreach (var item in _ingredients)
            {
                current += item.Weight;
                if (roll < current) return new SelectionLayer { List = item.List, Behavior = item.Behavior };
            }
            return new SelectionLayer { List = _ingredients[0].List, Behavior = _ingredients[0].Behavior };
        }
    }
}