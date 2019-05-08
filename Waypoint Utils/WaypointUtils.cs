﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace WaypointUtils
{
    class WaypointUtilConfig
    {
        public double DotRange { get; set; } = 2000.0;
        public double TitleRange { get; set; } = 500.0;
        public bool PerBlockWaypoints { get; set; } = false;
    }

    class WaypointUtilSystem : ModSystem
    {
        long id;
        ICoreClientAPI capi;
        GuiDialogFloatyWaypoints floatyPoints;
        public static WaypointUtilConfig Config = new WaypointUtilConfig();

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            LoadConfig();

            capi.Input.RegisterHotKey("viewwaypoints", "View Waypoints", GlKeys.U, HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler("viewwaypoints", ViewWaypoints);
            capi.Input.RegisterHotKey("culldeathwaypoints", "Cull Death Waypoints", GlKeys.O, HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler("culldeathwaypoints", CullDeathWaypoints);
            capi.Input.RegisterHotKey("reloadwaypointconfig", "Reload Waypoint Util Config", GlKeys.I, HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler("reloadwaypointconfig", a => { LoadConfig(); Repopulate(); return true; });
            capi.RegisterCommand("wpcfg", "Waypoint Configurtion", "[dotrange|titlerange|perblockwaypoints]", new ClientChatCommandDelegate(CmdWaypointConfig));

            id = api.World.RegisterGameTickListener(dt =>
            {
                EntityPlayer player = api.World.Player.Entity;

                if (player != null)
                {
                    player.WatchedAttributes.RegisterModifiedListener("entityDead", () =>
                    {
                        if (player.WatchedAttributes["entityDead"].ToString() == "1")
                        {
                            api.SendChatMessage("/waypoint add #" + ColorStuff.RandomHexColorVClamp(api, 0.50, 0.80) + " *Player Death Waypoint*");
                        }
                    });

                    api.World.RegisterCallback(d =>
                    {
                        if (Layer().ownWaypoints.Count > 0 && capi.Settings.Bool["floatywaypoints"]) OpenWaypoints();
                    }, 500);

                    api.World.RegisterGameTickListener(d =>
                    {
                        if (Layer().ownWaypoints.Count != guiDialogs.Count && guiDialogs.Count > 0) Repopulate();
                    }, 500);


                    api.World.UnregisterGameTickListener(id);
                }
            }, 500);

        }

        private void CmdWaypointConfig(int groupId, CmdArgs args)
        {
            string arg = args.PopWord();
            switch (arg)
            {
                case "dotrange":
                    double? dr = args.PopDouble();
                    Config.DotRange = dr != null ? (double)dr : Config.DotRange;
                    capi.ShowChatMessage("Dot Range Set To " + Config.DotRange + " Meters.");
                    break;
                case "titlerange":
                    double? tr = args.PopDouble();
                    Config.TitleRange = tr != null ? (double)tr : Config.TitleRange;
                    capi.ShowChatMessage("Title Range Set To " + Config.TitleRange + " Meters.");
                    break;
                case "perblockwaypoints":
                    bool? pb = args.PopBool();
                    Config.PerBlockWaypoints = pb != null ? (bool)pb : Config.PerBlockWaypoints;
                    capi.ShowChatMessage("Per Block Waypoints Set To " + Config.PerBlockWaypoints + ".");
                    break;
                default:
                    capi.ShowChatMessage(Lang.Get("Syntax: .wpcfg [dotrange|titlerange|perblockwaypoints]"));
                    break;
            }
            SaveConfig();
            Repopulate();
        }

        public void LoadConfig()
        {
            if (capi.LoadModConfig<WaypointUtilConfig>("waypointutils.json") == null) { SaveConfig(); return; }

            Config = capi.LoadModConfig<WaypointUtilConfig>("waypointutils.json");
            SaveConfig();
        }

        public void SaveConfig() => capi.StoreModConfig(Config, "waypointutils.json");

        WaypointMapLayer Layer()
        {
            WorldMapManager modMapManager = capi.ModLoader.GetModSystem("Vintagestory.GameContent.WorldMapManager") as WorldMapManager;
            return modMapManager.MapLayers.Single(l => l is WaypointMapLayer) as WaypointMapLayer;
        }

        List<GuiDialogFloatyWaypoints> guiDialogs = new List<GuiDialogFloatyWaypoints>();
        private bool ViewWaypoints(KeyCombination t1)
        {
            if (guiDialogs.Count != 0) CloseAndClear();
            else OpenWaypoints();
            return true;
        }

        public void CloseAndClear()
        {
            for (int i = 0; i < guiDialogs.Count; i++)
            {
                guiDialogs[i].TryClose();
            }
            guiDialogs.Clear();
        }

        public void OpenWaypoints()
        {
            WaypointMapLayer layer = Layer();

            guiDialogs = new List<GuiDialogFloatyWaypoints>();

            for (int i = 0; i < layer.ownWaypoints.Count; i++)
            {
                string text = layer.ownWaypoints[i].Title != null ? "Waypoint: " + layer.ownWaypoints[i].Title : "Waypoint: ";
                int color = layer.ownWaypoints[i].Color;
                Vec3d wPos = Config.PerBlockWaypoints ? layer.ownWaypoints[i].Position.AsBlockPos.ToVec3d().SubCopy(0, 0.5, 0) : layer.ownWaypoints[i].Position;

                floatyPoints = new GuiDialogFloatyWaypoints(text, capi, wPos, color);

                floatyPoints.OnOwnPlayerDataReceived();
                if (floatyPoints.TryOpen())
                {
                    guiDialogs.Add(floatyPoints);
                }
            }
        }

        public bool CullDeathWaypoints(KeyCombination t1)
        {
            WaypointMapLayer layer = Layer();
            List<string> commands = new List<string>();

            for (int i = 0; i < layer.ownWaypoints.Count; i++)
            {
                if (layer.ownWaypoints[i].Title.Contains("*Player Death Waypoint*"))
                {
                    commands.Add("/waypoint remove " + i);
                }
            }
            if (commands.Count > 0)
            {
                for (int i = commands.Count; i-- > 0;)
                {
                    capi.SendChatMessage(commands[i]);
                }

                Repopulate();
            }
            return true;
        }

        public void Repopulate()
        {
            ViewWaypoints(new KeyCombination());
            ViewWaypoints(new KeyCombination());
        }
    }

    public class GuiDialogFloatyWaypoints : HudElement
    {
        Vec3d waypointPos;
        string DialogTitle;
        int color;
        string dialogText = "";
        double distance = 0;

        public GuiDialogFloatyWaypoints(string DialogTitle, ICoreClientAPI capi, Vec3d waypointPos, int color) : base(capi)
        {
            this.DialogTitle = DialogTitle;
            this.waypointPos = waypointPos;
            this.color = color;
        }

        public override void OnOwnPlayerDataReceived()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialogAtPos(0.0);
            ElementBounds textBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, 250, 50);

            double[] dColor = ColorUtil.ToRGBADoubles(color);

            CairoFont font = CairoFont.WhiteSmallText();
            font.Color = dColor;
            font = font.WithStroke(new double[] { 0, 0, 0, 1 }, 0.5);

            SingleComposer = capi.Gui
                .CreateCompo(DialogTitle + capi.Gui.OpenedGuis.Count + 1, dialogBounds)
                .AddDynamicText("", font, EnumTextOrientation.Center, textBounds, "text")
                .Compose()
            ;
            UpdateDialog();

            if (capi.Settings.Bool["floatywaypoints"]) TryOpen();

            capi.World.RegisterGameTickListener(dt => UpdateDialog(), 500);
        }

        public void UpdateDialog()
        {
            EntityPlayer entityPlayer = capi.World.Player.Entity;
            distance = Math.Round(Math.Sqrt(entityPlayer.Pos.SquareDistanceTo(waypointPos)), 3);
            dialogText = DialogTitle + " " + distance + "m" + "\n\u2022";
        }

        protected virtual double FloatyDialogPosition => 0.75;
        protected virtual double FloatyDialogAlign => 0.75;

        public override bool ShouldReceiveMouseEvents() => false;

        public override void OnRenderGUI(float deltaTime)
        {
            if (!capi.Settings.Bool["floatywaypoints"]) return;

            WaypointUtilConfig config = WaypointUtilSystem.Config;

            EntityPlayer entityPlayer = capi.World.Player.Entity;
            Vec3d aboveHeadPos = new Vec3d(waypointPos.X + 0.5, waypointPos.Y + FloatyDialogPosition, waypointPos.Z + 0.5);
            Vec3d pos = MatrixToolsd.Project(aboveHeadPos, capi.Render.PerspectiveProjectionMat, capi.Render.PerspectiveViewMat, capi.Render.FrameWidth, capi.Render.FrameHeight);
            ElementBounds bounds = ElementBounds.Empty;

            if (pos.Z < 0 || (distance > config.DotRange && !dialogText.Contains("*")))
            {
                SingleComposer.GetDynamicText("text").SetNewText("");
                return;
            }

            SingleComposer.Bounds.Alignment = EnumDialogArea.None;
            SingleComposer.Bounds.fixedOffsetX = 0;
            SingleComposer.Bounds.fixedOffsetY = 0;
            SingleComposer.Bounds.absFixedX = pos.X - SingleComposer.Bounds.OuterWidth / 2;
            SingleComposer.Bounds.absFixedY = capi.Render.FrameHeight - pos.Y - SingleComposer.Bounds.OuterHeight * FloatyDialogAlign;
            SingleComposer.Bounds.absMarginX = 0;
            SingleComposer.Bounds.absMarginY = 0;

            double yBounds = (SingleComposer.Bounds.absFixedY / capi.Render.FrameHeight) + 0.025;
            double xBounds = (SingleComposer.Bounds.absFixedX / capi.Render.FrameWidth) + 0.065;

            bool isAligned = (yBounds > 0.49 && yBounds < 0.51) && (xBounds > 0.49 && xBounds < 0.51);

            if (isAligned || distance < config.TitleRange || dialogText.Contains("*")) SingleComposer.GetDynamicText("text").SetNewText(dialogText);
            else SingleComposer.GetDynamicText("text").SetNewText("\n\u2022");

            base.OnRenderGUI(deltaTime);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Settings.Bool["floatywaypoints"] = false;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            capi.Settings.Bool["floatywaypoints"] = true;
        }
    }

    class ColorStuff
    {
        public static int RandomColor(ICoreAPI api) => ColorUtil.HsvToRgb(
            (int)(api.World.Rand.NextDouble() * 255),
            (int)(api.World.Rand.NextDouble() * 255),
            (int)(api.World.Rand.NextDouble() * 255)
            );

        public static string RandomHexColor(ICoreAPI api) => RandomColor(api).ToString("X");
        public static string RandomHexColorVClamp(ICoreAPI api, double min, double max) => ClampedRandomColorValue(api, min, max).ToString("X");

        public static int ClampedRandomColorValue(ICoreAPI api, double min, double max)
        {
            return ColorUtil.HsvToRgb(
            (int)(api.World.Rand.NextDouble() * 255),
            (int)(api.World.Rand.NextDouble() * 255),
            (int)(GameMath.Clamp(api.World.Rand.NextDouble(), min, max) * 255)
            );
        }
    }
    /*
    class HaxorMan
    {
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
    }
    */
}