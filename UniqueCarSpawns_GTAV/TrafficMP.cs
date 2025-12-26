using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficMP : Script
{
    private Vehicle _spawnedVehicle;
    private Ped _driver;
    private Blip _trafficBlip;

    private int _nextSpawnTime = 0;
    private int _nextSearchTime = 0;

    private float SpawnDistance = 300.0f;
    private float DespawnDistance = 500.0f;
    private int RespawnDelayMs = 3000;
    private const int SearchIntervalMs = 500;

    private int _blipColor;
    private int _trafficBlipConfig;
    private int _streetFlag;

    private Random _rnd = new Random();
    private HashSet<int> _dlcModelHashes = new HashSet<int>();
    private int _lastPlayerVehicleHandle = 0;

    // List of rich zones
    private HashSet<string> _richZones = new HashSet<string>
    {
        "RICHM", "RGLEN", "ROCKF", "VINE", "DTVINE", "WVINE",
        "CHIL", "PBLUFF", "GOLF", "MORN", "OBSERV"
    };

    public TrafficMP()
    {
        Tick += OnTick;
        Aborted += OnAborted;

        ScriptSettings config = ScriptSettings.Load("Scripts\\UniqueCarSpawns.ini");
        _blipColor = config.GetValue<int>("MAIN", "blip_color_traffic", 3);
        _trafficBlipConfig = config.GetValue<int>("MAIN", "traffic_cars_blips", 0);
        _streetFlag = config.GetValue<int>("MAIN", "spawn_traffic", 1);
        RespawnDelayMs = config.GetValue<int>("MAIN", "time_traffic_gen", 3000);

        SpawnDistance = config.GetValue<float>("ADVANCED", "SpawnDistance", 300.0f);
        DespawnDistance = config.GetValue<float>("ADVANCED", "DespawnDistance", 500.0f);

        BuildDlcCache();
    }

    private void BuildDlcCache()
    {
        try
        {
            FieldInfo[] fields = typeof(VehList).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(List<string>))
                {
                    List<string> list = (List<string>)field.GetValue(null);
                    if (list != null)
                        foreach (string modelName in list) _dlcModelHashes.Add(Game.GenerateHash(modelName));
                }
            }
        }
        catch (Exception ex)
        {
            GTA.UI.Notification.Show($"~r~TrafficMP Cache Error:~w~ {ex.Message}");
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_streetFlag == 0) return;

        Ped player = Game.Player.Character;

        // 1. SAFETY CHECK: If the script thinks we have a car, but the game deleted it
        if (_spawnedVehicle != null && !_spawnedVehicle.Exists())
        {
            CleanUp();
            _nextSpawnTime = Game.GameTime + 1000;
        }

        // Logic to prevent spamming spawns if player is already driving a DLC car
        if (player.IsInVehicle())
        {
            Vehicle playerVeh = player.CurrentVehicle;
            if (playerVeh.Handle != _lastPlayerVehicleHandle)
            {
                if (IsDlcVehicle(playerVeh))
                {
                    if (_spawnedVehicle != null) CleanUp();
                    _nextSpawnTime = Game.GameTime + RespawnDelayMs;
                }
                _lastPlayerVehicleHandle = playerVeh.Handle;
            }
        }
        else
        {
            _lastPlayerVehicleHandle = 0;
        }

        // 2. DESPAWN LOGIC: If car exists and is too far
        if (_spawnedVehicle != null && _spawnedVehicle.Exists())
        {
            if (player.Position.DistanceTo(_spawnedVehicle.Position) > DespawnDistance)
            {
                CleanUp();
                _nextSpawnTime = Game.GameTime + RespawnDelayMs;
            }
        }
        // 3. SPAWN LOGIC: If no car exists, try to spawn one
        else if (Game.GameTime >= _nextSpawnTime)
        {
            if (Game.GameTime >= _nextSearchTime)
            {
                AttemptSpawnOptimized();
                _nextSearchTime = Game.GameTime + SearchIntervalMs;
            }
        }
    }

    private bool IsDlcVehicle(Vehicle v)
    {
        return _dlcModelHashes.Contains(v.Model.Hash);
    }

    private void AttemptSpawnOptimized()
    {
        Ped playerPed = Game.Player.Character;
        Vector3 playerPos = playerPed.Position;
        Vector3 playerForward = playerPed.ForwardVector;

        // Look for a spawn point in front of the player
        Vector3 targetSearchPos = playerPos + (playerForward * SpawnDistance);

        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();

        // Find a valid road node
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
            targetSearchPos.X, targetSearchPos.Y, targetSearchPos.Z,
            outPos, outHead, 1, 3.0f, 0);

        Vector3 spawnNodePos = outPos.GetResult<Vector3>();
        float spawnNodeHeading = outHead.GetResult<float>();

        if (spawnNodePos != Vector3.Zero)
        {
            // Ensure we don't spawn right on top of the player
            if (playerPos.DistanceTo(spawnNodePos) > 60.0f)
            {
                string modelToSpawn = GetModelFromContext(spawnNodePos);

                if (!string.IsNullOrEmpty(modelToSpawn))
                {
                    SpawnTrafficVehicle(spawnNodePos, spawnNodeHeading, modelToSpawn);
                }
            }
        }
    }

    private string GetModelFromContext(Vector3 position)
    {
        string zoneName = Function.Call<string>(Hash.GET_NAME_OF_ZONE, position.X, position.Y, position.Z);
        bool isRichZone = _richZones.Contains(zoneName);

        // --- TEST MODE: ONLY SUPERS AND CLASSICS ---

        if (isRichZone)
        {
            // 1. RICH ZONES -> SUPERS
            return GetRandomModel(VehList.models_supers);
        }
        else
        {
            // 2. EVERYWHERE ELSE -> CLASSICS
            return GetRandomModel(VehList.models_classics);
        }
    }

    private string GetRandomModel(List<string> list)
    {
        if (list == null || list.Count == 0) return null;
        return list[_rnd.Next(list.Count)];
    }

    private void SpawnTrafficVehicle(Vector3 position, float heading, string modelName)
    {
        Model carModel = new Model(modelName);

        if (!carModel.IsValid || !carModel.IsInCdImage) return;

        carModel.Request(500);
        if (!carModel.IsLoaded) return;

        // Clear area slightly
        Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, position.X, position.Y, position.Z, 6.0f, false, false, false, false, false);

        _spawnedVehicle = World.CreateVehicle(carModel, position, heading);

        if (_spawnedVehicle != null)
        {
            if (_trafficBlipConfig == 1) CreateMarkerAboveCar(_spawnedVehicle);

            _spawnedVehicle.IsPersistent = true; // IMPORTANT: Prevents game from deleting it instantly
            _spawnedVehicle.IsEngineRunning = true;
            _spawnedVehicle.AreLightsOn = true;

            // Create visible driver
            _driver = _spawnedVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);

            if (_driver != null)
            {
                _driver.IsVisible = true;
                _driver.CanBeTargetted = true;
                _driver.BlockPermanentEvents = false;
                _driver.Task.CruiseWithVehicle(_spawnedVehicle, 20.0f, DrivingStyle.Normal);
            }
        }

        carModel.MarkAsNoLongerNeeded();
    }

    private void CleanUp()
    {
        // 1. Force delete the Blip immediately
        if (_trafficBlip != null && _trafficBlip.Exists())
        {
            _trafficBlip.Delete();
        }
        _trafficBlip = null;

        // 2. Clean up driver
        if (_driver != null && _driver.Exists())
        {
            _driver.Delete();
        }
        _driver = null;

        // 3. Clean up vehicle
        if (_spawnedVehicle != null && _spawnedVehicle.Exists())
        {
            _spawnedVehicle.Delete();
        }
        // IMPORTANT: Set this to null so the script knows it's free to spawn a new one
        _spawnedVehicle = null;
    }

    private void CreateMarkerAboveCar(Vehicle car)
    {
        if (_trafficBlip != null && _trafficBlip.Exists()) _trafficBlip.Delete();

        _trafficBlip = Function.Call<Blip>(Hash.ADD_BLIP_FOR_ENTITY, car);
        Function.Call(Hash.SET_BLIP_SPRITE, _trafficBlip, 1);
        Function.Call(Hash.SET_BLIP_COLOUR, _trafficBlip, _blipColor);
        Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
        _trafficBlip.Name = "Unique vehicle";
    }

    private void OnAborted(object sender, EventArgs e)
    {
        CleanUp();
    }
}