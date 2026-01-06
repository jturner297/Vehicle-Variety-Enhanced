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
    private float SpawnDistance = 175.0f;
    private float DespawnDistance = 250.0f;
    private int SpawnChance = 100;
    private int CheckInterval = 15000;
    private int RareCarChance = 15; // Increased to 30% to fix the "Variety" issue
    // ==========================================

    private Vehicle _activeVehicle;
    private Ped _activeDriver;
    private Blip _activeBlip;
    private int _nextSpawnCheckTime = 0;
    private Random _rnd = new Random();
   
    private bool _isInMissionMode = false;
    private string _lastGlobalModel = "";


    // Updated: Uses Shared Enum
    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      //  "banshee3",
        "deveste",
      //  "turismo2",
       "sm722",
       "prototipo"
    };

    // ZONES
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "LAGO", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL", "EBURO", "CYPRE", "BANNIN", "TATAMO", "LMESA", "MURRI" };
    private HashSet<string> _richZones = new HashSet<string> { "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL", "PBLUFF", "GOLF", "MORN", "OBSERV", "HAWICK", "BURTON", "DELPE", "GALFISH" };
    private HashSet<string> _ghettoZones = new HashSet<string> { "CHAMH", "DAVIS", "RANCHO", "STRAW" };
    private HashSet<string> _urbanZones = new HashSet<string> { "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT", "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA" };

    public TrafficMP()
    {
        InitializeRegistry();
        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void InitializeRegistry()
    {
        _behaviorRegistry = new Dictionary<HashSet<string>, SpawnBehavior>();

        // Supers -> Spec
        _behaviorRegistry.Add(VehList.models_supers_common, SpawnBehavior.Spec);
        
        // City -> Spec (general upper class cars)
        _behaviorRegistry.Add(VehList.models_city, SpawnBehavior.Spec);

        // Classics -> Stock
        _behaviorRegistry.Add(VehList.models_classics_common, SpawnBehavior.Spec);


        // Lowriders -> RandomSpec
        _behaviorRegistry.Add(VehList.models_lowriders, SpawnBehavior.RandomSpec);


        // General Models -> Stock
        _behaviorRegistry.Add(VehList.models_general_common, SpawnBehavior.Spec);
        _behaviorRegistry.Add(VehList.models_general_rare, SpawnBehavior.Stock);
    }

    private void OnTick(object sender, EventArgs e)
    {
        bool isMissionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);

        /*if (isMissionActive)
        {
            RemoveResources();
            return;
        }*/
       
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
                _nextSpawnCheckTime = Game.GameTime + 2000; // Small delay before restarting traffic
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

    private void ManageSpawning(Ped player)
    {
        if (_rnd.Next(1, 101) > SpawnChance) return;
        if (IsZoneBanned(player.Position)) return;

        Vector3 searchPos = player.Position + (player.ForwardVector * SpawnDistance);
        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();

        // REVERTED: Changed 6th arg back to 0 (Any path) as per original file
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 100.0f) return;
        if (IsZoneBanned(spawnPos)) return;


        SpawnCandidate candidate = GetCandidateForLocation(spawnPos);

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
            // Function.Call(Hash.SET_VEHICLE_LIGHTS, _activeVehicle, 2);

            // INSERTED: Color Combination Logic (User Request)
            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, _activeVehicle);
            if (comboCount > 0)
            {
                Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, _activeVehicle, _rnd.Next(0, comboCount));
            }

            // Apply Style
            CarMod.ApplyStyle(_activeVehicle, candidate.Behavior, candidate.ModelName);

            _activeDriver = _activeVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
             if (_activeDriver != null)
             {
                 _activeDriver.BlockPermanentEvents = false;
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

    private SpawnCandidate GetCandidateForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        if (_bannedZones.Contains(zone)) return new SpawnCandidate();

        if (_richZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return PickFromList(SelectWeightedList(VehList.models_supers_common, VehList.models_city));
            else return PickFromList(SelectWeightedList(VehList.models_classics_common, VehList.models_city));

        }

        if (_ghettoZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return PickFromList(VehList.models_lowriders);
            return PickFromList(SelectWeightedList(VehList.models_general_common, VehList.models_general_rare));
        }

        if (_urbanZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return PickFromList(SelectWeightedList(VehList.models_general_common, VehList.models_general_rare));
            return PickFromList(VehList.models_city);
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
        // 1. Delete the blip (UI cleanup)
        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();

        // 2. Release the car and driver
        // They will continue driving their last path (Cruising) until the game cleans them up.
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();

        // 3. Reset script variables
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

