using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficMP : Script
{
    // ==========================================
    //      PERFORMANCE & LOGIC SETTINGS
    // ==========================================
    private int MaxDuplicates = 1;       // Strict variety (1 of each model max)

    // TUNED FOR HIGH SPEED REPLACEMENT:
    private int CheckInterval = 250;     // Run logic 4 times per second (Fast response)
    private int MaxSwapsPerCycle = 3;    // Swap 3 cars per tick.

    private int MinTransformDist = 80;   // Meters
    private int HorizonDist = 300;       // DISTANCE FIX: Beyond this, we force swaps
    private bool SwapNewTraffic = true;

    // EXCLUSIONS
    private bool IgnoreEmergency = true;
    private bool IgnoreService = true;
    private bool IgnoreBig = true;

    private const string MARKER_PLATE = "TMP_SWAP";
    // ==========================================

    private int _nextCheckTime = 0;
    private Random _rnd = new Random();

    // Optimization: Cache squared distance to avoid Sqrt math every frame
    private float _minTransformDistSq;
    private float _horizonDistSq;

    // Registry & Zones
    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deveste", "sm722", "prototipo" };
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    // Duplicate Prevention Tracker
    private HashSet<int> _presentModels = new HashSet<int>();

    public TrafficMP()
    {
        _minTransformDistSq = MinTransformDist * MinTransformDist; // Calculate once
        _horizonDistSq = HorizonDist * HorizonDist;
        InitializeRegistry();
        InitializeZones();
        Tick += OnTick;
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.GameTime < _nextCheckTime) return;

        try
        {
            RunIntelligentReplacement();
        }
        catch (Exception)
        {
            // Silently catch errors to keep script alive
        }

        _nextCheckTime = Game.GameTime + CheckInterval;
    }

    private void RunIntelligentReplacement()
    {
        Ped player = Game.Player.Character;
        Vector3 playerPos = player.Position;
        Vector3 camPos = GameplayCamera.Position; // Needed for Raycasting

        // 1. GET VEHICLES (Raw Array is faster than List)
        Vehicle[] allVehicles = World.GetAllVehicles();

        // 2. RESET TRACKERS
        _presentModels.Clear();
        Dictionary<int, int> modelCounts = new Dictionary<int, int>();
        List<Vehicle> candidates = new List<Vehicle>();

        // 3. FIRST PASS: SCAN WORLD
        foreach (Vehicle v in allVehicles)
        {
            if (v == null || !v.Exists()) continue;

            int hash = v.Model.Hash;

            // Always track this model exists to prevent spawning it later
            if (!_presentModels.Contains(hash)) _presentModels.Add(hash);

            if (modelCounts.ContainsKey(hash)) modelCounts[hash]++;
            else modelCounts[hash] = 1;

            // Is this vehicle a candidate for swapping?
            if (v.Driver == null || v.Driver.IsPlayer || v.Mods.LicensePlate == MARKER_PLATE) continue;

            // Distance Check
            float distSq = v.Position.DistanceToSquared(playerPos);
            if (distSq < _minTransformDistSq) continue;

            if (IsExcludedCategory(v)) continue;

            // --- SMART RAYCAST LOGIC START ---
            bool needsSwap = (modelCounts[hash] > MaxDuplicates);

            if (needsSwap)
            {
                // If checking specifically for duplicate reduction or variety
                if (IsSafeToSwap(v, distSq, camPos))
                {
                    candidates.Add(v);
                }
            }
            // --- SMART RAYCAST LOGIC END ---
        }

        // 4. SECOND PASS: SWAP (With Limits)
        int swapsDone = 0;

        // Iterate backwards
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (swapsDone >= MaxSwapsPerCycle) break;

            Vehicle v = candidates[i];
            if (v != null && v.Exists())
            {
                if (TransformVehicle(v))
                {
                    swapsDone++;
                }
            }
        }

        // 5. OPTIONAL: EMPTY ROAD INJECTION
        if (SwapNewTraffic && swapsDone == 0 && allVehicles.Length < 25)
        {
            SpawnNewOnEmptyRoad();
        }
    }

    // ==========================================
    //        NEW: SMART RAYCASTING METHOD
    // ==========================================
    private bool IsSafeToSwap(Vehicle v, float distSq, Vector3 camPos)
    {
        // 1. HORIZON CHECK: If it's super far (> 300m), always swap. 
        // We don't care about visibility at this distance, it's just a pixel.
        if (distSq > _horizonDistSq) return true;

        // 2. FRUSTUM CHECK: Is it even on my screen?
        // If it's behind me or to the side (not visible), swap it.
        if (!Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, v.Position.X, v.Position.Y, v.Position.Z, 4.0f))
        {
            return true;
        }

        // 3. RAYCAST CHECK (The Open Road Fix)
        // If we are here, the car is < 300m AND within our screen view.
        // We must check if it is physically blocked by a hill, building, or wall.

        // Cast to TIRES (0.4f up)
        bool lowBlocked = World.Raycast(camPos, v.Position + new Vector3(0, 0, 0.4f), IntersectFlags.Map).DidHit;

        // Cast to ROOF (1.4f up)
        bool highBlocked = World.Raycast(camPos, v.Position + new Vector3(0, 0, 1.4f), IntersectFlags.Map).DidHit;

        // If BOTH checks hit a map object, the car is behind cover. Safe to swap.
        // If EITHER hits nothing, the player has line-of-sight. UNSAFE to swap.
        if (lowBlocked && highBlocked)
        {
            return true;
        }

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

            newVehicle.MarkAsNoLongerNeeded();
            driver.MarkAsNoLongerNeeded();

            _presentModels.Add(newHash);

            model.MarkAsNoLongerNeeded();
            return true;
        }

        model.MarkAsNoLongerNeeded();
        return false;
    }

    private void SpawnNewOnEmptyRoad()
    {
        Ped player = Game.Player.Character;
        Vector3 spawnPos = player.Position + (player.ForwardVector * 180f);

        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();

        if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, spawnPos.X, spawnPos.Y, spawnPos.Z, outPos, outHead, 1, 3.0f, 0))
        {
            Vector3 finalPos = outPos.GetResult<Vector3>();
            float heading = outHead.GetResult<float>();

            if (Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, finalPos.X, finalPos.Y, finalPos.Z, 5.0f)) return;

            SpawnCandidate candidate = GetCandidateForLocation(finalPos);

            int newHash = Game.GenerateHash(candidate.ModelName);
            if (_presentModels.Contains(newHash)) return;

            if (string.IsNullOrEmpty(candidate.ModelName)) return;

            Model model = new Model(candidate.ModelName);
            model.Request(10);
            if (model.IsLoaded)
            {
                Vehicle v = World.CreateVehicle(model, finalPos, heading);
                if (v != null)
                {
                    v.Mods.LicensePlate = MARKER_PLATE;
                    Ped driver = v.CreateRandomPedOnSeat(VehicleSeat.Driver);
                    if (driver != null && driver.Exists())
                    {
                        driver.Task.CruiseWithVehicle(v, 20f, (VehicleDrivingFlags)786603);
                        driver.MarkAsNoLongerNeeded();
                    }
                    else
                    {
                        v.Delete();
                    }

                    if (v.Exists()) v.MarkAsNoLongerNeeded();
                    _presentModels.Add(newHash);
                }
                model.MarkAsNoLongerNeeded();
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
        richProfile.AddIngredient(VehList.models_supers_common, 40);
        richProfile.AddIngredient(VehList.models_classics_common, 40);
        richProfile.AddIngredient(VehList.models_city, 20);

        ZoneProfile ghettoProfile = new ZoneProfile("GHETTO");
        ghettoProfile.AddIngredient(VehList.models_lowriders, 50);
        ghettoProfile.AddIngredient(VehList.models_general_common, 40);
        ghettoProfile.AddIngredient(VehList.models_general_rare, 10);

        ZoneProfile urbanProfile = new ZoneProfile("URBAN");
        urbanProfile.AddIngredient(VehList.models_city, 50);
        urbanProfile.AddIngredient(VehList.models_general_common, 30);
        urbanProfile.AddIngredient(VehList.models_general_rare, 20);

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