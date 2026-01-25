using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public class TrafficColors : Script
{
    // =============================================================
    //                          SETTINGS
    // =============================================================

    private int _checkInterval = 250;

    // PACING: Only change 40% of traffic. 
    private int _swapChance = 50;
    private bool _usePureRandom = true;
    // BLACKLIST: Specific models to IGNORE (e.g. Utility/Service vehicles)
    private HashSet<string> _excludedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "boxville", "boxville2", "taxi", "trash", "trash2", "boxville4", "tractor2", "tractor"
    };

    // =============================================================

    private int _nextCheckTime = 0;
    private Random _rnd = new Random();
    private bool _debugMode = false;

    private HashSet<int> _processedVehicles = new HashSet<int>();
    private List<Blip> _activeBlips = new List<Blip>();

    private HashSet<int> _excludedHashes = new HashSet<int>();

    public TrafficColors()
    {
        foreach (string name in _excludedModelNames)
        {
            int hash = Function.Call<int>(Hash.GET_HASH_KEY, name);
            _excludedHashes.Add(hash);
        }

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            _debugMode = !_debugMode;
            string status = _debugMode ? "~g~ON" : "~r~OFF";
            string mode = _usePureRandom ? "Pure Random" : "Carcols";
            GTA.UI.Notification.PostTicker($"TrafficColors: {status} | Mode: {mode} | Tracked: {_processedVehicles.Count}", true);
            ToggleBlipVisibility(_debugMode);
        }

        // Quick Toggle for testing modes in-game
        if (e.KeyCode == Keys.F12 && _debugMode)
        {
            _usePureRandom = !_usePureRandom;
            string mode = _usePureRandom ? "~b~Pure Random" : "~y~Carcols Preset";
            GTA.UI.Notification.PostTicker($"TrafficColors Mode Switched: {mode}", true);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.GameTime > _nextCheckTime)
        {
            CleanupLists();
            ProcessTraffic();
            _nextCheckTime = Game.GameTime + _checkInterval;
        }

        if (_debugMode) DrawDebugMarkers();
    }

    private void OnAborted(object sender, EventArgs e)
    {
        foreach (var b in _activeBlips) if (b.Exists()) b.Delete();
        _processedVehicles.Clear();
    }

    private void ProcessTraffic()
    {
        Vehicle[] allVehicles = World.GetAllVehicles();
        Vehicle lastVehicle = Game.Player.LastVehicle;

        foreach (Vehicle v in allVehicles)
        {
            if (v == null || !v.Exists()) continue;
            if (_processedVehicles.Contains(v.Handle)) continue;

            // Protection
            if (v.Driver == Game.Player.Character) continue;
            if (lastVehicle != null && v.Handle == lastVehicle.Handle) continue;
            if (v.IsPersistent) continue;
            if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) continue;

            // Exclusion
            // 1. Check Categories
            // We group these boolean checks for speed and readability
            bool isRestrictedClass =
                v.ClassType == VehicleClass.Emergency ||
                v.ClassType == VehicleClass.Utility ||
                v.ClassType == VehicleClass.Service ||
                v.ClassType == VehicleClass.Industrial ||
                v.ClassType == VehicleClass.Military ||
                v.ClassType == VehicleClass.Commercial ||
                v.ClassType == VehicleClass.Cycles; // Added Cycles (Bikes shouldn't get random paint)

            // 2. Check Types
            bool isRestrictedType =
                v.Model.IsTrain ||
                v.Model.IsBoat ||
                v.Model.IsHelicopter ||
                v.Model.IsPlane;

            // 3. Check Blacklist (Specific Models)
            bool isBlacklisted = _excludedHashes.Contains(v.Model.Hash);

            // 4. MASTER EXCLUSION
            if (isRestrictedClass || isRestrictedType || isBlacklisted)
            {
                // CRITICAL: Mark it as processed! 
                // We tell the script: "We have seen this car, and we decided to ignore it."
                // This prevents re-checking it every single frame.
                _processedVehicles.Add(v.Handle);
                continue;
            }

            // Action
            if (_rnd.Next(0, 100) < _swapChance)
            {
                ApplyColorLogic(v);
                if (_debugMode) AddDebugBlip(v);
            }

            _processedVehicles.Add(v.Handle);
        }
    }

    private void ApplyColorLogic(Vehicle v)
    {
        if (_usePureRandom)
        {
            // --- PURE RANDOM MODE ---
            // Pick ONE color ID (0 to 159)
            int randomColor = _rnd.Next(0, 160);

            // Apply to Primary and Secondary
            Function.Call(Hash.SET_VEHICLE_COLOURS, v, randomColor, randomColor);

            // Apply to Pearlescent (Arg 2) and Wheel (Arg 3)
            // We set Pearl to same color to make it look "Deep/Factory" rather than "Clown"
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, v, randomColor, 0);
        }
        else
        {
            // --- PRESET MODE (Carcols) ---
            int comboCount = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS, v);
            if (comboCount > 0)
            {
                int randomID = _rnd.Next(0, comboCount);
                Function.Call(Hash.SET_VEHICLE_COLOUR_COMBINATION, v, randomID);
            }
        }
    }

    // ==========================================
    //           CLEANUP & DEBUG
    // ==========================================
    private void CleanupLists()
    {
        for (int i = _activeBlips.Count - 1; i >= 0; i--)
        {
            if (!_activeBlips[i].Exists()) _activeBlips.RemoveAt(i);
        }

        if (_processedVehicles.Count > 200)
        {
            _processedVehicles.RemoveWhere(handle => !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle));
        }
    }

    private void AddDebugBlip(Vehicle v)
    {
        Blip b = v.AddBlip();
        b.Sprite = BlipSprite.PersonalVehicleCar;
        b.Color = BlipColor.Purple;
        b.Scale = 0.6f;
        b.Name = "Randomized Color";
        b.IsShortRange = true;
        b.Alpha = _debugMode ? 255 : 0;
        _activeBlips.Add(b);
    }

    private void ToggleBlipVisibility(bool visible)
    {
        foreach (var b in _activeBlips)
        {
            if (b.Exists()) b.Alpha = visible ? 255 : 0;
        }
    }

    private void DrawDebugMarkers()
    {
        // Debug marker logic is purely visual, no need to over-process
    }
}