using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace ClickyKeys.Converters
{
    public sealed class FontFamilyJsonConverter : JsonConverter<FontFamily>
    {
        public override FontFamily Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var name = reader.GetString();

            return new FontFamily(string.IsNullOrWhiteSpace(name) ? "Calibri" : name);
        }

        public override void Write(Utf8JsonWriter writer, FontFamily value, JsonSerializerOptions options)
        {

            writer.WriteStringValue(value?.Source ?? "Calibri");
        }
    }
}





