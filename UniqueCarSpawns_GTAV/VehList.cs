
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.IO;

public class VehList
{
    public static List<string> models_cult = new List<string>() {
    "autarch",
    "sc1",
    "cyclone",
    "visione",
    "xa21",
    "gp1",
    "nero",
    "nero2",
    "tempesta",
    "penetrator",
    "pfister811",
    "prototipo",
    "reaper",
    "fmj",
    "t20",
    "osiris",
    "rebla",
    "novak",
    "sm722"
    };

    public static List<string> models_boats = new List<string>() {
    "longfin",
    "toro"
    };

    public static List<string> models_cemetery = new List<string>() {
    "tornado6",
    "btype2",
    "sanctus",
    "lurcher",
    "brigham",
    };

    public static List<string> models_cheburek = new List<string>() {
    "cheburek",
    "ratbike",
   "clique2",
   "peyote2",
   "weevil2"
    };

    public static List<string> models_cinema = new List<string>() {
    "scramjet",
    "vigilante",
    "voltic2",
    "toreador",
    "jb7002",
    "deluxo",
    "stromberg",
    "rrocket",
    "shotaro",
    "dune5"
    };

    public static List<string> models_cluckin = new List<string>() {
    "benson2"
    };

    public static List<string> models_compacts = new List<string>() {
    "brioso",
    "brioso2",
    "brioso3",
    "weevil",
    "club",
    "kanjo",
    "asbo",
    "issi3"
    };

    public static List<string> models_coupes = new List<string>() {
    "kanjosj",
    "postlude",
    "previon",
    "windsor2",
    "windsor",
    "fr36",
    "eurosX32",
    };



    public static List<string> models_lowriders = new List<string>() {
    "eudora",
        "faction3",
        "Buccaneer2",
     "Chino2",
     "Faction2",
     "Moonbeam2",
     "Primo2",
     "Voodoo",
     "SlamVan3",
     "Tornado5",
     "Minivan2",
     "Peyote3",
     "Yosemite2",
     "Chimera",
     "Glendale2",
     "Manana2",
     "SabreGT2",
        "virgo2"
    };
    public static List<string> models_poor = new List<string>() {
        "greenwood",
        "retinue2",
    "dynasty",
    "cheburek",
    "fagaloa",
    "greenwood",
    };

    public static List<string> models_helicopter = new List<string>() {
    "conada",
    "swift2",
    "Havok",
    "Volatus",
    "SeaSparrow",
    "Supervolito",
    "Supervolito2",
    "Swift2"
    };

    public static List<string> models_humanlabs = new List<string>() {
    "brickade2",
    };


    public static List<string> models_karting = new List<string>() {
    "veto",
    "veto2",
     "locust",
          "ruston",
    };



    public static string thruster_model = "thruster";
    public static string apc_model = "apc";
    public static string raiju_model = "raiju";
    public static string conada2_model = "conada2";

    public static List<string> models_military_planes = new List<string>() {
    "strikeforce",
    "nokota",
    "pyro",
    "mogul",
       "raiju",
    "molotok",
    "tula",
    "rogue",
    "starling",
    "hydra",
    };

    public static List<string> models_military_helicopters = new List<string>() {
    "annihilator2",
    "hunter",
    "valkyrie",
    "savage",
     "conada2",
    };

    public static List<string> models_military_opressors = new List<string>() {
    "oppressor",
    "oppressor2",
    };

    public static List<string> models_military_bikes = new List<string>() {
    "squaddie",
    "manchez2",
    "winky",
    "insurgent3",
    };

    public static List<string> models_motorcycles = new List<string>() {
    "manchez3",
    "shinobi",
    "manchez2",
    "stryder",
    "fcr2",
    "fcr",
    "diablous",
    "diablous2",
    "esskey",
    "vortex",
    "daemon2",
    "zombiea",
    "zombieb",
    "wolfsbane",
    "nightblade",
    "manchez",
    "hakuchou2",
    "faggio",
    "faggio3",
    "defiler",
    //"chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
    "bf400",
    "vindicator",
    "lectro",
    "enduro",
    };



    public static List<string> models_beach = new List<string>() {
    "pbus2",
    };

    public static List<string> models_planes = new List<string>() {
    "Nimbus",
    "Luxor2",
    "Velum2",
    "Microlight",
    "Seabreeze",
    "Howard",
    "Rogue",
    "AlphaZ1",
    "streamer216"
    };

 






  

    public static List<string> models_submarine = new List<string>() {
    "avisa",
    };

    public static List<string> models_supers = new List<string>() {
    //base game traffic cars 
       "zentorno",
       "turismor",
       "cheetah",
       "entityxf",
     //  "vacca", 
        
        //base game parked cars
  // "adder", 
        
        //traffic  cars
        "tempesta",
//"italigtb",
//"sc1",
"reaper",
//"penetrator",
        //parked  cars
        "t20",
//"pfister811",
"osiris",
"gp1",
"xa21",
"fmj",
//"cyclone",
"prototipo",
"nero",
"nero2",
"visione",
"sm722",

    };

 



 

    public static List<string> models_valentine = new List<string>() {
    "btype3",
    "broadway",
    "stafford",
     "coquette5",
    };


    public static List<string> models_wastelander = new List<string>() {
    "wastelander",
    };

    public static List<string> models_weaponboats = new List<string>() {
    "tug",
    };



    public static List<string> models_pizza = new List<string>() {
    "pizzaboy",
    };


    public static List<string> models_higgins = new List<string>() {
    "maverick2",
    "conada",
    };





    public static List<string> models_arena_speed = new List<string>() {
    "sheava",
    "tyrus",
    "le7b",
    "tropos",
    "lm87",
    "specter2",
    "italigtb2",
    "s80",
};

    public static List<string> models_arena_hotring = new List<string>() {
    "tampa2",
    "gauntlet6",
    "hotring",
    "everon2",
    "flashgt",
    "gb200",
    "omnis",
};

    public static List<string> models_arena_offroad = new List<string>() { 
    "ratel",
    "monstrociti",
    "hellion",
    "trophytruck",
    "trophytruck2",
    "bf400",
    "cliffhanger",
    "raptor",
    "rrocket"
};
    public static List<string> models_openwheel = new List<string>() {
    "openwheel1",
    "openwheel2",
    "formula",
    "formula2",
    };

    public static List<string> models_armoured = new List<string>() {
        "baller5",
    "baller6",
    "xls2",
    "cognoscenti2",
        "cog552",
            "schafter5",
    "schafter6",
    "paragon2"
    };
    public static List<string> models_classics = new List<string>() {
       
         "cheetah2",
         "feltzer3",
             "infernus2",
            "gt500",
            "jb7002",
            "mamba",
            "monroe",
            "swinger",
            "torero",
            "turismo2",
    };
    public static List<string> models_old_school = new List<string>() {
       
    
         "issi3",
          "dynasty",
        // "michelli",
        "hustler",
           "weevil",
              "hermes",


    };


}