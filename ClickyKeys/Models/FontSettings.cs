using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ClickyKeys.Converters;

namespace ClickyKeys
{

    public class FontSettings : INotifyPropertyChanged
    {
        private FontFamily _fontFamily = new FontFamily("Arial");
        private double _fontSize = 36.0;
        private bool _isBold;
        private bool _isItalic;
        private bool _isUnderline;

        [JsonPropertyName("font_family")]
        [JsonConverter(typeof(FontFamilyJsonConverter))]
        public FontFamily FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("font_size")]
        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value <= 0 ? 1 : value; OnPropertyChanged(); }
        }

        [JsonPropertyName("is_bold")]
        public bool IsBold
        {
            get => _isBold;
            set { _isBold = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("is_italic")]
        public bool IsItalic
        {
            get => _isItalic;
            set { _isItalic = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("is_underline")]
        public bool IsUnderline
        {
            get => _isUnderline;
            set { _isUnderline = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

