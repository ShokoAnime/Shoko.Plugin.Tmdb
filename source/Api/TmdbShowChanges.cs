using System.Collections.Generic;
using System.Text.Json;
using TMDbLib.Objects.Changes;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   The seasons and episodes of a show TMDB recorded changes to.
/// </summary>
/// <param name="SeasonNumbers">The numbers of the seasons that changed.</param>
/// <param name="Episodes">The season and episode numbers of the episodes that changed.</param>
public sealed record TmdbShowChanges(IReadOnlySet<int> SeasonNumbers, IReadOnlySet<(int Season, int Episode)> Episodes)
{
    /// <summary>
    ///   Reads TMDB's changes of a show into the seasons and episodes that
    ///   changed.
    /// </summary>
    /// <remarks>
    ///   Only the <c>season</c> and <c>episode</c> keys are read, and an
    ///   episode's change marks its season too, so the season is fetched with
    ///   it.
    /// </remarks>
    /// <param name="changes">The changes TMDB reported.</param>
    /// <returns>What changed.</returns>
    public static TmdbShowChanges Parse(IEnumerable<Change> changes)
    {
        var seasons = new HashSet<int>();
        var episodes = new HashSet<(int, int)>();
        foreach (var change in changes)
        {
            if (change.Key is not ("episode" or "season"))
                continue;

            foreach (var item in change.Items ?? [])
            {
                var value = item switch
                {
                    ChangeItemAdded added => added.Value,
                    ChangeItemUpdated updated => updated.Value,
                    ChangeItemDestroyed destroyed => destroyed.Value,
                    // A deleted item carries the value it had.
                    ChangeItemDeleted deleted => deleted.OriginalValue,
                    _ => null,
                };
                if (Number(value, "season_number") is not { } seasonNumber)
                    continue;

                seasons.Add(seasonNumber);
                if (change.Key is "episode" && Number(value, "episode_number") is { } episodeNumber)
                    episodes.Add((seasonNumber, episodeNumber));
            }
        }

        return new(seasons, episodes);
    }

    private static int? Number(object? value, string name)
    {
        var number = 0;
        return value is JsonElement { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var property)
            && property.ValueKind switch
            {
                JsonValueKind.Number => property.TryGetInt32(out number),
                JsonValueKind.String => int.TryParse(property.GetString(), out number),
                _ => false,
            }
            ? number
            : null;
    }
}
