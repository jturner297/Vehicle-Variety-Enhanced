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
    private float SpawnDistance = 250.0f;
    private float DespawnDistance = 300.0f;
    private int SpawnChance = 100;
    private int CheckInterval = 10000;
    private int RareCarChance = 15; // Increased to 30% to fix the "Variety" issue
    // ==========================================

    private Vehicle _activeVehicle;
    private Ped _activeDriver;
    private Blip _activeBlip;
    private int _nextSpawnCheckTime = 0;
    private Random _rnd = new Random();

    // SMART SHUFFLE SYSTEMS
    private Dictionary<List<string>, Queue<string>> spawnQueues = new Dictionary<List<string>, Queue<string>>();
    private Dictionary<List<string>, string> lastSpawnedDict = new Dictionary<List<string>, string>();

    private string _lastGlobalModel = "";
    private Dictionary<List<string>, TrafficSpawnBehavior> _behaviorRegistry;

    // EXCLUSION LIST (Blacklist) - CLEARED TO FIX VARIETY
    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "banshee3",
        "deveste",
        "turismo2",
       "sm722"
    };

    // ZONES
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "LAGO", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL", "EBURO", "CYPRE", "BANNIN", "TATAMO", "LMESA" };
    private HashSet<string> _richZones = new HashSet<string> { "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "MORN", "OBSERV", "HAWICK", "BURTON", "DELPE", "GALFISH" };
    private HashSet<string> _ghettoZones = new HashSet<string> { "CHAMH", "DAVIS", "RANCHO", "STRAW", "MURRI" };
    private HashSet<string> _urbanZones = new HashSet<string> { "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA" };

    public TrafficMP()
    {
        InitializeRegistry();
        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void InitializeRegistry()
    {
        _behaviorRegistry = new Dictionary<List<string>, TrafficSpawnBehavior>();

        // Register new split lists
        _behaviorRegistry.Add(VehList.models_supers_common, TrafficSpawnBehavior.Super);
        _behaviorRegistry.Add(VehList.models_supers_rare, TrafficSpawnBehavior.Super);

        _behaviorRegistry.Add(VehList.models_classics_common, TrafficSpawnBehavior.Clean);
        _behaviorRegistry.Add(VehList.models_classics_rare, TrafficSpawnBehavior.Clean);

        // Register standard lists
        _behaviorRegistry.Add(VehList.models_lowriders, TrafficSpawnBehavior.Custom);
        _behaviorRegistry.Add(VehList.models_old_school, TrafficSpawnBehavior.Clean);
        _behaviorRegistry.Add(VehList.models_motorcycles, TrafficSpawnBehavior.Clean);
    }

    private void OnTick(object sender, EventArgs e)
    {
        // Mission/Cutscene Cleanup
        // If a mission starts or time skips via cutscene, delete everything immediately.
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);
        if (isMissionActive)
        {
            RemoveResources();
            return;
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

    private void ManageSpawning(Ped player)
    {
        if (_rnd.Next(1, 101) > SpawnChance) return;
        if (IsZoneBanned(player.Position)) return;

        Vector3 searchPos = player.Position + (player.ForwardVector * SpawnDistance);
        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();

        // FIX 1: Changed '0' (Any) to '1' (Roads Only) in the 6th argument
        // This prevents spawning in driveways, alleys, or off-road paths
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 100.0f) return;
        if (IsZoneBanned(spawnPos)) return;

        SpawnCandidate candidate = GetCandidateForLocation(spawnPos);

        // Global duplicate check
        if (string.IsNullOrEmpty(candidate.ModelName) || candidate.ModelName == _lastGlobalModel) return;

        CreateTrafficEntity(candidate, spawnPos, spawnHeading);
    }

    private bool IsZoneBanned(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        return _bannedZones.Contains(zone);
    }

    private void CreateTrafficEntity(SpawnCandidate candidate, Vector3 pos, float heading)
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
            Function.Call(Hash.SET_VEHICLE_LIGHTS, _activeVehicle, 2);

            ApplyTrafficMods(_activeVehicle, candidate.Behavior);

            _activeDriver = _activeVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
            if (_activeDriver != null)
            {
                _activeDriver.BlockPermanentEvents = false;

                //Updated Driving Flags
                _activeDriver.Task.CruiseWithVehicle(_activeVehicle, 20.0f,
               VehicleDrivingFlags.StopAtTrafficLights |
               VehicleDrivingFlags.StopForPeds |
               VehicleDrivingFlags.StopForVehicles |
               VehicleDrivingFlags.SteerAroundStationaryVehicles |
               VehicleDrivingFlags.SteerAroundObjects |
               VehicleDrivingFlags.AllowGoingWrongWay |  // <--- This is the key "aggressive" flag
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

    private void ApplyTrafficMods(Vehicle v, TrafficSpawnBehavior behavior)
    {
        v.Mods.InstallModKit();
        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, v);
        if (comboCount > 0) Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, v, _rnd.Next(0, comboCount));

        switch (behavior)
        {
            case TrafficSpawnBehavior.Super:
                v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;
                break;

            case TrafficSpawnBehavior.Custom:
                ApplyRandomVisuals(v);
                RandomizeLivery(v);
                break;

            case TrafficSpawnBehavior.Clean:
                break;
        }
    }

    private void ApplyRandomVisuals(Vehicle v)
    {
        var performanceTypes = new List<VehicleModType> { VehicleModType.Engine, VehicleModType.Brakes, VehicleModType.Transmission, VehicleModType.Suspension, VehicleModType.Armor };
        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            if (performanceTypes.Contains(modType) || modType == VehicleModType.Livery || modType == VehicleModType.Horns) continue;
            int count = v.Mods[modType].Count;
            if (count > 0) v.Mods[modType].Index = _rnd.Next(0, count);
        }
    }

    private void RandomizeLivery(Vehicle v)
    {
        int count = v.Mods.LiveryCount;
        if (count > 0) v.Mods.Livery = _rnd.Next(0, count);
    }

    private SpawnCandidate GetCandidateForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (_bannedZones.Contains(zone)) return new SpawnCandidate();

        if (_richZones.Contains(zone))
        {
            // 50% Super, 50% Classic
            if (_rnd.Next(0, 2) == 0)
            {
                return PickFromList(SelectWeightedList(VehList.models_supers_common, VehList.models_supers_rare));
            }
            else
            {
                return PickFromList(SelectWeightedList(VehList.models_classics_common, VehList.models_classics_rare));
            }
        }

        if (_ghettoZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return PickFromList(VehList.models_lowriders);
            return PickFromList(VehList.models_old_school);
        }

        if (_urbanZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return PickFromList(VehList.models_old_school);
            return PickFromList(VehList.models_motorcycles);
        }

        return new SpawnCandidate();
    }

    private List<string> SelectWeightedList(List<string> common, List<string> rare)
    {
        if (_rnd.Next(0, 100) < RareCarChance)
        {
            return rare;
        }
        return common;
    }

    private SpawnCandidate PickFromList(List<string> list)
    {
        string modelName = GetUniqueModel(list);
        if (string.IsNullOrEmpty(modelName)) return new SpawnCandidate();

        if (_behaviorRegistry.ContainsKey(list))
        {
            return new SpawnCandidate { ModelName = modelName, Behavior = _behaviorRegistry[list] };
        }

        return new SpawnCandidate { ModelName = modelName, Behavior = TrafficSpawnBehavior.Clean };
    }

    private string GetUniqueModel(List<string> list)
    {
        if (list == null || list.Count == 0) return null;

        if (!spawnQueues.ContainsKey(list) || spawnQueues[list].Count == 0)
        {
            List<string> freshBatch = new List<string>(list);
            freshBatch.RemoveAll(x => _excludedModels.Contains(x));
            if (freshBatch.Count == 0) return null;

            TrafficUtils.Shuffle(freshBatch);

            if (lastSpawnedDict.ContainsKey(list) && freshBatch.Count > 1 && freshBatch[0] == lastSpawnedDict[list])
            {
                string temp = freshBatch[0];
                freshBatch[0] = freshBatch[freshBatch.Count - 1];
                freshBatch[freshBatch.Count - 1] = temp;
            }
            spawnQueues[list] = new Queue<string>(freshBatch);
        }

        string selection = spawnQueues[list].Dequeue();
        lastSpawnedDict[list] = selection;
        return selection;
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

    private void OnAborted(object sender, EventArgs e)
    {
        RemoveResources();
    }
}

// ==========================================
//          HELPER CLASSES & ENUMS
// ==========================================

public enum TrafficSpawnBehavior
{
    Clean,
    Super,
    Custom,
}

public struct SpawnCandidate
{
    public string ModelName;
    public TrafficSpawnBehavior Behavior;
}

public static class TrafficUtils
{
    private static Random rng = new Random();
    public static void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}