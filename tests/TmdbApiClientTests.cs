using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using TMDbLib.Objects.Changes;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.TvShows;
using Xunit;

namespace Shoko.Plugin.Tmdb.Tests;

/// <summary>
///   The plugin's TMDB client over the fixtures: what it answers for an entry
///   TMDB lacks, how it retries, and how it feeds the rate limiter.
/// </summary>
public sealed class TmdbApiClientTests
{
    private readonly TmdbRoutes _routes = new();

    [Fact]
    public async Task AnEntryTmdbLacksIsNull()
    {
        using var client = TmdbTestClient.Create(_routes);

        Assert.Null(await client.GetShow(404, TvShowMethods.Undefined, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithoutAnApiKeyNothingIsAsked()
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration());

        Assert.False(client.HasApiKey);
        await Assert.ThrowsAsync<MetadataProviderNotConfiguredException>(() => client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));
        Assert.Empty(_routes.Paths);
    }

    [Fact]
    public async Task ARateLimitIsWaitedOutAndRetried()
    {
        _routes.Status("tv/1001", (HttpStatusCode)429, TimeSpan.FromMilliseconds(50)).Fixture("tv/1001", "show-1001.json");
        using var client = TmdbTestClient.Create(_routes);

        var show = await client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken);

        Assert.Equal(1001, show?.Id);
        Assert.Equal(2, _routes.Count("tv/1001"));
        Assert.Null(client.RateLimiter.ResumesAt);
    }

    [Fact]
    public async Task AServerErrorIsThrownAndCountsTowardsThePause()
    {
        _routes.Status("tv/1001", HttpStatusCode.ServiceUnavailable);
        using var client = TmdbTestClient.Create(_routes);

        for (var attempt = 0; attempt < 3; attempt++)
            await Assert.ThrowsAsync<TMDbServiceUnavailableException>(() => client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));

        Assert.NotNull(client.RateLimiter.ServerErrorsUntil);
    }

    [Fact]
    public async Task ARequestThatTimesOutIsTriedAgainAndThenThrown()
    {
        _routes.Hang("tv/1001");
        using var client = TmdbTestClient.Create(_routes, timeout: TimeSpan.FromMilliseconds(20));

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));

        Assert.True(TmdbApiClient.IsTransient(exception));
        Assert.Equal(4, _routes.Count("tv/1001"));
    }

    [Fact]
    public async Task AnInvalidIDIsAnEntryTMDBLacks()
    {
        _routes.Json("tv/1001", """{"success":false,"status_code":44,"status_message":"The ID is invalid."}""", HttpStatusCode.InternalServerError);
        using var client = TmdbTestClient.Create(_routes);

        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Null(await client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));

        Assert.Null(client.RateLimiter.ServerErrorsUntil);
    }

    [Fact]
    public async Task TheShowsChangesNameTheSeasonsAndEpisodes()
    {
        _routes.Fixture("tv/1001/changes", "changes-show-1001.json");
        using var client = TmdbTestClient.Create(_routes);

        var changes = await client.GetShowChanges(1001, DateTime.UtcNow.AddHours(-1), TestContext.Current.CancellationToken);

        Assert.NotNull(changes);
        Assert.Equal([1], changes.SeasonNumbers.Order());
        Assert.Equal([(1, 2)], changes.Episodes);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, -2)]
    public async Task TheChangesAreNotAskedOutsideTheWindow(int windowDays, int lastRefreshedDaysAgo)
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration { UserApiKey = "test-key", IncrementalChangesWindowDays = windowDays });

        Assert.Null(await client.GetShowChanges(1001, DateTime.UtcNow.AddDays(lastRefreshedDaysAgo), TestContext.Current.CancellationToken));
        Assert.True(await client.HasMovieChanged(7001, DateTime.UtcNow.AddDays(lastRefreshedDaysAgo), TestContext.Current.CancellationToken));
        Assert.Empty(_routes.Paths);
    }

    [Fact]
    public async Task TheImageServerFallsBackToTmdbsOwnWithoutAKey()
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration());

        Assert.Equal(TmdbApiClient.DefaultImageServerUrl, await client.GetImageServerUrl(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public void OnlyServerErrorsArePassing(HttpStatusCode status, bool transient)
        => Assert.Equal(transient, TmdbApiClient.IsTransient(new GeneralHttpException(status)));

    [Fact]
    public void TheChangesAreReadFromEveryKindOfItem()
    {
        static object Json(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        var changes = TmdbShowChanges.Parse(
        [
            new() { Key = "episode", Items = [new ChangeItemAdded { Value = Json("""{"season_number":1,"episode_number":3}""") }] },
            new() { Key = "episode", Items = [new ChangeItemDeleted { OriginalValue = Json("""{"season_number":2,"episode_number":"4"}""") }] },
            new() { Key = "season", Items = [new ChangeItemDestroyed { Value = Json("""{"season_number":3}""") }] },
            new() { Key = "episode", Items = [new ChangeItemUpdated { Value = Json("""{"season_number":null,"episode_number":5}""") }] },
            new() { Key = "name", Items = [new ChangeItemUpdated { Value = "Renamed" }] },
        ]);

        Assert.Equal([1, 2, 3], changes.SeasonNumbers.Order());
        Assert.Equal([(1, 3), (2, 4)], changes.Episodes.Order());
    }

    [Fact]
    public void AnInvalidIDIsNotAServerError()
    {
        var invalid = new TMDbServerException(new TMDbStatusMessage { StatusCode = (int)TMDbStatusCode.IdInvalid });

        Assert.False(TmdbApiClient.IsServerError(invalid));
        Assert.True(TmdbApiClient.IsServerError(new TMDbServerException(new TMDbStatusMessage { StatusCode = (int)TMDbStatusCode.InternalError })));
    }

    [Theory]
    [InlineData(typeof(TMDbServiceUnavailableException), true)]
    [InlineData(typeof(TMDbServerException), true)]
    [InlineData(typeof(TMDbAuthenticationException), false)]
    public void TMDbLibsOwnServerErrorsArePassingToo(Type exceptionType, bool transient)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, [(TMDbStatusMessage?)null])!;

        Assert.Equal(transient, TmdbApiClient.IsServerError(exception));
        Assert.Equal(transient, TmdbApiClient.IsTransient(exception));
    }
}
