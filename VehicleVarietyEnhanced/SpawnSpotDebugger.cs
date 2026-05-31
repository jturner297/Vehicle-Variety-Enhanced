using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.IO;
using System.Windows.Forms;

public class SpawnSpotDebugger : Script
{
    // This will create a text file in your GTA V 'scripts' folder
    private readonly string _logFilePath = "scripts/ParkedSpawns_Debug.txt";

    public SpawnSpotDebugger()
    {
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Trigger the save when F9 is pressed
        if (e.KeyCode == Keys.F9)
        {
            SaveCurrentSpot();
        }
    }

    private void SaveCurrentSpot()
    {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists()) return;

        Vector3 pos = player.Position;
        float heading = player.Heading;

        // Get the actual game zone (e.g., "VINE", "DOWNTOWN") to help you name the ID
        string zoneName = Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);

        string vehicleComment = "";

        // If the player is in a vehicle, grab the vehicle's exact heading and localized name
        if (player.IsInVehicle())
        {
            Vehicle v = player.CurrentVehicle;
            heading = v.Heading;
            vehicleComment = $" // Model: {v.LocalizedName}";
        }

        // Generate a unique ID using the zone and the current time so they don't overwrite
        string spotId = $"{zoneName}_{DateTime.Now:HHmmss}";

        // Placeholders you can quickly change in the text file later
        string vehPlaceholder = "VehList.models_super";
        string behaviorPlaceholder = "SpawnBehavior.Spec";

        // Format exactly like your ParkedDatabase.cs entries, appending the comment at the very end
        string formattedString = $"new SpawnSpot(\"{spotId}\", new Vector3({pos.X:F3}f, {pos.Y:F3}f, {pos.Z:F3}f), {heading:F3}f, {vehPlaceholder}, {behaviorPlaceholder}),{vehicleComment}";

        try
        {
            // Append the new spot to the text file
            File.AppendAllText(_logFilePath, formattedString + Environment.NewLine);

            // Show a visual confirmation on the screen so you know it worked
            GTA.UI.Screen.ShowSubtitle($"~g~Spot Saved:~w~ {spotId}");
        }
        catch (Exception ex)
        {
            GTA.UI.Screen.ShowSubtitle($"~r~Failed to save spot: {ex.Message}");
        }
    }
}