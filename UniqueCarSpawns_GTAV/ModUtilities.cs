using GTA;
using GTA.Native;

public static class ModUtilities
{

    // Global State Tracking for Startup Delay
    private static int _modUnlockTime = 0;

    public static void TriggerGlobalDelay(int delayMs)
    {
        _modUnlockTime = Game.GameTime + delayMs;
    }

    public static bool IsModReady()
    {
        var player = Game.Player.Character;
        if (player == null || !player.Exists()) return false;

        // Mod is ready if the timer has passed and the player is alive
        return Game.GameTime > _modUnlockTime && !player.IsDead;
    }
    public static bool IsPlayerInInterior()
    {
        int interiorId = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, Game.Player.Character);
        return interiorId != 0;
    }


    public static bool IsMissionOrCutsceneActive()
    {
        return Function.Call<bool>(Hash.GET_MISSION_FLAG) ||
               Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);
    }

    public static Blip CreateVehicleBlip(Vehicle car, BlipColor color = BlipColor.Blue)
    {
        Blip b = car.AddBlip();
        b.Sprite = BlipSprite.Standard;
        b.Color = color;
        b.Scale = ModSettings.BlipSize;
        b.IsShortRange = false;
        Function.Call(Hash.SHOW_HEIGHT_ON_BLIP, b, false);
   

        if (ModSettings.ShowVehicleNameOnBlips)
        {
            b.Name = Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, car.Model.Hash));
        }
        else
        {
            b.Name = "Vehicle";
        }

        Function.Call(Hash.FLASH_MINIMAP_DISPLAY);
        return b;
    }
}