﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace VSHUD
{
    public class ClientModSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;
    }
}
