using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattlefieldBootstrap
    {
        void BuildAqueduct()
        {
            float x = ArenaSize.x * .22f, z = ArenaSize.y * .36f;
            foreach (int side in new[] { -1, 1 })
            {
                for (int row = -3; row <= 3; row++)
                {
                    Vector2 p = new Vector2(side * x, row * z / 3);
                    Pillar("Aqueduct pier", p, 2.6f, 13, sandstone);
                    // A continuous elevated channel leaves every ground crossing open.
                    if (row < 3)
                        Shape("Aqueduct channel", PrimitiveType.Cube, arenaRoot,
                            new Vector3(p.x, 13.5f, p.y + z / 6), new Vector3(6.2f, 2, z / 3 + 1), sandstone, true);
                }
                foreach (int end in new[] { -1, 1 })
                    Room("Pump house", new Vector2(side * ArenaSize.x * .37f, end * z * .65f), new Vector2(17, 23), 8);
                for (int row = -2; row <= 2; row++)
                    Wall("Broken channel footing", new Vector2(side * x * .47f, row * z / 2.5f), new Vector2(8, 2), 7, sandstone);
            }
            Floor("Aqueduct processional road", Vector2.zero, new Vector2(10, ArenaSize.y - 6), sandstone);
            routeCheckpoints.Add(Vector3.zero);
            foreach (int side in new[] { -1, 1 })
                routeCheckpoints.Add(new Vector3(side * x, 0, z / 6));
        }

        void BuildQuarry()
        {
            float dx = ArenaSize.x * .20f, dz = ArenaSize.y * .15f;
            for (int row = -2; row <= 2; row++)
            {
                for (int column = -2; column <= 2; column++)
                {
                    Vector2 p = new Vector2(column * dx + (row % 2 == 0 ? -4 : 4), row * dz);
                    Rock(p, new Vector2(12 + (column + 2) % 3 * 2, 11), 9 + (row + 2) % 3 * 2);
                }
                routeCheckpoints.Add(new Vector3(0, 0, row * dz + dz * .5f));
            }
            foreach (int side in new[] { -1, 1 })
            {
                Room("Stonecutters lodge", new Vector2(side * dx, side * ArenaSize.y * .39f), new Vector2(23, 18), 8);
                Floor("Quarry haul road", new Vector2(side * ArenaSize.x * .455f, 0), new Vector2(7, ArenaSize.y - 6), sandstone);
            }
        }

        void BuildMonastery()
        {
            float x = ArenaSize.x * .27f, z = ArenaSize.y * .27f;
            foreach (int side in new[] { -1, 1 })
            {
                foreach (int end in new[] { -1, 1 })
                    Room("Monastery cloister", new Vector2(side * x, end * z), new Vector2(28, 32), 9);
                DoorWall("Bell court arcade", new Vector2(0, side * z * .5f), x * 1.3f, true, 9, 10, -x * .38f, x * .38f);
                for (int i = -2; i <= 2; i++)
                {
                    Pillar("Garden colonnade", new Vector2(side * x * 1.45f, i * z * .34f), 1.4f, 9);
                    Pillar("Bell court column", new Vector2(side * x * .47f, i * z * .24f), 1.2f, 10);
                }
            }
            Pillar("Central bell tower", Vector2.zero, 3.4f, 17);
            Floor("Bell court mosaic", Vector2.zero, new Vector2(x, z), roofTile);
            routeCheckpoints.Add(new Vector3(0, 0, 12));
        }

        void BuildBastion()
        {
            float x = ArenaSize.x * .30f, z = ArenaSize.y * .25f;
            for (int row = -1; row <= 1; row++)
            {
                float centerDoor = row == 0 ? 12 : 0;
                DoorWall("Bastion rampart", new Vector2(0, row * z), ArenaSize.x - 18, true, 10, 10, -x, centerDoor, x);
                if (row != 0) Gate(new Vector2(0, row * z), 9, 8, false, row < 0 ? 0 : 9);
                foreach (int side in new[] { -1, 1 })
                {
                    Pillar("Rampart watchtower", new Vector2(side * (x + 9), row * z + 8), 2.8f, 14, factoryIron);
                    Wall("Courtyard shield wall", new Vector2(side * x * .52f, row * z + 14), new Vector2(12, 2.5f), 8, factoryIron);
                }
                routeCheckpoints.Add(new Vector3(centerDoor, 0, row * z + 6));
            }
            foreach (int side in new[] { -1, 1 })
                foreach (int end in new[] { -1, 1 })
                    Room("Bastion armoury", new Vector2(side * x, end * z * 1.55f), new Vector2(22, 19), 9);
            Floor("Bastion military road", Vector2.zero, new Vector2(9, ArenaSize.y - 6), roofTile);
        }
    }
}
