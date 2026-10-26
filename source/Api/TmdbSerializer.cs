using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TMDbLib.Utilities.Serializer;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   TMDbLib's serializer, taking a number TMDB quotes where TMDbLib expects
///   one bare.
/// </summary>
/// <remarks>
///   TMDbLib reads strictly, and one number it cannot read fails the whole
///   answer, where the old library took <c>"123"</c> for 123.
/// </remarks>
public sealed class TmdbSerializer : ITMDbSerializer
{
    private readonly JsonSerializerOptions _options = new(TMDbJsonSerializer.Instance.Options)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    ///   The serializer to give TMDB's client.
    /// </summary>
    public static TmdbSerializer Instance { get; } = new();

    /// <inheritdoc />
    public void Serialize(Stream target, object obj, Type type)
        => TMDbJsonSerializer.Instance.Serialize(target, obj, type);

    /// <inheritdoc />
    public object? Deserialize(Stream source, Type type)
        => JsonSerializer.Deserialize(source, _options.GetTypeInfo(type));
}
