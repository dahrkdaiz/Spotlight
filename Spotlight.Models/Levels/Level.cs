﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Spotlight.Models
{
    public class Level
    {
        public const int BLOCK_HEIGHT = 27;
        public const int BLOCK_WIDTH = 15 * 16;

        public Level()
        {
            TileData = new int[240 * 27];
            LevelPointers = new List<LevelPointer>();
            ObjectData = new List<LevelObject>();
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public int TileSetIndex { get; set; }
        public int ClearTileIndex { get; set; }
        public int MusicValue { get; set; }
        public int ScreenLength { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int PaletteEffect { get; set; }
        public int PaletteIndex { get; set; }
        public int StaticTileTableIndex { get; set; }
        
        [JsonIgnore]
        public int AnimationTileTableIndex
        {
            get
            {
                switch (AnimationType)
                {
                    case 1:
                        return 0xD0;

                    case 2:
                        return 0xF8;

                    case 3:
                        return 0x5C;

                    default:
                        return 0x80;
                }
            }
        }
        public int AnimationType { get; set; }
        public int ScrollType { get; set; }
        public bool TemporaryProjectileInteractions { get; set; }
        public bool RhythmBlocks { get; set; }
        public int EventType { get; set; }
        public int[] TileData { get; set; }
        public List<LevelObject> ObjectData { get; set; }
        public List<LevelPointer> LevelPointers { get; set; }
    }

    [XmlRoot("level")]
    public class LegacyLevel
    {
        [XmlAttribute]
        public string guid { get; set; }

        [XmlAttribute]
        public string type { get; set; }

        [XmlAttribute]
        public string graphicsbank { get; set; }

        [XmlAttribute]
        public string clearvalue { get; set; }

        [XmlAttribute]
        public string music { get; set; }

        [XmlAttribute]
        public string length { get; set; }

        [XmlAttribute]
        public string xstart { get; set; }

        [XmlAttribute]
        public string ystart { get; set; }

        [XmlAttribute]
        public string invincibleenemies { get; set; }

        [XmlAttribute]
        public string paletteeffect { get; set; }

        [XmlAttribute]
        public string palette { get; set; }

        [XmlAttribute]
        public string animationtype { get; set; }

        [XmlAttribute]
        public string startaction { get; set; }

        [XmlAttribute]
        public string scrolltype { get; set; }

        [XmlAttribute]
        public string tempprojeffects { get; set; }

        [XmlAttribute]
        public string rhythm { get; set; }

        [XmlAttribute]
        public string leveldata { get; set; }

        [XmlAttribute]
        public string misc1 { get; set; }

        [XmlArray("pointers")]
        [XmlArrayItem("pointer")]
        public List<LegacyLevelPointer> pointers { get; set; }

        [XmlArray("spritedata")]
        [XmlArrayItem("sprite")]
        public List<LegacyLevelObject> spritedata { get; set; }

        [XmlIgnore]
        public string name { get; set; }


        public static List<string> MusicValues = new List<string>() {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "A",
            "B",
            "C",
            "D",
            "E",
            "F",
            "10",
            "20",
            "30",
            "40",
            "50",
            "60",
            "70",
            "80",
            "90",
            "A0",
            "B0"
            };
    }
}