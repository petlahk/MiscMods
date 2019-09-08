﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace WaypointUtils
{
    class ClockDialogSystem : ModSystem
    {
        private long id;

        public override void StartClientSide(ICoreClientAPI api)
        {
            GuiDialogClock clock = new GuiDialogClock(api);

            api.Input.RegisterHotKey("clock", "Open Clock GUI", GlKeys.ControlRight, HotkeyType.GUIOrOtherControls);

            id = api.Event.RegisterGameTickListener(dt => 
            {
                if (api.World.Player?.Entity != null)
                {
                    if (api.Settings.Bool["clockGui"])
                    {
                        clock.TryOpen();
                    }

                    api.Event.UnregisterGameTickListener(id);
                }
                
            }, 1000);

            api.Input.SetHotKeyHandler("clock", a =>
            {
                clock.Toggle();
                return true;
            });
        }
    }

    class GuiDialogClock : HudElement
    {
        long id;
        long id2;

        public GuiDialogClock(ICoreClientAPI capi) : base(capi)
        {
            this.capi = capi;
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();

            ElementBounds textBounds = ElementBounds.Fixed(EnumDialogArea.LeftMiddle, 5, -250, 500, 500);

            double[] stroke = new double[] { 0, 0, 0, 1 };

            SingleComposer = capi.Gui.CreateCompo("clock", textBounds)
                .AddDynamicText("", CairoFont.WhiteSmallText().WithStroke(stroke, 2), EnumTextOrientation.Justify, textBounds, "clock")
                .Compose();

            bool dot = false;
            string d = "";

            UpdateText(d);

            id2 = capi.World.RegisterGameTickListener(dt => d = (dot = !dot) ? ":" : " ", 2000);
            id = capi.World.RegisterGameTickListener(dt => UpdateText(d), 30);
        }

        public void UpdateText(string dot)
        {
            BlockPos entityPos = capi.World.Player.Entity.Pos.AsBlockPos;
            ClimateCondition climate = capi.World.BlockAccessor.GetClimateAt(entityPos);

            GameCalendar cal = (GameCalendar)capi.World.Calendar;

            string hour = cal.FullHourOfDay < 10 ? "0" + cal.FullHourOfDay : "" + cal.FullHourOfDay;
            int m = (int)(60 * (cal.HourOfDay - cal.FullHourOfDay));
            string minute = m < 10 ? "0" + m : "" + m;

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Date: " + cal.DayOfYear + "/" + cal.DaysPerYear + ", " + cal.Year)
                .AppendLine("Time: " + hour + dot + minute)
                .AppendLine("Global Season: " + cal.Season)
                .AppendLine("Average Temperature: " + Math.Round(climate.Temperature, 3))
                .AppendLine("Average Rainfall: " + Math.Round(climate.Rainfall, 3))
                .AppendLine("Average Fertility: " + Math.Round(climate.Fertility, 3));

            SingleComposer.GetDynamicText("clock").SetNewText(stringBuilder.ToString());
        }

        public override bool TryOpen()
        {
            if (base.TryOpen())
            {
                OnOwnPlayerDataReceived();
                capi.Settings.Bool["clockGui"] = true;
                return true;
            }
            return false;
        }

        public override bool TryClose()
        {
            if (base.TryClose())
            {
                capi.Settings.Bool["clockGui"] = false;
                Dispose();
                return true;
            }
            return false;
        }

        public override void Dispose()
        {
            base.Dispose();
            capi.World.UnregisterGameTickListener(id);
            capi.World.UnregisterGameTickListener(id2);
        }

        public override string ToggleKeyCombinationCode => "clock";
    }
}