using GTA;
using GTA.Native;
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

public class VehicleConfigDumper : Script
{
    private readonly string _logFilePath = "scripts/CarMod_ExportedConfigs.txt";
    private int _nextAllowedPress = 0;

    public VehicleConfigDumper()
    {
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Check if F10 is pressed
        if (e.KeyCode == Keys.F10)
        {
            // Optional: If you still want to ensure the game is fully loaded/ready, 
            // you can check ModUtilities.IsModReady() right here.
            if (Game.GameTime > _nextAllowedPress)
            {
                DumpVehicleConfiguration();
                _nextAllowedPress = Game.GameTime + 1000; // 1 second cooldown
            }
        }
    }

    private void DumpVehicleConfiguration()
    {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists() || !player.IsInVehicle())
        {
            GTA.UI.Screen.ShowSubtitle("~r~Error:~w~ You must be inside a vehicle to dump configuration.");
            return;
        }

        Vehicle v = player.CurrentVehicle;
        string modelName = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v.Model.Hash).ToLower();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"        // --- EXPORTED CONFIG: {modelName.ToUpper()} ({v.LocalizedName}) ---");
        sb.AppendLine($"        {{ \"{modelName}\", v => {{");

        // 1. Colors & Pearlescent
        int primary = (int)v.Mods.PrimaryColor;
        int secondary = (int)v.Mods.SecondaryColor;
        int pearlescent = (int)v.Mods.PearlescentColor;
        int rimColor = (int)v.Mods.RimColor;
        sb.AppendLine($"            SetColors(v, {primary}, {secondary}, {pearlescent}, {rimColor});");

        // 2. Wheels
        if (v.Mods.WheelType != VehicleWheelType.Stock)
        {
            int wheelIndex = v.Mods[VehicleModType.FrontWheel].Index;
            sb.AppendLine($"            SetWheels(v, VehicleWheelType.{v.Mods.WheelType}, {wheelIndex});");
        }

        // 3. Window Tint
        if (v.Mods.WindowTint != VehicleWindowTint.None)
        {
            sb.AppendLine($"            v.Mods.WindowTint = VehicleWindowTint.{v.Mods.WindowTint};");
        }

        // 4. Toggle Mods (Turbo, Xenon)
        bool turboInstalled = v.Mods[VehicleToggleModType.Turbo].IsInstalled;
        if (!turboInstalled)
        {
            sb.AppendLine($"            v.Mods[VehicleToggleModType.Turbo].IsInstalled = false;");
        }

        bool xenonInstalled = v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled;
        if (xenonInstalled)
        {
            sb.AppendLine($"            v.Mods[VehicleToggleModType.XenonHeadlights].IsInstalled = true;");
        }

        // 5. Standard Body Mods
        foreach (VehicleModType modType in Enum.GetValues(typeof(VehicleModType)))
        {
            if (modType == VehicleModType.FrontWheel || modType == VehicleModType.RearWheel) continue;

            int modIndex = v.Mods[modType].Index;
            if (modIndex >= 0)
            {
                sb.AppendLine($"            v.Mods[VehicleModType.{modType}].Index = {modIndex};");
            }
        }

        sb.AppendLine("        }},");

        try
        {
            File.AppendAllText(_logFilePath, sb.ToString() + Environment.NewLine);
            GTA.UI.Screen.ShowSubtitle($"~g~CarMod Spec Saved:~w~ {modelName}");
        }
        catch (Exception ex)
        {
            GTA.UI.Screen.ShowSubtitle($"~r~Failed to write spec: {ex.Message}");
        }
    }
}