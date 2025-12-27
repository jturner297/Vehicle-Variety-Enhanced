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
    private bool ShowBlips = true;          // Set 'false' to hide map markers
    private float SpawnDistance = 175.0f;   // Distance ahead of player to spawn
    private float DespawnDistance = 200.0f; // Distance to delete the car
    private int SpawnChance = 100;          // 1-100% chance to spawn when slot is open

    // Interval: 5 Seconds (Waits this long AFTER a car disappears before finding a new one)
    private int CheckInterval = 5000;
    // ==========================================

    // State Tracking
    private Vehicle _activeVehicle;
    private Ped _activeDriver;
    private Blip _activeBlip;
    private int _nextSpawnCheckTime = 0;
    private Random _rnd = new Random();

    // SMART SHUFFLE SYSTEMS
    private Dictionary<List<string>, Queue<string>> spawnQueues = new Dictionary<List<string>, Queue<string>>();
    private Dictionary<List<string>, string> lastSpawnedDict = new Dictionary<List<string>, string>();

    // ==========================================
    //              EXCLUSION LIST (BLACKLIST)
    // ==========================================
    // Cars listed here will NEVER spawn in traffic.
    private HashSet<string> _excludedModels = new HashSet<string>
    {
        "turismo2", //Banned from traffic
         "sm722",
           "prototipo"
    };

    // ==========================================
    //              ZONE DEFINITIONS
    // ==========================================

    // 0. BANNED ZONES
    private HashSet<string> _bannedZones = new HashSet<string>
    {
        "ARMYB", "LAGO", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN",
        "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL",
        "EBURO", "CYPRE", "BANNIN", "TATAMO", "LMESA"
    };

    // 1. RICH / UPSCALE (Supers & Classics)
    private HashSet<string> _richZones = new HashSet<string>
    {
        "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE", "CHIL",
        "PBLUFF", "GOLF", "MORN", "OBSERV", "HAWICK", "BURTON", "DELPE", "GALFISH"
    };

    // 2. GHETTO / SOUTH CENTRAL (Lowriders & Old School)
    private HashSet<string> _ghettoZones = new HashSet<string>
    {
        "CHAMH", "DAVIS", "RANCHO", "STRAW", "MURRI"
    };

    // 3. URBAN / CITY CENTER (Old School / Vintage)
    private HashSet<string> _urbanZones = new HashSet<string>
    {
        "DOWNT", "TEXTI", "SKID", "PBOX", "LEGSQU", "KOREAT",
        "VESP", "VCANA", "DELSOL", "MIRR", "EAST_V", "ALTA"
    };

    // 4. RURAL / DESERT (Empty)
    private HashSet<string> _ruralZones = new HashSet<string>
    {
        "ALAMO", "DESRT", "SANDY", "GRAPES", "HARMO", "SLAB", "MTCHIL",
        "MTGORDO", "PALETO", "PALFOR", "CMSW", "ZANCUDO", "TONGVAH",
        "TONGVAV", "BANHAMC", "BHAMCA", "CHU", "NCHU", "CCREAK",
        "CALAFB", "BRADP", "BRADT", "WINDF"
    };

    public TrafficMP()
    {
        Tick += OnTick;
        Aborted += OnAborted;
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        // 1. THEFT CHECK
        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            if (player.IsInVehicle(_activeVehicle))
            {
                ReleaseVehicleToPlayer();
            }
        }

        // 2. CLEANUP MANAGER
        ManageCleanup(player);

        // 3. SPAWN MANAGER
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
                if (!player.IsInVehicle(_activeVehicle))
                {
                    RemoveResources();
                }
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

        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
            searchPos.X, searchPos.Y, searchPos.Z,
            outPos, outHead, 0, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 100.0f) return;

        if (IsZoneBanned(spawnPos)) return;

        string modelName = GetModelForLocation(spawnPos);
        if (string.IsNullOrEmpty(modelName)) return;

        CreateTrafficEntity(modelName, spawnPos, spawnHeading);
    }

    private bool IsZoneBanned(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
        return _bannedZones.Contains(zone);
    }

    private void CreateTrafficEntity(string modelName, Vector3 pos, float heading)
    {
        Model model = new Model(modelName);
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

            ApplyTrafficMods(_activeVehicle, modelName);

            _activeDriver = _activeVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
            if (_activeDriver != null)
            {
                _activeDriver.BlockPermanentEvents = false;

                // [FIXED] Updated Flags: StopForVehicles & StopForPeds
                _activeDriver.Task.CruiseWithVehicle(_activeVehicle, 20.0f,
                    VehicleDrivingFlags.StopAtTrafficLights |
                    VehicleDrivingFlags.StopForVehicles |
                    VehicleDrivingFlags.StopForPeds);
            }

            if (ShowBlips)
            {
                _activeBlip = _activeVehicle.AddBlip();
                _activeBlip.Sprite = BlipSprite.PersonalVehicleCar;
                _activeBlip.Color = BlipColor.Purple;
                _activeBlip.Name = "Exotic Traffic";
                Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
            }
        }

        model.MarkAsNoLongerNeeded();
    }

    private void ApplyTrafficMods(Vehicle v, string modelName)
    {
        v.Mods.InstallModKit();

        if (v.ClassType == VehicleClass.Super || v.ClassType == VehicleClass.Sports)
        {
            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;
        }

        int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, v);
        if (comboCount > 0)
        {
            Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, v, _rnd.Next(0, comboCount));
        }

        if (VehList.models_lowriders.Contains(modelName))
        {
            ApplyRandomVisuals(v);
            RandomizeLivery(v);
        }
    }

    private void ApplyRandomVisuals(Vehicle v)
    {
        var performanceTypes = new List<VehicleModType> {
            VehicleModType.Engine,
            VehicleModType.Brakes,
            VehicleModType.Transmission,
            VehicleModType.Suspension,
            VehicleModType.Armor
        };

        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            if (performanceTypes.Contains(modType) || modType == VehicleModType.Livery || modType == VehicleModType.Horns) continue;

            int count = v.Mods[modType].Count;
            if (count > 0)
            {
                v.Mods[modType].Index = _rnd.Next(0, count);
            }
        }
    }

    private void RandomizeLivery(Vehicle v)
    {
        int count = v.Mods.LiveryCount;
        if (count > 0) v.Mods.Livery = _rnd.Next(0, count);
    }

    private string GetModelForLocation(Vector3 pos)
    {
        string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);

        if (_bannedZones.Contains(zone)) return null;

        if (_richZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return GetUniqueModel(VehList.models_supers);
            return GetUniqueModel(VehList.models_classics);
        }

        if (_ghettoZones.Contains(zone))
        {
            if (_rnd.Next(0, 2) == 0) return GetUniqueModel(VehList.models_lowriders);
            return GetUniqueModel(VehList.models_old_school);
        }

        if (_urbanZones.Contains(zone))
        {
            return GetUniqueModel(VehList.models_old_school);
        }

        return null;
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