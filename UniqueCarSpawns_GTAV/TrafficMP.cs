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
    private float SpawnDistance = 150.0f; // Target Distance

    // Increased to accommodate Ghost Spawning fade-in time
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

    // FADE SYSTEM (Ghost Spawn Logic)
    private Vehicle _fadingVehicle;
    private int _fadingAlpha = 0;

    private bool _isInMissionMode = false;
    private string _lastGlobalModel = "";


    // Updated: Uses Shared Enum
    private Dictionary<HashSet<string>, SpawnBehavior> _behaviorRegistry;

    private HashSet<string> _excludedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
       "deveste", "sm722", "prototipo"
    };

    // ZONES
    private HashSet<string> _bannedZones = new HashSet<string> { "ARMYB", "LAGO", "JAIL", "AIRP", "ZQ_UAR", "TERMINA", "ELYSIAN", "PALMPOW", "PALCOV", "ELGORL", "ISHeist", "HORS", "PROL", "TATAMO", "MTJOSE" };
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


        // but since this is a simple script, they just get the general car lists unless defined here.
        // Kept Rural definitions for standard rural zones.
      //  AssignToProfile(ruralProfile, "GRAPES", "TONGVAH", "MTGORDO", "CMSW", "DESRT", "CANNY", "CCREAK", "MTCHIL", "GALFISH", "PALFOR", "PALETO");
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

        // ==========================================
        // FADE-IN LOGIC (Ghost Spawn System)
        // ==========================================
        if (_fadingVehicle != null && _fadingVehicle.Exists())
        {
            if (_fadingAlpha < 255)
            {
                _fadingAlpha += 15; // Fade Speed
                if (_fadingAlpha > 255) _fadingAlpha = 255;

                _fadingVehicle.Opacity = _fadingAlpha;
                Function.Call(Hash.SET_ENTITY_ALPHA, _fadingVehicle, _fadingAlpha, false);
            }
            else
            {
                Function.Call(Hash.RESET_ENTITY_ALPHA, _fadingVehicle);
                _fadingVehicle = null;
            }
        }
        // ==========================================

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
        if (_activeVehicle != null && _activeVehicle.Exists())
        {
            _activeVehicle.Opacity = 255;
            Function.Call(Hash.RESET_ENTITY_ALPHA, _activeVehicle);
        }

        if (_activeBlip != null && _activeBlip.Exists()) _activeBlip.Delete();
        if (_activeVehicle != null && _activeVehicle.Exists()) _activeVehicle.MarkAsNoLongerNeeded();
        if (_activeDriver != null && _activeDriver.Exists()) _activeDriver.MarkAsNoLongerNeeded();

        _activeVehicle = null;
        _activeDriver = null;
        _activeBlip = null;
        _fadingVehicle = null;
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

        // SMART DISTANCE: 
        // 1. Try 200m (Standard).
        float testDist = SpawnDistance;
        Vector3 searchPos = player.Position + (player.ForwardVector * testDist);

        // 2. Curve Check: If 200m is hidden (blocked by hill/curve), pull back to 110m.
        // This keeps the vector on the road and allows the "Snap Check" to pass.
        bool isVisible = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, searchPos.X, searchPos.Y, searchPos.Z, 2.0f);
        if (!isVisible)
        {
            testDist = 110.0f;
            searchPos = player.Position + (player.ForwardVector * testDist);
        }

        OutputArgument outPos = new OutputArgument();
        OutputArgument outHead = new OutputArgument();

        // Node Arg 0 = Main Roads (This works best for City)
        Function.Call(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, searchPos.X, searchPos.Y, searchPos.Z, outPos, outHead, 0, 3.0f, 0);

        Vector3 spawnPos = outPos.GetResult<Vector3>();
        float spawnHeading = outHead.GetResult<float>();

        if (spawnPos == Vector3.Zero) return;
        if (player.Position.DistanceTo(spawnPos) < 100.0f) return;
        if (IsZoneBanned(spawnPos)) return;

        // THE BACK ALLEY FIX:
        // Calculate lateral distance from our search vector to the found road.
        float snapDist = Vector2.Distance(new Vector2(searchPos.X, searchPos.Y), new Vector2(spawnPos.X, spawnPos.Y));

        // Logic:
        // In City: > 40m means it jumped over a building into an alley -> ABORT.
        // In Rural: > 80m is okay (fields are wide).
        string currentZone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, player.Position.X, player.Position.Y, player.Position.Z);
        bool isRural = (currentZone == "DESRT" || currentZone == "MTCHIL" || currentZone == "CANNY" || currentZone == "CCREAK" || currentZone == "GREATC");

        float maxSnap = isRural ? 80.0f : 40.0f;

        if (snapDist > maxSnap) return;

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
            // 1. GHOST SPAWN (Start Invisible)
            _activeVehicle.Opacity = 0;
            Function.Call(Hash.SET_ENTITY_ALPHA, _activeVehicle, 0, false);
            _fadingVehicle = _activeVehicle;
            _fadingAlpha = 0;

            _activeVehicle.IsPersistent = true;
            _activeVehicle.IsEngineRunning = true;

            // 2. PHYSICS FIX (Prevent U-Turn)
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, _activeVehicle, 5.0f);

            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, _activeVehicle);
            if (comboCount > 0)
            {
                Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, _activeVehicle, _rnd.Next(0, comboCount));
            }

            CarMod.ApplyStyle(_activeVehicle, candidate.Behavior, candidate.ModelName);

            // 3. APPLY SPEED (Cruise immediately)
            float driveSpeed = 20.0f; // ~45mph
            Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, _activeVehicle, driveSpeed);

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
               // REMOVED "AllowGoingWrongWay" to fix the U-Turn bug
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
        _fadingVehicle = null;
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
        _fadingVehicle = null;
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