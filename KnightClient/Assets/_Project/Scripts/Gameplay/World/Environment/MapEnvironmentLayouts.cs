using System;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.World.Environment
{
    /// <summary>
    /// Hand-written dressing for each map id. Play areas and the spots in
    /// <see cref="MapEnvironmentLayout.KeepClear"/> mirror serverSettings.json
    /// (map bounds, spawn points, NPCs and portals); keep them in sync when the
    /// server data changes.
    /// </summary>
    public static class MapEnvironmentLayouts
    {
        private static readonly string[] Oaks =
            { "tree_oak_green", "tree_oak_green", "tree_oak_yellow", "pine_0", "pine_2" };
        private static readonly string[] Pines =
            { "pine_0", "pine_1", "pine_2", "pine_3", "pine_small_0", "pine_small_1", "pine_small_2" };
        private static readonly string[] GreenBushes =
            { "bush_large_green", "bush_green", "bush_small_green", "bush_large_olive", "bush_olive" };
        private static readonly string[] AutumnBushes =
            { "bush_large_olive", "bush_olive", "bush_large_amber", "bush_amber", "bush_red" };
        private static readonly string[] Flowers =
        {
            "flower_orange", "flower_white", "flower_blue", "flower_yellow",
            "flower_orange_s", "flower_white_s", "flower_blue_s", "flower_yellow_s",
        };

        private static readonly string[] DeadTrees = { "tree_dead", "pine_small_0", "pine_small_2" };
        private static readonly string[] AutumnTrees =
            { "tree_oak_amber", "tree_oak_orange", "tree_oak_yellow", "pine_1" };
        private static readonly string[] GrayRocks =
            { "rock_gray", "rock_flat_gray", "rock_big_gray", "stump" };

        public static MapEnvironmentLayout Get(string mapId)
        {
            switch ((mapId ?? string.Empty).ToLowerInvariant())
            {
                case "wolf_field_01": return WolfField();
                case "safe_zone_01": return SafeZone();
                // Hunting chain east of Safe Zone 1 (serverSettings.json): each map
                // has a west portal (-9, 0) back and an east portal (9, 0) onward.
                case "slime_meadow_01":
                    return Field(new Color(0.14f, 0.26f, 0.11f), GroundKind.Dirt, Oaks, GreenBushes,
                        Flowers, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -6.4f, 7.2f, 0.8f, 11.2f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 40,
                                0.5f, Flowers));
                        });
                case "spider_woods_01":
                    return Field(new Color(0.05f, 0.11f, 0.07f), GroundKind.Dirt, Pines,
                        AutumnBushes, new[] { "mushroom_big", "mushroom_red", "leaves", "pebbles" },
                        layout => layout.Scatters.Add(new ScatterRule(ScatterZone.Inside,
                            PropLayer.Standing, 6, 3f, "dead_bush", "dead_bush_small", "stump_moss")));
                case "dragonfly_marsh_01":
                    return Field(new Color(0.09f, 0.20f, 0.16f), GroundKind.Dirt, Pines, GreenBushes,
                        new[] { "grass_clump", "fern", "flower_blue_s", "flower_white_s" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -17.6f, 6.4f, 17.6f, 12f));
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -17.6f, -12f, 17.6f, -8f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 10,
                                1.8f, "cattail", "foxglove_1", "fern"));
                        });
                case "serpent_lake_01":
                    return Field(new Color(0.10f, 0.20f, 0.24f), GroundKind.Cobble, Oaks, GreenBushes,
                        new[] { "pebbles", "grass_clump", "flower_blue", "flower_white" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -20f, 6.4f, 20f, 12f));
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -20f, -12f, 20f, -6.4f));
                            layout.Props.Add(new PropPlacement("cattail", -5.6f, 5.6f));
                            layout.Props.Add(new PropPlacement("cattail", 4.4f, 5.7f, flip: true));
                        });
                case "turtle_shore_01":
                    return Field(new Color(0.16f, 0.24f, 0.16f), GroundKind.Dirt, AutumnTrees,
                        AutumnBushes, new[] { "pebbles", "rock_small_brown", "leaves", "flower_yellow_s" },
                        layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -20f, -12f, 20f, -6.4f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 6,
                                3f, "rock_brown", "rock_flat_brown", "bush_amber"));
                        });
                case "goblin_camp_01":
                    return Field(new Color(0.13f, 0.17f, 0.09f), GroundKind.Dirt, AutumnTrees,
                        AutumnBushes, new[] { "pebbles", "leaves", "rock_small_brown", "grass_clump" },
                        layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -4.8f, -2.4f, 4.8f, 4.0f));
                            layout.Bonfires.Add(new Vector2(-1.6f, 2.0f));
                            layout.Bonfires.Add(new Vector2(3.2f, -1.6f));
                            layout.Props.Add(new PropPlacement("stump", -3.6f, 3.2f));
                            layout.Props.Add(new PropPlacement("rock_brown", 4.0f, 3.2f));
                        });
                case "ghoul_swamp_01":
                    return Field(new Color(0.06f, 0.10f, 0.08f), GroundKind.Dirt, DeadTrees, AutumnBushes,
                        new[] { "mushroom_blue", "mushroom_big", "leaves", "grass_clump" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Water, -17.6f, 6.4f, 17.6f, 12f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 6,
                                3f, "dead_bush", "dead_bush_small", "cattail"));
                        });
                case "eye_ruins_01":
                    return Field(new Color(0.12f, 0.13f, 0.14f), GroundKind.Cobble, DeadTrees, AutumnBushes,
                        new[] { "pebbles", "rock_small_gray", "rock_flat_gray", "leaves" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Brick, -4.0f, 2.4f, -0.8f, 5.2f));
                            layout.Patches.Add(new GroundPatch(GroundKind.Brick, 2.4f, -4.8f, 5.6f, -2.4f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 6,
                                3f, GrayRocks));
                        });
                case "bone_graveyard_01":
                    return Field(new Color(0.09f, 0.09f, 0.11f), GroundKind.Dirt, DeadTrees, AutumnBushes,
                        new[] { "pebbles", "rock_small_gray", "leaves", "mushroom_big" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -6.4f, 2.4f, -2.4f, 4.8f));
                            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, 2.4f, -4.8f, 6.4f, -2.4f));
                            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 8,
                                2.5f, "tree_dead", "dead_bush_small", "rock_gray", "stump"));
                        });
                case "mage_tower_01":
                    return Field(new Color(0.08f, 0.07f, 0.14f), GroundKind.Brick, Pines, AutumnBushes,
                        new[] { "flower_blue_s", "flower_white_s", "pebbles", "mushroom_blue" }, layout =>
                        {
                            layout.Patches.Add(new GroundPatch(GroundKind.Brick, -3.2f, -2.4f, 3.2f, 3.2f));
                            layout.Bonfires.Add(new Vector2(0f, 1.6f));
                            layout.Props.Add(new PropPlacement("rock_big_gray", -2.6f, 2.8f));
                            layout.Props.Add(new PropPlacement("rock_big_gray", 2.6f, 2.8f, flip: true));
                        });
                default:
                    return Village();
            }
        }

        /// <summary>
        /// Common hunting-ground dressing: a path between the two portals,
        /// forest around the play area and themed decals inside. Portal and
        /// entry spots stay clear (KeepClear).
        /// </summary>
        private static MapEnvironmentLayout Field(Color backdrop, GroundKind path, string[] trees,
            string[] bushes, string[] decals, Action<MapEnvironmentLayout> theme)
        {
            var layout = new MapEnvironmentLayout(DefaultPlayArea(), backdrop);
            layout.KeepClear.AddRange(new[]
            {
                new Vector2(-9f, 0f), new Vector2(-7f, 0f), new Vector2(7f, 0f), new Vector2(9f, 0f),
            });
            layout.Patches.Add(new GroundPatch(path, -20f, -0.8f, 20f, 0.8f));
            theme?.Invoke(layout);
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 90, 2.2f, trees));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 25, 1.4f, bushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 40, 1.3f, bushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 8, 2f,
                "rock_gray", "rock_brown", "stump"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 35, 0.6f, decals));
            return layout;
        }

        private static Rect DefaultPlayArea() => Rect.MinMaxRect(-10f, -6f, 10f, 6f);

        /// <summary>tutorial_map_01: road through the village, farm plot, campfire, ponds.</summary>
        private static MapEnvironmentLayout Village()
        {
            var layout = new MapEnvironmentLayout(DefaultPlayArea(), new Color(0.13f, 0.24f, 0.10f));
            layout.KeepClear.AddRange(new[]
            {
                new Vector2(0f, -3f), new Vector2(4f, 0f), new Vector2(3f, 0f),
                new Vector2(9f, 0f), new Vector2(-9f, 0f),
                // NPCs (serverSettings.json npcDefinitions).
                new Vector2(-1.2f, 3.8f), new Vector2(-6.6f, -1.8f), new Vector2(7.4f, 1.8f),
            });
            layout.Patches.Add(new GroundPatch(GroundKind.Cobble, -20f, -0.8f, 20f, 0.8f));
            layout.Patches.Add(new GroundPatch(GroundKind.Cobble, -2.4f, -4.0f, 2.4f, -0.8f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -8.8f, -5.2f, -4.8f, -2.4f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -4.0f, 2.4f, -1.6f, 4.0f));
            layout.Patches.Add(new GroundPatch(GroundKind.Water, 11.2f, 4.0f, 17.6f, 9.6f));
            layout.Patches.Add(new GroundPatch(GroundKind.Water, -17.6f, -11.2f, -12.0f, -7.2f));

            string[] crops = { "crop_cabbage", "crop_lettuce", "crop_carrot" };
            for (int row = 0; row < crops.Length; row++)
            for (int column = 0; column < 5; column++)
            {
                layout.Props.Add(new PropPlacement(crops[row], -8.4f + column * 0.8f,
                    -3.4f - row * 0.8f, PropLayer.Decal, column % 2 == 1));
            }
            layout.Props.Add(new PropPlacement("scarecrow", -4.2f, -3.0f));
            layout.Props.Add(new PropPlacement("tree_oak_green", 8.0f, 3.6f));
            layout.Props.Add(new PropPlacement("tree_oak_yellow", -8.4f, 4.0f, flip: true));
            layout.Props.Add(new PropPlacement("bush_large_green", 6.6f, 4.6f));
            layout.Props.Add(new PropPlacement("stump_moss", -5.4f, 4.8f));
            layout.Props.Add(new PropPlacement("cattail", 11.0f, 4.4f));
            layout.Props.Add(new PropPlacement("cattail", 11.3f, 5.2f, flip: true));
            layout.Bonfires.Add(new Vector2(-2.8f, 3.2f));

            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 90, 2.3f, Oaks));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 30, 1.4f, GreenBushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 45, 1.3f, GreenBushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 8, 2f,
                "rock_brown", "rock_flat_brown", "stump"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 7, 3f,
                "bush_small_green", "bush_green", "foxglove_0", "foxglove_1"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 70, 0.5f, Flowers));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 10, 1.5f,
                "grass_clump", "fern", "rock_small_brown", "mushroom_red", "foxglove_2"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Decal, 40, 0.6f, Flowers));
            return layout;
        }

        /// <summary>wolf_field_01: wild meadow with dirt clearings and a dark pine forest.</summary>
        private static MapEnvironmentLayout WolfField()
        {
            var layout = new MapEnvironmentLayout(DefaultPlayArea(), new Color(0.08f, 0.15f, 0.09f));
            layout.KeepClear.AddRange(new[]
            {
                new Vector2(-7f, 0f), new Vector2(-6.5f, 0f), new Vector2(-9f, 0f),
            });
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -20f, -0.8f, -4.0f, 0.8f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -4.8f, -1.6f, -1.6f, 1.6f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, 1.6f, -4.8f, 4.8f, -2.4f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, 4.0f, 1.6f, 8.0f, 3.2f));
            layout.Patches.Add(new GroundPatch(GroundKind.Dirt, -6.4f, 3.2f, -3.2f, 4.8f));
            layout.Patches.Add(new GroundPatch(GroundKind.Water, 12.0f, -4.8f, 17.6f, 1.6f));

            layout.Props.Add(new PropPlacement("tree_dead", 8.6f, 4.4f));
            layout.Props.Add(new PropPlacement("stump", -1.0f, 4.6f));
            layout.Props.Add(new PropPlacement("stump_moss", 8.4f, -2.0f));
            layout.Props.Add(new PropPlacement("rock_big_gray", -8.6f, 4.6f));
            layout.Props.Add(new PropPlacement("rock_gray", 0.6f, -5.4f));

            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 100, 2.1f, Pines));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 12, 3f,
                "tree_dead", "tree_oak_amber", "tree_oak_orange", "rock_big_gray", "rock_big_brown"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 40, 1.3f, AutumnBushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 10, 2f,
                "rock_gray", "rock_brown", "dead_bush_small", "stump"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 6, 3.5f,
                "dead_bush_small", "dead_bush", "bush_olive"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 30, 0.9f,
                "rock_small_gray", "rock_small_brown", "rock_flat_gray", "pebbles", "leaves",
                "grass_clump", "mushroom_big", "mushroom_blue"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 20, 0.7f,
                "flower_yellow_s", "flower_white_s", "flower_orange_s"));
            return layout;
        }

        /// <summary>safe_zone_01: paved town square with a campfire, road to the village.</summary>
        private static MapEnvironmentLayout SafeZone()
        {
            var layout = new MapEnvironmentLayout(DefaultPlayArea(), new Color(0.12f, 0.22f, 0.12f));
            layout.KeepClear.AddRange(new[]
            {
                new Vector2(0f, -2.5f), new Vector2(0f, -5f), new Vector2(7f, 0f), new Vector2(9f, 0f),
                // NPCs (serverSettings.json npcDefinitions).
                new Vector2(-4.2f, 3.0f), new Vector2(4.2f, 3.0f), new Vector2(-4.2f, -0.8f),
                new Vector2(4.2f, -0.8f), new Vector2(6.8f, 2.2f),
            });
            layout.Patches.Add(new GroundPatch(GroundKind.Brick, -6.4f, -2.4f, 6.4f, 4.0f));
            layout.Patches.Add(new GroundPatch(GroundKind.Cobble, -1.6f, -12f, 1.6f, -2.4f));
            layout.Patches.Add(new GroundPatch(GroundKind.Water, -17.6f, -4.0f, -12.0f, 3.2f));
            layout.Patches.Add(new GroundPatch(GroundKind.Water, 12.0f, -4.0f, 17.6f, 3.2f));

            layout.Bonfires.Add(new Vector2(0f, 1.2f));
            layout.Props.Add(new PropPlacement("tree_oak_green", -8.6f, 4.4f));
            layout.Props.Add(new PropPlacement("tree_oak_green", 8.6f, 4.4f, flip: true));
            layout.Props.Add(new PropPlacement("bush_large_green", -7.6f, -1.6f));
            layout.Props.Add(new PropPlacement("bush_large_green", 7.6f, -1.6f, flip: true));
            layout.Props.Add(new PropPlacement("bush_green", -3.0f, -4.6f));
            layout.Props.Add(new PropPlacement("bush_green", 3.0f, -4.6f, flip: true));

            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 85, 2.3f, Oaks));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideBehind, PropLayer.Standing, 25, 1.4f, GreenBushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Standing, 45, 1.3f, GreenBushes));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Standing, 6, 2.5f,
                "foxglove_0", "foxglove_1", "bush_small_green"));
            layout.Scatters.Add(new ScatterRule(ScatterZone.Inside, PropLayer.Decal, 45, 0.5f, Flowers));
            layout.Scatters.Add(new ScatterRule(ScatterZone.OutsideFront, PropLayer.Decal, 30, 0.6f, Flowers));
            return layout;
        }
    }
}
