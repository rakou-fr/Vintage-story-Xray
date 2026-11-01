using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace OreHighlight
{
    public class OreConfig
    {
        public bool IsEnabled { get; set; } = false;
        public int ScanRadius { get; set; } = 24; 
        
        public Dictionary<string, int> Targets { get; set; }

        public OreConfig()
        {
            Targets = new Dictionary<string, int>();
            
            // On utilise 255 pour le premier paramètre (Alpha) pour s'assurer que ce soit 100% visible
            Targets.Add("nativecopper", ColorUtil.ToRgba(255, 255, 120, 0));
            Targets.Add("malachite", ColorUtil.ToRgba(255, 255, 120, 0));
            Targets.Add("hematite", ColorUtil.ToRgba(255, 255, 50, 50));
            Targets.Add("gold", ColorUtil.ToRgba(255, 255, 215, 0));
            Targets.Add("cassiterite", ColorUtil.ToRgba(255, 200, 200, 255));
            Targets.Add("coal", ColorUtil.ToRgba(255, 50, 50, 50));
        }
    }
}