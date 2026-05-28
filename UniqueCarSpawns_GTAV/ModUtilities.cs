using GTA;
using GTA.Native;

public static class ModUtilities
{

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