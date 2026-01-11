using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Drawing;

public class TrafficMP : Script
{
    // ==========================================
    //      PERFORMANCE & LOGIC SETTINGS
    // ==========================================
    private int MaxDuplicates = 1;       // Strict variety

    // CPU TUNING
    private int CheckInterval = 1000;     // 4x per second
    private int MaxSwapsPerCycle = 5;

    private int MinTransformDist = 60;   // Pulled closer (was 80)
    private int HorizonDist = 100;       // AGGRESSIVE: Beyond 200m, we force everything.


    // GLOBAL BLIP SETTING
    private bool ShowBlips = true;

    // EXCLUSIONS
    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string MARKER_PLATE = "TMP_SWAP";
    // ==========================================

    private int _nextCheckTime = 0;
    private Random _rnd = new Random();

    private float _minTransformDistSq;
    private float _horizonDistSq;
    private bool _debugMode = false; // Toggle F11

    // Registry & Zones
    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    private HashSet<int> _presentModels = new HashSet<int>();
    private List<Blip> _activeBlips = new List<Blip>();

    // AGGRESSIVE PROBES: Starts at 120m now.
    private float[] _spawnProbes = new float[] { 120f, 180f, 250f, 350f, 500f };

    public TrafficMP()
    {
        _minTransformDistSq = MinTransformDist * MinTransformDist;
        _horizonDistSq = HorizonDist * HorizonDist;
        InitializeRegistry();
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
            GTA.UI.Notification.Show($"TrafficMP Debug: {(_debugMode ? "~g~ON" : "~r~OFF")}");
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

        Vehicle[] allVehicles = World.GetAllVehicles();

        _presentModels.Clear();
        Dictionary<int, int> modelCounts = new Dictionary<int, int>();
        List<Vehicle> candidates = new List<Vehicle>();

        // 1. SCAN
        foreach (Vehicle v in allVehicles)
        {
            if (v == null || !v.Exists()) continue;

            int hash = v.Model.Hash;
            if (!_presentModels.Contains(hash)) _presentModels.Add(hash);

            if (modelCounts.ContainsKey(hash)) modelCounts[hash]++;
            else modelCounts[hash] = 1;

            if (v.Driver == null || v.Driver.IsPlayer || v.Mods.LicensePlate == MARKER_PLATE) continue;

            float distSq = v.Position.DistanceToSquared(playerPos);
            if (distSq < _minTransformDistSq) continue;

            if (IsExcludedCategory(v)) continue;

            bool needsSwap = (modelCounts[hash] > MaxDuplicates);

            if (needsSwap)
            {
                // Use new Aggressive Safety Check
                if (IsSafeToSwap(v, distSq, camPos))
                {
                    candidates.Add(v);
                }
            }
        }

        // 2. SWAP
        int swapsDone = 0;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (swapsDone >= MaxSwapsPerCycle) break;

            if (TransformVehicle(candidates[i]))
            {
                swapsDone++;
            }
        }

    }

  

    // ==========================================
    //           AGGRESSIVE SWAP LOGIC
    // ==========================================
    private bool IsSafeToSwap(Vehicle v, float distSq, Vector3 camPos)
    {
        // 1. FORCE SWAP > 200m (Visible or not, we change it)
        if (distSq > _horizonDistSq) return true;

        // 2. BEHIND US check
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f))
        {
            return true;
        }

        // 3. RAYCAST (Mid-Range 60m - 200m)
        // If TIRES are blocked, swap it. (Allows swapping on hills/dips)
        bool lowBlocked = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;

        if (lowBlocked) return true;

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

    private bool TransformVehicle(Vehicle oldVehicle)
    {
        SpawnCandidate candidate = GetCandidateForLocation(oldVehicle.Position);

        int newHash = Game.GenerateHash(candidate.ModelName);
        if (_presentModels.Contains(newHash)) return false;

        if (string.IsNullOrEmpty(candidate.ModelName)) return false;

        Model model = new Model(candidate.ModelName);
        if (!model.IsValid || !model.IsInCdImage) return false;

        model.Request(10);
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

            CarMod.ApplyStyle(newVehicle, candidate.Behavior, candidate.ModelName);
            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, 20.0f, (VehicleDrivingFlags)786603);

            if (ShowBlips || _debugMode) CreateBlip(newVehicle, candidate.ModelName);
            if (_debugMode) GTA.UI.Notification.PostTicker($"~y~Swap: {candidate.ModelName}", true);

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            _presentModels.Add(newHash);

            model.MarkAsNoLongerNeeded();
            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, newVehicle);
            if (comboCount > 0)
            {
                Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, newVehicle, _rnd.Next(0, comboCount));
            }
            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    // ==========================================
    //           BLIP & DEBUG SYSTEM
    // ==========================================
    private void CreateBlip(Vehicle v, string name)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = BlipColor.Green;
        b.Scale = 0.5f;
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
            if (v.Mods.LicensePlate == MARKER_PLATE)
            {
                if (v.IsOnScreen)
                {
                    World.DrawMarker(MarkerType.ChevronUpx1, v.Position + new Vector3(0, 0, 2), Vector3.Zero, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Color.Yellow);
                }
            }
        }
    }

    // ==========================================
    //           BOILERPLATE (LISTS & ZONES)
    // ==========================================
    private void InitializeRegistry()
    {
        _behaviorRegistry = new Dictionary<HashSet<string>, SpawnBehavior>();
        if (VehList.models_supers_common != null) _behaviorRegistry.Add(VehList.models_supers_common, SpawnBehavior.Spec);
        if (VehList.models_city != null) _behaviorRegistry.Add(VehList.models_city, SpawnBehavior.Spec);
        if (VehList.models_classics_common != null) _behaviorRegistry.Add(VehList.models_classics_common, SpawnBehavior.Spec);
        if (VehList.models_lowriders != null) _behaviorRegistry.Add(VehList.models_lowriders, SpawnBehavior.RandomSpec);
        if (VehList.models_general_common != null) _behaviorRegistry.Add(VehList.models_general_common, SpawnBehavior.Spec);
        if (VehList.models_general_rare != null) _behaviorRegistry.Add(VehList.models_general_rare, SpawnBehavior.Stock);
        if (VehList.models_rural != null) _behaviorRegistry.Add(VehList.models_rural, SpawnBehavior.Spec);
        if (VehList.models_wacky != null) _behaviorRegistry.Add(VehList.models_wacky, SpawnBehavior.RandomSpec);
    }

    private void InitializeZones()
    {
        ZoneProfile ruralProfile = new ZoneProfile("RURAL");
        ruralProfile.IsRural = true;
        ruralProfile.AddIngredient(VehList.models_rural, 50);
        ruralProfile.AddIngredient(VehList.models_general_common, 30);
        ruralProfile.AddIngredient(VehList.models_general_rare, 10);
        ruralProfile.AddIngredient(VehList.models_wacky, 10);

        ZoneProfile richProfile = new ZoneProfile("RICH");
        richProfile.AddIngredient(VehList.models_supers_common, 5);
        richProfile.AddIngredient(VehList.models_classics_common, 5);
        richProfile.AddIngredient(VehList.models_city, 90);

        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(VehList.models_lowriders, 5);
        ghettoProfile.AddIngredient(VehList.models_general_common, 55);
        ghettoProfile.AddIngredient(VehList.models_general_rare, 40);

        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(VehList.models_city, 30);
        urbanProfile.AddIngredient(VehList.models_general_common, 40);
        urbanProfile.AddIngredient(VehList.models_general_rare, 30);

        ZoneProfile generalProfile = new ZoneProfile("GENERAL");
        generalProfile.AddIngredient(VehList.models_general_common, 50);
        generalProfile.AddIngredient(VehList.models_general_rare, 50);

        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA", "HARMO", "RTRAK", "ZQ_UAR", "MTJOSE");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE", "GALLI", "BAYTRE", "MORN");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON", "LOSPUER", "AIRP");
        AssignToProfile(generalProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI", "PALHIGH", "TATAMO");
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones) { foreach (string z in zones) _zoneRegistry[z] = profile; }

    private SpawnCandidate GetCandidateForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (string.IsNullOrEmpty(zone)) return new SpawnCandidate();
        if (_bannedZones.Contains(zone)) return new SpawnCandidate();

        if (_zoneRegistry.ContainsKey(zone))
        {
            ZoneProfile profile = _zoneRegistry[zone];
            HashSet<string> selectedList = profile.PickList();
            if (selectedList != null) return PickFromList(selectedList);
        }
        return new SpawnCandidate();
    }

    private SpawnCandidate PickFromList(HashSet<string> list)
    {
        string modelName = VehicleSelector.GetNext(list, _excludedModels);
        if (string.IsNullOrEmpty(modelName)) return new SpawnCandidate();
        return new SpawnCandidate { ModelName = modelName, Behavior = _behaviorRegistry.ContainsKey(list) ? _behaviorRegistry[list] : SpawnBehavior.Stock };
    }

    public struct SpawnCandidate { public string ModelName; public SpawnBehavior Behavior; }
    public class ZoneProfile
    {
        public string Name;
        public bool IsRural { get; set; } = false;
        private struct Ingredient { public HashSet<string> List; public int Weight; }
        private List<Ingredient> _ingredients = new List<Ingredient>();
        private int _totalWeight = 0;
        private Random _rnd = new Random();
        public ZoneProfile(string name) { Name = name; }
        public void AddIngredient(HashSet<string> list, int weight)
        {
            if (list == null) return;
            _ingredients.Add(new Ingredient { List = list, Weight = weight }); _totalWeight += weight;
        }
        public HashSet<string> PickList()
        {
            if (_ingredients.Count == 0) return null;
            int roll = _rnd.Next(0, _totalWeight);
            int current = 0;
            foreach (var item in _ingredients) { current += item.Weight; if (roll < current) return item.List; }
            return _ingredients[0].List;
        }
    }
}