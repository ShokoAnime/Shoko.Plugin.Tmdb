using Shoko.Plugin.Tmdb.Api;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using TMDbLib.Utilities.Serializer;
using Xunit;

namespace Shoko.Plugin.Tmdb.Tests;

/// <summary>
///   What TMDB may quote that TMDbLib's own serializer would refuse.
/// </summary>
public sealed class TmdbSerializerTests
{
    [Fact]
    public void AQuotedNumberIsRead()
    {
        var ids = TmdbSerializer.Instance.DeserializeFromString<ExternalIdsTvShow>("""{"id":1,"tvdb_id":"123"}""");

        Assert.Equal(123, ids?.TvdbId);
    }

    [Fact]
    public void ABareNumberStillIsRead()
    {
        var episode = TmdbSerializer.Instance.DeserializeFromString<TvEpisode>("""{"id":1,"episode_number":2,"runtime":24,"air_date":""}""");

        Assert.Equal(24, episode?.Runtime);
        Assert.Null(episode?.AirDate);
    }
}
