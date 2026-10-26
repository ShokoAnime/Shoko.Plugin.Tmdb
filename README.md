# Shoko TMDB Metadata

Brings metadata from [The Movie Database (TMDB)](https://www.themoviedb.org/)
into Shoko Server: shows with their seasons and episodes, movies and
collections, and the cast, crew, studios and networks behind them. Shoko
links your anime to TMDB on its own, and you can fix or add links by hand.

Official server builds come with the plugin installed and turned on. That
bundled copy can't be uninstalled, but a newer release installed over it can.
If you built the server yourself, install the plugin from the Shoko Core
Repository under Settings → Plugin Management → Browse.

Found a problem? Open an issue in the
[issue tracker](https://github.com/ShokoAnime/Shoko.Plugin.Tmdb/issues) or
ask on [Discord](https://discord.gg/shokoanime).

## What you get

- **Shows and movies.** Titles, overviews, cast and crew, genres, keywords,
  content ratings, studios, networks and suggestions. A movie also knows the
  collection it belongs to.
- **Alternate episode orders.** TMDB's episode groups become orderings you can
  pick for a series, when you turn their download on.
- **Collections, people, studios and networks.** Fetched with their own
  details when they're new or out of date. Each kind can be turned off for the
  TMDB provider under Settings → Metadata.
- **Genres as tags.** A combined genre such as "Action & Adventure" becomes
  two tags, so a filter for "Action" finds it too.
- **Images.** Posters, backdrops, logos and profile pictures, with the one
  TMDB uses for an entry picked as its default.
- **Links to TMDB.** Everything with a page on themoviedb.org links to it.

When Shoko searches TMDB for an anime, it compares titles, air dates and
episode counts, scores each candidate and links the best ones. Episodes are
then lined up with AniDB's.

## Settings

You'll find the settings under Settings → Metadata, with the TMDB provider.
They're saved in `tmdb.json` in the plugin's configuration folder.

| Setting | Default | What it does |
|---|---|---|
| Consider Existing Other Links | off | Takes the TMDB links of your other anime into account when linking. |
| Download All Titles | off | Keeps the titles in every language, not just the ones your language settings ask for. |
| Download All Overviews | off | The same, for overviews. |
| Download All Content Ratings | off | The same, for content ratings. |
| Auto Download Alternate Ordering | off | Downloads TMDB's episode groups to use as alternate orderings. |
| Auto Search Show Candidate Count | `5` | How many shows (`1` to `10`) a search looks at closely. More is slower but finds more. |
| Auto Search Movie Candidate Count | `5` | The same, for movies. |
| Incremental Changes Window Days | `1` | How many days back (`0` to `14`) to ask TMDB for changes. `0` turns the change check off. |
| User API Key | none | Your own TMDB API key. Needs a restart. |
| Max Requests Per Window | `10` | How many requests (`1` to `40`) may go out in one window. |
| Window Duration (ms) | `1000` | How long one window lasts (`100` to `10000`). |

Some can also be set with environment variables, which win over the file:
`TMDB_API_KEY`, `TMDB_CHANGES_WINDOW_DAYS`,
`TMDB_RATE_LIMIT_MAX_REQUESTS_PER_WINDOW` and
`TMDB_RATE_LIMIT_WINDOW_DURATION_MS`.

## API key

The plugin's official releases come with a TMDB API key, so there's nothing to
set up. If you set a key of your own, it's used instead. A plugin built from
source has no key until you set one, and until then TMDB stays off: the server
says the provider isn't configured.

## Being gentle with TMDB

All requests go through the plugin's own rate limiter, set by the two window
settings above. Up to four TMDB jobs of each kind run at once. When TMDB asks
us to slow down, or keeps answering with server errors, the plugin pauses its
jobs until TMDB is ready again, and the server shows that TMDB is paused
and why.

## Images

Images are fetched from TMDB's image server in their original size. To use a
different image server, set `TMDB_IMAGE_CDN_URL`, or set the `tmdb` template
in the server's image settings, which wins over both.

## Attribution

<a href="https://www.themoviedb.org/"><img src="https://www.themoviedb.org/assets/v4/logos/v2/blue_square_1-5bdc75aaebeb75dc7ae79426ddd9be3b2be1e342510f8202baf6bffa71d7f5c4.svg" alt="TMDB" width="80"></a>

This plugin uses TMDB and the TMDB APIs but is not endorsed, certified, or
otherwise approved by TMDB. All metadata and images it fetches belong to TMDB
and its contributors.

## For developers

```
dotnet build Shoko.Plugin.Tmdb.slnx -c Release
dotnet test Shoko.Plugin.Tmdb.slnx -c Release
```

The plugin builds against the published `Shoko.Abstractions` package. To
build against a Shoko checkout instead, which also runs the configuration
analyzer, pass `-p:ShokoRepositoryPath=/path/to/Shoko`.

`TmdbMetadataProvider` is the plugin's metadata provider. The core runs the
refresh, search, link, image and entity jobs and calls into it; the plugin
asks TMDB and writes what it gets into the core's shared stores, which the
core reads everything back from. `TmdbSuspensionProvider` reports the rate
limit and server error pauses. The thumbnail and icon in `source/Assets/` are
embedded in the assembly, and the icon is also the `tmdb` source's.

To release, publish a GitHub release with a `v<version>` tag on `stable`. The
workflow builds and tests the plugin, stamps the API key from the `TMDB_API`
secret into `Constants.ApiKey`, packages it, attaches the archive to the
release and adds the release to the manifest on the `metadata` branch. The
key is never committed.
