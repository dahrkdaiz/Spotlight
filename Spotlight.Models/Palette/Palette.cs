﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Serialization;

namespace Spotlight.Models
{

    public class Palette
    {
        public Palette()
        {
            IndexedColors = new int[32];
            RgbColors = new Color[8][];
            for(int i = 0; i < 8; i++)
            {
                RgbColors[i] = new Color[4];
            }
        }
        public int[] IndexedColors { get; set; }
        public string Name { get; set; }


        [JsonIgnore]
        public int BackgroundColor
        {
            get
            {
                return IndexedColors[0];
            }
        }

        [JsonIgnore]
        public Color[][] RgbColors { get; set; }
    }

    public class LegacyPalette
    {
        [XmlAttribute]
        public string background { get; set; }

        [XmlAttribute]
        public string data { get; set; }

        [XmlAttribute]
        public string name { get; set; }

        [XmlAttribute]
#pragma warning disable IDE1006 // Naming Styles
        public string guid { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}