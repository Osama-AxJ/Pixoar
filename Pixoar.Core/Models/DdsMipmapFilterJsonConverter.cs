using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixoar.Core.Models;

/// <summary>
/// Serializes DDS mipmap filters and migrates legacy texconv-default values to Fant.
/// </summary>
public sealed class DdsMipmapFilterJsonConverter : JsonConverter<DdsMipmapFilter>
{
    /// <inheritdoc />
    public override DdsMipmapFilter Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString()?.Trim().ToLowerInvariant() switch
            {
                "fant" => DdsMipmapFilter.Fant,
                "linear" => DdsMipmapFilter.Linear,
                "cubic" => DdsMipmapFilter.Cubic,
                "triangle" => DdsMipmapFilter.Triangle,
                "default" or "texconvdefault" or "default (texconv)" => DdsMipmapFilter.Fant,
                _ => DdsMipmapFilter.Fant
            };
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value))
        {
            return value switch
            {
                0 or 1 => DdsMipmapFilter.Fant,
                2 => DdsMipmapFilter.Linear,
                3 => DdsMipmapFilter.Cubic,
                4 => DdsMipmapFilter.Triangle,
                _ => DdsMipmapFilter.Fant
            };
        }

        throw new JsonException("DDS mipmap filter must be a string or number.");
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        DdsMipmapFilter value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            DdsMipmapFilter.Fant => "fant",
            DdsMipmapFilter.Linear => "linear",
            DdsMipmapFilter.Cubic => "cubic",
            DdsMipmapFilter.Triangle => "triangle",
            _ => "fant"
        });
    }
}
