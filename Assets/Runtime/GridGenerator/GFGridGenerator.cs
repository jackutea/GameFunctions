using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using RD = System.Random;
using CTX = GameFunctions.GFGridGeneratorContext;

// Rewrite 算法:
// 侵蚀算法(Erode):     01      = 11
// 描边算法(Outline):   011     = 211
// 洪泛算法(Flood):     01      = 11
// 播种算法(Scatter):   0100    = 0101
// 平滑算法(Smooth):    101     = 111
// 河流算法(River):     01000   = 01111
// 点缀算法(Decorate):  01000   = 01001
namespace GameFunctions {

    public static class GFGridGenerator {

        // ==== Land Init ====
        public static CTX GenAll(GFGenGridOption gridOption, GFGenSeaOption seaOption, GFGenLakeOption lakeOption) {

            CTX ctx = new CTX();
            ctx.Init(gridOption, seaOption, lakeOption);

            // Gen: Land(default)
            for (int i = 0; i < ctx.grid.Length; i++) {
                ctx.Land_Add(i, gridOption.defaultLandValue);
            }

            // Gen: Sea
            bool succ_sea = Gen_Sea(ctx);
            if (!succ_sea) {
                Debug.LogError("Gen_Sea failed");
            }

            // Cache Update: Land
            // remove sea cells from land cells
            for (int i = 0; i < ctx.grid_sea_set.Count; i += 1) {
                int seaIndex = ctx.grid_sea_indices[i];
                ctx.Land_Remove(seaIndex);
            }
            ctx.Land_UpdateAll();

            // Gen: Land - Lake
            bool succ_lake = Gen_Land_Lake(ctx);
            if (!succ_lake) {
                Debug.LogError("Gen_Land_Lake failed");
            }

            // Cache Update: Land
            // remove lake cells from land cells
            for (int i = 0; i < ctx.grid_lake_set.Count; i += 1) {
                int lakeIndex = ctx.grid_lake_indices[i];
                ctx.Land_Remove(lakeIndex);
            }
            ctx.Land_UpdateAll();

            return ctx;

        }

        // ==== Sea ====
        // cells[index] = seaValue
        public static bool Gen_Sea(CTX ctx) {
            var option = ctx.seaOption;
            option.DIR = Math.Abs(option.DIR) % GFGenAlgorithm.DIR_COUNT;
            if (option.TYPE == GFGenSeaOption.TYPE_NORMAL) {
                return Gen_Sea_Normal(ctx);
            } else if (option.TYPE == GFGenSeaOption.TYPE_SHARP) {
                return Gen_Sea_Normal(ctx);
            } else {
                Debug.LogWarning("Unknown type: " + option.TYPE);
                return Gen_Sea_Normal(ctx);
            }
        }

        static bool Gen_Sea_Normal(CTX ctx) {

            int[] cells = ctx.grid;
            int width = ctx.gridOption.width;
            int height = ctx.gridOption.height;

            GFGenSeaOption option = ctx.seaOption;

            int seaCount = option.seaCount;
            if (seaCount >= cells.Length) {
                Debug.LogError("seaCount >= cells.Length");
                return false;
            }

            // Gen: start sea cell
            int seaValue = option.seaValue;
            GFGenAlgorithm.Pos_GetRandomPointOnEdge(ctx.random, width, height, option.DIR, out int start_x, out int start_y);
            int startSeaIndex = GFGenAlgorithm.Index_GetByPos(start_x, start_y, width);
            ctx.Sea_Add(startSeaIndex, seaValue);
            --seaCount;

            Action<int> onHandle = (int index) => {
                ctx.Sea_Add(index, seaValue);
            };

            // Erode: sea
            return GFGenAlgorithm.Alg_Erode(cells,
                                        ctx.grid_sea_indices,
                                        ctx.grid_sea_set,
                                        ctx.random,
                                        width,
                                        height,
                                        seaCount,
                                        option.erodeRate,
                                        seaValue,
                                        option.DIR,
                                        onHandle);

        }

        // ==== Land-Lake ====
        // cells[index] = landValue
        static bool Gen_Land_Lake(CTX ctx) {
            if (ctx.lakeOption.TYPE == GFGenLakeOption.TYPE_FLOOD) {
                return Gen_Land_Lake_Normal(ctx);
            } else {
                Debug.LogWarning("Unknown type: " + ctx.lakeOption.TYPE);
                return Gen_Land_Lake_Normal(ctx);
            }
        }

        static bool Gen_Land_Lake_Normal(CTX ctx) {

            int width = ctx.gridOption.width;
            int height = ctx.gridOption.height;
            GFGenLakeOption option = ctx.lakeOption;
            RD random = ctx.random;
            HashSet<int> waterValues = ctx.waterValues;

            int failedTimes = width * height * 10;

            int lakeCount = option.lakeCount;
            int lakeValue = option.lakeValue;
            int awayFromWater = option.awayFromWater;

            // Gen: start lake cell
            GFVector4Int edgeOffset = new GFVector4Int();
            int start_x = 0;
            int start_y = 0;
            int start_index = 0;
            do {

                failedTimes--;
                if (failedTimes <= 0) {
                    break;
                }

                // Select a random land cell
                int[] cells_land_indices = ctx.grid_land_indices;
                int cells_land_count = ctx.grid_land_set.Count;
                start_index = cells_land_indices[random.Next(ctx.grid_land_set.Count)];
                start_x = start_index % width;
                start_y = start_index / width;

                edgeOffset = GFGenAlgorithm.Pos_DetectAwayFrom(ctx.grid, width, height, start_x, start_y, waterValues, awayFromWater);

            } while (edgeOffset.Min() < option.awayFromWater);

            if (failedTimes <= 0) {
                Debug.LogError("Gen_Land_Lake_Normal failed");
                return false;
            }

            ctx.Lake_Add(start_index, option.lakeValue);
            --lakeCount;

            Action<int> onHandle = (int index) => {
                ctx.Lake_Add(index, option.lakeValue);
            };

            // Flood: lake
            return GFGenAlgorithm.Alg_Flood(ctx.grid,
                                        ctx.grid_lake_indices,
                                        ctx.grid_lake_set,
                                        random,
                                        width,
                                        height,
                                        lakeCount,
                                        lakeValue,
                                        onHandle);

        }

    }

}