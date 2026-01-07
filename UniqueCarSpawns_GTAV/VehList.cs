
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
    public static HashSet<string> models_cult = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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


    public static HashSet<string> models_cemetery = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tornado6",
    "btype2",
    "sanctus",
    "lurcher",
    "brigham",
    };

    public static HashSet<string> models_cheburek = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "cheburek",
    "ratbike",
   "clique2",
   "peyote2",
   "weevil2"
    };

    public static HashSet<string> models_studio = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "scramjet", //speedracer
    "vigilante", //batmobile
    "voltic2", //rocket voltic
    "toreador",//submarine car #2
    "jb7002",//bond car
    "deluxo",//delorean
    "stromberg",//submarine car
    "rrocket",//rampant rocket
    "shotaro",//tron
    "dune5",//rampbuggy
    "ruiner2", //nightrider
    "oppressor" //streethawk
    };

    public static HashSet<string> models_cluckin = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "benson2"
    };

    public static HashSet<string> models_compacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "brioso",
    "brioso2",
    "brioso3",
    "weevil",
    "club",
    "kanjo",
    "asbo",
    "issi3"
    };

    public static HashSet<string> models_coupes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "kanjosj",
    "postlude",
    "previon",
    "windsor2",
    "windsor",
    "fr36",
    "eurosX32",
    };



    public static HashSet<string> models_lowriders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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
    public static HashSet<string> models_poor = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "greenwood",
        "retinue2",
    "dynasty",
    "cheburek",
    "fagaloa",
    "greenwood",
    };

    public static HashSet<string> models_helicopter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "conada",
    "swift2",
    "Havok",
    "Volatus",
    "SeaSparrow",
    "Supervolito",
    "Supervolito2",
    "Swift2"
    };

    public static HashSet<string> models_humanlabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "brickade2",
    };


    public static HashSet<string> models_karting = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "veto",
    "veto2",
         "locust",
          "ruston",
          "raptor",
          "stryder",
          "dubsta3",
          "everon",
          "brawler",
          "kamacho",
          "monstrociti",
          "vagrant",
          "outlaw"
    };

    /*public static HashSet<string> models_beach_cars = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "locust",
          "ruston",
          "raptor",
          "stryder",
          "dubsta3",
          "everon",
          "brawler",
          "kamacho",
          "monstrociti",
          "vagrant",
          "outlaw"

    };*/



    public static string apc_model = "apc";
    public static string raiju_model = "raiju";
    public static string conada2_model = "conada2";



    public static HashSet<string> models_kuruma = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "kuruma2"
    };

    public static HashSet<string> models_insurgents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "insurgent2",
    "insurgent3",
    "nightshark",
    };


    public static HashSet<string> models_thruster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "thruster"
    };
    public static HashSet<string> models_oppressor2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
     "oppressor2"
    };

    public static HashSet<string> models_military_planes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "strikeforce",
    "nokota",
    "pyro",
    "mogul",
       "raiju",
    "molotok",
    "tula",
    "starling",
    "hydra",
    };

    public static HashSet<string> models_military_helicopters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "annihilator2",
    "hunter",
    "valkyrie",
    "savage",
     "conada2",
    };

    public static HashSet<string> models_military_opressors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "oppressor",
    "oppressor2",
    };

    public static HashSet<string> models_military_bikes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "squaddie",
    "manchez2",
    "winky",
    "insurgent3",
    };

    public static HashSet<string> models_motorcycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "manchez3",
    "shinobi",
    "manchez2",
    //"stryder",
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



    public static HashSet<string> models_beach = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "pbus2",
    };

    public static HashSet<string> models_planes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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

    public static HashSet<string> models_submarine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "avisa",
    };


    public static HashSet<string> models_supers_common = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these cars do not spawn in natural-popgroups traffic and dont have a dedicated rockstar spawn
        "tempesta",
        "reaper",
        "t20",
        "osiris",
        "gp1",
        "xa21",
        "fmj",  
        "prototipo",
        "nero",
        "nero2",
        "banshee3",
             "sm722",
       "visione",
       "deveste",
    };

    
    public static HashSet<string> models_doomsday = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { //these cars already spawn in traffic or have an in game location
       "tampa3",
    };



    public static HashSet<string> models_valentine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "btype3",
    "broadway",
    "stafford",
     "coquette5",
    };


    public static HashSet<string> models_wastelander = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "wastelander",
    };

    public static HashSet<string> models_weaponboats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tug",
    };



    public static HashSet<string> models_pizza = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "pizzaboy",
    };


    public static HashSet<string> models_higgins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "maverick2",
    "conada",
    };





    public static HashSet<string> models_arena_speed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "sheava",
    "tyrus",
    "le7b",
    "tropos",
    "lm87",
    "specter2",
    "italigtb2",
    "s80",
};

    public static HashSet<string> models_arena_hotring = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "tampa2",
    "gauntlet6",
    "hotring",
    "everon2",
    "flashgt",
    "gb200",
    "omnis",
};

    public static HashSet<string> models_arena_offroad = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
    "ratel",
    "monstrociti",
    "hellion",
    "trophytruck",
    "trophytruck2",
    "bf400",
    "cliffhanger",
    "raptor",
};
    public static HashSet<string> models_openwheel = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "openwheel1",
        "openwheel2",
        "formula",
        "formula2",
    };

    public static HashSet<string> models_armoured = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "baller5",
        "baller6",
        "xls2",
        "cognoscenti2",
        "cog552",
        "schafter5",
        "schafter6",
        "paragon2"
    };
   
    public static HashSet<string> models_classics_common = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {

         "cheetah2",
         "feltzer3",
         "infernus2",
         "gt500",
         "rapidgt3",
         "swinger",
         "torero",
         "turismo2",
         "tropos",

    };

    /*
   public static HashSet<string> models_classics_rare = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
       "monroe",
       "stingergt",
       "mamba",
       "commet3",
       "casco",
      "jb7002",
   }; */


    public static HashSet<string> models_wacky = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "dukes3",
        "cheburek",
        "comet4",
        "hotknife",
        "weevil2",
        "patriot3",
        "peyote2",
        "ratbike",
        "ratloader2",
        "ratloader",
        "tornado6",
        "winky"
    };



    public static HashSet<string> models_choppers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
 //   "shinobi",
  //  "manchez2",
  //  "stryder", 3 wheeler trike
 //   "fcr2",
 //   "fcr",
  //  "diablous", sports
   // "diablous2", sports
  //  "esskey", //sports and dirt bike hybrid
   // "vortex", //sports bike
    "daemon2",
    "zombieb",
    "nightblade",
   // "manchez", //dirt bike
  //  "hakuchou2", //sports bike
   // "faggio", //faggio sport
    "faggio3", //faggio mod
   // "defiler", //spots bike
    "chimera",
    "avarus",
    "cliffhanger",
    "gargoyle",
  //  "bf400", //dirt bike
 //   "vindicator",  //sports bike
 //   "lectro",  //sports bike
  //  "enduro", //dirt bike
    };
    public static HashSet<string> models_bikes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {

    "stryder",
    "fcr2",
    "fcr",
    "diablous2", 
    "esskey", //sports and dirt bike hybrid
    "vortex", //sports bike
    "manchez", //dirt bike



    "bf400", //dirt bike
   "vindicator",  //sports bike
    "lectro",  //sports bike
    "enduro", //dirt bike
    };

    public static HashSet<string> models_general_rare = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these spawn everywhere desert and city

        "asterope2",


        "chavosv6",
        "z190",


        "fr36",
        "futo2", //futo gtx
        "hellion",

        "kanjosj",
        "khamelion",
        "moonbeam",
        "nebula",

        "retinue",

        "seminole2",

        "sultan2",

        "vstr",

        "zr350",
        "youga2", //youga classic
   
        "hakuchou2", //sports bike
            "wolfsbane",
                "zombiea",
         "hermes",


    };
    public static HashSet<string> models_general_common = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these spawn everywhere desert and city
         "asbo",
                 "kanjo",
                         "brioso",
        "calico",
        "club",
        "dominator7", //dominator gtx
        "dominator8", //dominator gtt
        "dominator3", //dominator asp
                "eudora",
        "faction",
                "impaler",
        "jester3", //jester classic
              "kuruma",
                "nightshade",
        "previon",
        "remus",
                "rt3000",
                "sentinel3", //sentinel classic
        "slamvan",
                "tulip",
        "virgo",
                "zion3", //zion classic
             "penumbra2", //penumbraff
                            "issi3",
        "dynasty",
        "hustler",
                "yosemite",
    };


    public static HashSet<string> models_city = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these will spawn in most urban areas, more prevalent in rich zones (city only)
        "banshee2",
        "bestiagts",
     //   "brioso",
        "cinquemila",
        "cog55",
        "cognoscenti",
        "comet5",
        "coquette6",
        "cypher",
        "drafter",
                "euros",
     //   "fr36",
        "growler",
        "italigtb",
        "italigto",
        "jugular",
        "komoda",
        "lynx",
        "neon",
        "novak",
        "panthere",
        "paragon",
        "pariah",
        "raiden",
        "rebla",
        "revolter",
        "schlagen",
        "seven70",
        "specter",
        "s95",
        "vectre",
        "verlierer2",
      //  "vstr",
        "windsor",
        "windsor2",
        "xls",
        "diablous",
         "shinobi",
            "faggio", //faggio sport
                "defiler", //spots bike
                        "coquette3",
                        //new stuff from classics and super rare
                               "monroe",
       "stingergt",
       "mamba",
       "commet3",
       "casco",
      "jb7002",
             "zentorno",
       "turismor",
       "cheetah",
       "entityxf",
       "vacca",
        "adder",
       "sc1",
       "penetrator",
       "pfister811",
       "cyclone",
    };

    public static HashSet<string> models_rural = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {//these will spawn in rural areas, more prevalent in rich zones (desert only)
        "boor",
        "brawler",
        "cheburek",
        "clique2",
        "dominator10", //fx
        "dorado",
        "esskey",
        "firebolt",
        "gauntlet5", //gauntlet classic custom
        "journey2",
        "kamacho",
        "monstrociti",
        "retinue2",
        "riata",
        "rumpo3",
        "savestra",
        "seminole2",
        "vamos",
        "yosemite3",
        "youga3",
        "enduro",
        "gargoyle",
        "manchez2",
        "sovereign",
        "bf400",

    };



}