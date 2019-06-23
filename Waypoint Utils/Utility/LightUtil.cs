﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace WaypointUtils
{
    class LightUtil : ModSystem
    {
        ICoreClientAPI capi;
        long id;
        WaypointUtilConfig config;
        ConfigLoader configLoader;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            configLoader = capi.ModLoader.GetModSystem<ConfigLoader>();
            config = configLoader.Config;
            capi.RegisterCommand("lightutil", "Light Util", "[lightlevel|type|radius|alpha|red]", new ClientChatCommandDelegate(CmdLightUtil));

            id = api.World.RegisterGameTickListener(dt =>
            {
                EntityPlayer player = api.World.Player.Entity;

                if (player != null)
                {
                    api.World.RegisterGameTickListener(d =>
                    {
                        if (config.LightLevels)
                        {
                            LightHighlight(null, config.LightLevelType);
                        }
                        else
                        {
                            ClearLightLevelHighlights();
                        }
                    }, 500);

                    api.World.UnregisterGameTickListener(id);
                }
            }, 500);
        }

        public void CmdLightUtil(int groupId, CmdArgs args)
        {
            string arg = args.PopWord();
            switch (arg)
            {
                case "lightlevel":
                    bool? cnd = args.PopBool();
                    if (cnd != null) config.LightLevels = (bool)cnd;
                    else { config.LightLevels = !config.LightLevels; }
                    break;
                case "type":
                    int? type = args.PopInt();
                    if (type != null)
                    {
                        EnumLightLevelType leveltype = (EnumLightLevelType)type;
                        config.LightLevelType = (EnumLightLevelType)type;
                    }
                    capi.ShowChatMessage("Light Util Type Set To " + Enum.GetName(typeof(EnumLightLevelType), config.LightLevelType));
                    break;
                case "radius":
                    int? rad = args.PopInt();
                    if (rad != null)
                    {
                        config.LightRadius = (int)rad;
                    }
                    capi.ShowChatMessage("Light Util Radius Set To " + config.LightRadius);
                    break;
                case "alpha":
                    float? alpha = args.PopFloat();
                    config.LightLevelAlpha = alpha != null ? (float)alpha : config.LightLevelAlpha;
                    capi.ShowChatMessage("Light Util Opacity Set To " + config.LightLevelAlpha);
                    break;
                case "red":
                    int? red = args.PopInt();
                    if (red != null)
                    {
                        config.LightLevelRed = (int)red;
                    }
                    capi.ShowChatMessage("Red Level Set To " + config.LightLevelRed);
                    break;
                case "above":
                    bool? ab = args.PopBool();
                    if (ab != null) config.LightLevels = (bool)ab;
                    else { config.LUShowAbove = !config.LUShowAbove; }
                    capi.ShowChatMessage("Show Above Set To " + config.LUShowAbove);
                    break;
                default:
                    capi.ShowChatMessage("Syntax: .lightutil [lightlevel|type|radius|alpha|red|above]");
                    break;
            }
            configLoader.SaveConfig();
        }

        List<BlockPos> highlightedBlocks = new List<BlockPos>();
        Dictionary<BlockPos, int> prepared = new Dictionary<BlockPos, int>();

        public void LightHighlight(BlockPos pos = null, EnumLightLevelType type = EnumLightLevelType.OnlyBlockLight)
        {
            if (highlightedBlocks.Count != 0) ClearLightLevelHighlights();

            pos = pos == null ? capi.World.Player.Entity.LocalPos.AsBlockPos.UpCopy() : pos;
            int rad = config.LightRadius;

            for (int x = -rad; x <= rad; x++)
            {
                for (int y = -rad; y <= rad; y++)
                {
                    for (int z = -rad; z <= rad; z++)
                    {
                        BlockPos iPos = pos.AddCopy(x, y, z);
                        Block block = capi.World.BlockAccessor.GetBlock(iPos);

                        bool rep = config.LUSpawning ? capi.World.BlockAccessor.GetBlock(iPos.UpCopy()).IsReplacableBy(block) : true;
                        bool opq = config.LUOpaque ? block.AllSidesOpaque : true;

                        if (block.BlockId != 0 && rep && opq && (x * x + y * y + z * z) <= (rad * rad))
                        {
                            highlightedBlocks.Add(iPos);
                        }
                    }
                }
            }
            BlockPos[] blocks = highlightedBlocks.ToArray();
            LightLevelHighlight(blocks, type);
        }

        public void LightLevelHighlight(BlockPos[] blocks, EnumLightLevelType type)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                BlockPos iPos = config.LUShowAbove ? blocks[i].UpCopy() : blocks[i];
                int level = capi.World.BlockAccessor.GetLightLevel(iPos, type);

                if (level != 0)
                {
                    float fLevel = level / 32.0f;
                    int alpha = (int)Math.Round(config.LightLevelAlpha * 255);
                    int c = level > config.LightLevelRed ? ColorUtil.ToRgba(alpha, 0, (int)(fLevel * 255), 0) : ColorUtil.ToRgba(alpha, 0, 0, (int)(fLevel * 255));

                    List<BlockPos> highlight = new List<BlockPos>() { blocks[i].AddCopy(0, 1, 0), blocks[i].AddCopy(1, 0, 1) };
                    List<int> color = new List<int>() { c };

                    capi.World.HighlightBlocks(capi.World.Player, config.MinLLID + i, highlight, color, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cubes);
                }
            }
        }

        public void ClearLightLevelHighlights()
        {
            for (int i = 0; i < highlightedBlocks.Count; i++)
            {
                capi.World.HighlightBlocks(capi.World.Player, config.MinLLID + i, new List<BlockPos>(), EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cubes);
            }
            highlightedBlocks.Clear();
        }
    }
}
