using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficMP : Script
{
    // ==========================================
    //              QUICK SETTINGS
    // ==========================================
    private bool ShowBlips = true;

    // UPDATED: 250.0f gives a healthy buffer for the new 150m spawn logic.
    private float DespawnDistance = 250.0f;

    private int SpawnChance = 100;
    private int CheckInterval = 1500;
    private int RareCarChance = 15;
    // ==========================================

    private Vehicle _activeVehicle;
    private Ped _activeDriver;
    private Blip _activeBlip;
    private int _nextSpawnCheckTime = 0;
    private Random _rnd = new Random();

    private bool _isInMissionMode = false;
    private string _lastGlobalModel = "";

    private ZoneProfile _trailProfile;

    // Updated: Uses Shared Enum
    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
       "deveste", "sm722", "prototipo"
    };

    // ZONES
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL", "TATAMO", "MTJOSE" };
    private Dictionary<string, ZoneProfile> _zoneRegistry = new Dictionary<string, ZoneProfile>();

    public TrafficMP()
    {
        InitializeRegistry();
        InitializeZones();
        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void InitializeRegistry()
    {
        _behaviorRegistry = new Dictionary<HashSet<string>, SpawnBehavior>();
        _behaviorRegistry.Add(VehList.models_supers_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_city, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_classics_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_lowriders, SpawnBehavior.RandomSpec);
        _behaviorRegistry.Add(VehList.models_general_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_general_rare, SpawnBehavior.Stock);
    }

    private void InitializeZones()
    {
        _trailProfile = new ZoneProfile();
        _trailProfile.AddIngredient(VehList.models_cemetery, 100);

        ZoneProfile ruralProfile = new ZoneProfile();
        ruralProfile.AddIngredient(VehList.models_rural, 50);
        ruralProfile.AddIngredient(VehList.models_general_common, 30);
        ruralProfile.AddIngredient(VehList.models_general_rare, 10);
        ruralProfile.AddIngredient(VehList.models_wacky, 10);

        ZoneProfile richProfile = new ZoneProfile();
        richProfile.AddIngredient(VehList.models_supers_common, 40);
        richProfile.AddIngredient(VehList.models_classics_common, 40);
        richProfile.AddIngredient(VehList.models_city, 20);

        ZoneProfile ghettoProfile = new ZoneProfile();
        ghettoProfile.AddIngredient(VehList.models_lowriders, 50);
        ghettoProfile.AddIngredient(VehList.models_general_common, 40);
        ghettoProfile.AddIngredient(VehList.models_general_rare, 10);

        ZoneProfile urbanProfile = new ZoneProfile();
        urbanProfile.AddIngredient(VehList.models_city, 50);
        urbanProfile.AddIngredient(VehList.models_general_common, 30);
        urbanProfile.AddIngredient(VehList.models_general_rare, 20);

        ZoneProfile industrialProfile = new ZoneProfile();
        industrialProfile.AddIngredient(VehList.models_general_common, 50);
        industrialProfile.AddIngredient(VehList.models_general_rare, 50);

        AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "PALFOR", "DESRT", "MTCHIL", "NCHU", "ALAMO", "PALETO", "SANDY", "GREATC", "WINDF", "ZANCUDO", "LAGO", "SANCHIA");
        AssignToProfile(richProfile, "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "OBSERV", "DELPE");
        AssignToProfile(ghettoProfile, "CHAMH", "DAVIS", "RANCHO", "STRAW");
        AssignToProfile(urbanProfile, "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA", "HAWICK", "BURTON");
        AssignToProfile(industrialProfile, "EBURO", "CYPRE", "BANNIN", "LMESA", "MURRI");
    }

    private void AssignToProfile(ZoneProfile profile, params string[] zones)
    {
        foreach (string z in zones) _zoneRegistry[z] = profile;
    }

    private void OnTick(object sender, EventArgs e)
    {
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        if (isMissionActive)
        {
            if (!_isInMissionMode)
            {
                ReleaseResourcesToGame();
                _isInMissionMode = true;
            }
            return;
        }
        else
        {
            if (_isInMissionMode)
            {
                _isInMissionMode = false;
                _nextSpawnCheckTime = Game.GameTime + 2000;
            }
        }

        Ped player = Game.Player.Character;

        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            if (player.IsInVehicle(_activeVehicle)) ReleaseVehicleToPlayer();
        }

        ManageCleanup(player);

        if (_activeVehicle == null)
        {
            if (Game.GameTime > _nextSpawnCheckTime)
            {
                ManageSpawning(player);
                _nextSpawnCheckTime = Game.GameTime + CheckInterval;
            }
        }
    }

    private void ReleaseVehicleToPlayer()
    {
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();

        _activeVehicle = null;
        _activeDriver = null;
        _activeBlip = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }

    private void ManageCleanup(Ped player)
    {
        if (_activeVehicle != null && !_activeVehicle.Exists())
        {
            RemoveResources();
            return;
        }

        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            if (player.Position.DistanceTo(_activeVehicle.Position) > DespawnDistance)
            {
                if (!player.IsInVehicle(_activeVehicle)) RemoveResources();
            }
        }
    }

    private bool IsRuggedZone(string zone)
    {
        HashSet<string> ruralZones = new HashSet<string> {
            "MTGORDO", "CMSW","GREATC", "WINDF", "ZANCUDO" , "LAGO", "ZQ_UAR",
            "PALFOR", "DESRT", "MTCHIL", "GALFISH", "CANNY", "CCREAK"
        };
        return ruralZones.Contains(zone);
    }

    // NEW: Helper to identify dangerous zones for long-distance spawning
    private bool IsHighRiskZone(string zone)
    {
        HashSet<string> riskZones = new HashSet<string> {
            "MTCHIL", "CANNY", "CCREAK", "PALETO", "PALFOR", "CMSW", "MTGORDO"
        };
        return riskZones.Contains(zone);
    }

    // =========================================================================
    //                        CORE SPAWNING LOGIC (UPDATED)
    // =========================================================================

    private void ManageSpawning(Ped player)
    {
        if (_rnd.Next(1, 101) > SpawnChance) return;
        if (IsZoneBanned(player.Position)) return;

        // BOSS LOGIC: Decide which specialist to call based on the zone
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, player.Position.X, player.Position.Y, player.Position.Z);

        if (IsRuggedZone(currentZone))
        {
            SpawnWilderness(player); // Use Strict Logic
        }
        else
        {
            SpawnCity(player);       // Use Aggressive Logic
        }
    }

    // SPECIALIST A: CITY (Vinewood, Downtown, Suburbs)
    // Aggressive, relaxed checks to fill gaps and handle hills.
    private void SpawnCity(Ped player)
    {
        // 1. BALANCED DISTANCE (150m)
        float testDist = 150.0f;
        Vector3 searchPos = player.Position + (player.ForwardVector * testDist);

        // Smart Check: If blocked (Corner/Hill), pull back to 90m to find the street.
        bool isVisible = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, searchPos.X, searchPos.Y, searchPos.Z, 2.0f);
        if (!isVisible)
        {
            testDist = 90.0f;
            searchPos = player.Position + (player.ForwardVector * testDist);
        }

        // 2. FIND ROAD (Asphalt Only - Arg 0)
        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 40.0f) return;
        if (IsZoneBanned(spawnPos)) return;

        // 3. RELAXED CHECKS (Fixes Vinewood Gaps)
        // Snap: Allow 60m variance to find parallel streets around blocks.
        float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(spawnPos.X, spawnPos.Y));
        if (snapDist > 60.0f) return;

        // Height: Allow 50m difference (Overpasses/Steep Hills)
        if (Math.Abs(spawnPos.Z - player.Position.Z) > 50.0f) return;

        // 4. SPAWN
        SpawnCandidate candidate = GetCandidateForLocation(spawnPos, false);
        if (string.IsNullOrEmpty(candidate.ModelName) || candidate.ModelName == _lastGlobalModel) return;

        CreateTrafficEntity(candidate, spawnPos, spawnHeading, false);
    }

    // SPECIALIST B: WILDERNESS (Mountains, Desert, Canyons)
    // Safe, strict checks to prevent glitches and manage trails.
    private void SpawnWilderness(Ped player)
    {
        // 1. BALANCED DISTANCE & RISK MANAGEMENT
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, player.Position.X, player.Position.Y, player.Position.Z);
        bool isHighRisk = IsHighRiskZone(currentZone);

        float testDist = 150.0f; // Default "Goldilocks" Distance

        if (isHighRisk)
        {
            // Cap at 130m in canyons to prevent wall glitches
            testDist = 130.0f;
        }
        else
        {
            // Open Rural: If hidden (Curve), pull back to 90m to stay on road.
            Vector3 longPos = player.Position + (player.ForwardVector * testDist);
            bool isVisible = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, longPos.X, longPos.Y, longPos.Z, 2.0f);
            if (!isVisible) testDist = 90.0f;
        }

        Vector3 searchPos = player.Position + (player.ForwardVector * testDist);

        // 2. ROAD DOMINANCE (Veto Logic)
        OutputArgument outMain = new OutputArgument();
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE, searchPos.X, searchPos.Y, searchPos.Z, outMain, 0, 3.0f, 0);
        Vector3 mainPos = outMain.GetResult<Vector3>();
        float distToMain = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(mainPos.X, mainPos.Y));

        int nodeArg = 0;
        bool forceOffroad = false;
        bool useVerticalCheck = true;

        // If Highway is within 30m, it wins.
        if (distToMain < 30.0f)
        {
            nodeArg = 0;
            forceOffroad = false;
            useVerticalCheck = true;
        }
        else
        {
            nodeArg = 1; // Trail Mode
            forceOffroad = true;
            useVerticalCheck = false;
        }

        // 3. EXECUTE SEARCH
        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, nodeArg, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 50.0f) return;
        if (IsZoneBanned(spawnPos)) return;

        // 4. SAFETY CHECKS (Dynamic based on Risk)
        float maxSnap = isHighRisk ? 20.0f : 80.0f; // Relaxed snap for open fields
        float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(spawnPos.X, spawnPos.Y));
        if (snapDist > maxSnap) return;

        float maxVert = isHighRisk ? 10.0f : 30.0f; // Relaxed vertical for rolling hills
        bool isFlying = player.IsInVehicle() && (player.CurrentVehicle.Model.IsHelicopter || player.CurrentVehicle.Model.IsPlane);
        if (useVerticalCheck && !isFlying)
        {
            if (Math.Abs(spawnPos.Z - player.Position.Z) > maxVert) return;
        }

        SpawnCandidate candidate = GetCandidateForLocation(spawnPos, forceOffroad);
        if (string.IsNullOrEmpty(candidate.ModelName) || candidate.ModelName == _lastGlobalModel) return;

        CreateTrafficEntity(candidate, spawnPos, spawnHeading, forceOffroad);
    }

    // =========================================================================

    private bool IsZoneBanned(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        return _bannedZones.Contains(zone);
    }

    private void CreateTrafficEntity(SpawnCandidate candidate, Vector3 pos, float heading, bool isTrailSpawn)
    {
        Model model = new Model(candidate.ModelName);
        if (!model.IsValid || !model.IsInCdImage) return;

        model.Request(500);
        if (!model.IsLoaded) return;

        Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 5.0f, false, false, false, false, false);

        _activeVehicle = World.CreateVehicle(model, pos, heading);

        if (_activeVehicle != null)
        {
            _activeVehicle.IsPersistent = true;
            _activeVehicle.IsEngineRunning = true;

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, _activeVehicle);
            if (comboCount > 0)
            {
                Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, _activeVehicle, _rnd.Next(0, comboCount));
            }

            CarMod.ApplyStyle(_activeVehicle, candidate.Behavior, candidate.ModelName);

            float driveSpeed = isTrailSpawn ? 6.0f : 20.0f;

            _activeDriver = _activeVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
            if (_activeDriver != null)
            {
                _activeDriver.BlockPermanentEvents = false;
                _activeDriver.Task.CruiseWithVehicle(_activeVehicle, driveSpeed,
               VehicleDrivingFlags.StopAtTrafficLights |
               VehicleDrivingFlags.StopForPeds |
               VehicleDrivingFlags.StopForVehicles |
               VehicleDrivingFlags.SteerAroundStationaryVehicles |
               VehicleDrivingFlags.SteerAroundObjects |
               VehicleDrivingFlags.AllowGoingWrongWay |
               VehicleDrivingFlags.ChangeLanesAroundObstructions
           );
            }


            if (ShowBlips)
            {
                _activeBlip = _activeVehicle.AddBlip();
                _activeBlip.Sprite = BlipSprite.PersonalVehicleCar;
                _activeBlip.Color = BlipColor.Purple;
                _activeBlip.Name = "Exotic Traffic";
                Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
            }

            _lastGlobalModel = candidate.ModelName;
        }
        model.MarkAsNoLongerNeeded();
    }

    private SpawnCandidate GetCandidateForLocation(Vector3 pos, bool forceOffroad)
    {
        if (forceOffroad)
        {
            return PickFromList(_trailProfile.PickList());
        }

        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);

        if (_bannedZones.Contains(zone)) return new SpawnCandidate();

        if (_zoneRegistry.ContainsKey(zone))
        {
            ZoneProfile profile = _zoneRegistry[zone];
            HashSet<string> selectedList = profile.PickList();

            if (selectedList != null)
            {
                return PickFromList(selectedList);
            }
        }

        return new SpawnCandidate();
    }

    private HashSet<string> SelectWeightedList(HashSet<string> common, HashSet<string> rare)
    {
        return (_rnd.Next(0, 100) < RareCarChance) ? rare : common;
    }

    private SpawnCandidate PickFromList(HashSet<string> list)
    {
        string modelName = VehicleSelector.GetNext(list, _excludedModels);
        if (string.IsNullOrEmpty(modelName)) return new SpawnCandidate();

        if (_behaviorRegistry.ContainsKey(list))
        {
            return new SpawnCandidate { ModelName = modelName, Behavior = _behaviorRegistry[list] };
        }

        return new SpawnCandidate { ModelName = modelName, Behavior = SpawnBehavior.Stock };
    }

    private void RemoveResources()
    {
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.Delete();

        _activeBlip = null;
        _activeDriver = null;
        _activeVehicle = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }

    private void ReleaseResourcesToGame()
    {
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();

        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();

        _activeBlip = null;
        _activeDriver = null;
        _activeVehicle = null;
        _nextSpawnCheckTime = Game.GameTime + CheckInterval;
    }
    private void OnAborted(object sender, EventArgs e) => RemoveResources();
}

public struct SpawnCandidate
{
    public string ModelName;
    public SpawnBehavior Behavior;
}

public class ZoneProfile
{
    private struct Ingredient
    {
        public HashSet<string> List;
        public int Weight;
    }

    private List<Ingredient> _ingredients = new List<Ingredient>();
    private int _totalWeight = 0;
    private Random _rnd = new Random();

    public void AddIngredient(HashSet<string> list, int weight)
    {
        _ingredients.Add(new Ingredient { List = list, Weight = weight });
        _totalWeight += weight;
    }

    public HashSet<string> PickList()
    {
        if (_ingredients.Count == 0) return null;

        int roll = _rnd.Next(0, _totalWeight);
        int current = 0;

        foreach (var item in _ingredients)
        {
            current += item.Weight;
            if (roll < current)
            {
                return item.List;
            }
        }
        return _ingredients[0].List;
    }
}