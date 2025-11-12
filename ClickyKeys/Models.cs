using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickyKeys
{

    public enum InputType
    {
        None = 0,

        // Keyboard
        Key = 1,

        // Mouse – buttons
        MouseLeft = 10,
        MouseRight = 11,
        MouseMiddle = 12,
        MouseXButton1 = 13,
        MouseXButton2 = 14,

        // Mouse – scroll
        MouseWheelUp = 20,
        MouseWheelDown = 21,
        MouseWheelLeft = 22,
        MouseWheelRight = 23,
    }

    public enum ColorTarget { 
        Panels, 
        Background,
        Keys,
        Values}


    public class PanelsSettings
    {
        public int Index { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Key KeyCode { get; set; } = Key.None;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InputType Input { get; set; } = InputType.Key;

        public string Description { get; set; } = "";
    }

    public class PanelState
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("panels")]
        public List<PanelsSettings> Panels { get; set; } = new();
    }
     public class hsvColor
    {
        public double hue = 0.0;          
        public double sat = 1.0;         
        public double val = 1.0;         
    }

    public class ColorsPallet
    {
        public Color background;
        public Color panels;
        public Color keys;
        public Color values;
    }
}
