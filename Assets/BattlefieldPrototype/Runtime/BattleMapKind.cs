namespace BattlefieldPrototype
{
    // Append values: saved scenes keep their original layouts.
    public enum BattleMapKind { RuinedVillage, RiverCrossing, Woodland, IronCitadel, BlackstoneMaze,
        Ironworks, SplitstoneCanyon, SunkenHarbor, SunlessCatacombs, ShatteredBazaar, ThornSanctum,
        EmberAqueduct, CrownQuarry, StormMonastery, ObsidianBastion }
    public static class BattleMapNames
    {
        public const int Count = 15;
        public static bool IsEnclosed(BattleMapKind kind) => (int)kind >= (int)BattleMapKind.IronCitadel;
        public static string Name(BattleMapKind kind)
        {
            switch(kind)
            {
                case BattleMapKind.RiverCrossing: return "THREE CROSSINGS";
                case BattleMapKind.Woodland: return "THE OLD WOOD";
                case BattleMapKind.IronCitadel: return "IRON CITADEL";
                case BattleMapKind.BlackstoneMaze: return "BLACKSTONE LABYRINTH";
                case BattleMapKind.Ironworks: return "THE IRONWORKS";
                case BattleMapKind.SplitstoneCanyon: return "SPLITSTONE CANYON";
                case BattleMapKind.SunkenHarbor: return "SUNKEN HARBOR";
                case BattleMapKind.SunlessCatacombs: return "SUNLESS CATACOMBS";
                case BattleMapKind.ShatteredBazaar: return "SHATTERED BAZAAR";
                case BattleMapKind.ThornSanctum: return "THORN SANCTUM";
                case BattleMapKind.EmberAqueduct: return "EMBER AQUEDUCT";
                case BattleMapKind.CrownQuarry: return "CROWN QUARRY";
                case BattleMapKind.StormMonastery: return "STORM MONASTERY";
                case BattleMapKind.ObsidianBastion: return "OBSIDIAN BASTION";
                default: return "ASHFALL VILLAGE";
            }
        }
        public static string Description(BattleMapKind kind)
        {
            switch(kind)
            {
                case BattleMapKind.IronCitadel: return "Double curtain walls, gatehouses and roofed barracks. Fight for the courtyards.";
                case BattleMapKind.BlackstoneMaze: return "A connected labyrinth with loops, four entrances and towering blind corners.";
                case BattleMapKind.Ironworks: return "Furnaces, overhead pipes and cycling blast doors. Watch the amber gate signals.";
                case BattleMapKind.SplitstoneCanyon: return "High rock faces split the valley into winding passes and ambush pockets.";
                case BattleMapKind.SunkenHarbor: return "Warehouse interiors, cargo canyons and moving dock gates. Multiple flanking lanes.";
                case BattleMapKind.SunlessCatacombs: return "Roofed crypts, pillars and interconnected galleries. No view over the walls.";
                case BattleMapKind.ShatteredBazaar: return "Dense city blocks, archways, covered stalls and narrow rear alleys.";
                case BattleMapKind.ThornSanctum: return "Broken temple rings, towering hedges and sacred groves conceal the next approach.";
                case BattleMapKind.EmberAqueduct: return "Towering aqueduct piers, sheltered underpasses and pump houses divide three broad assault lanes.";
                case BattleMapKind.CrownQuarry: return "Staggered rock shelves and stonecutters' lodges create winding lanes and wide flanking routes.";
                case BattleMapKind.StormMonastery: return "Four roofed cloisters surround a bell court. Open arcades connect sheltered rooms and side gardens.";
                case BattleMapKind.ObsidianBastion: return "Three fortified lines, cycling portcullises and side gates reward breakthroughs and flanking attacks.";
                case BattleMapKind.RiverCrossing: return "OPEN LAYOUT / Three broad crossings and low banks favour ranged armies.";
                case BattleMapKind.Woodland: return "OPEN LAYOUT / Scattered trees and ruins leave long firing lanes.";
                default: return "OPEN LAYOUT / Low village ruins and broad approaches leave room to manoeuvre.";
            }
        }
    }
}
