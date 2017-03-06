using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode.Internal;
using YoutubeExplode.Models;
using YoutubeExplode.Services;

namespace YoutubeExplode
{
    /// <summary>
    /// YoutubeClient
    /// </summary>
    public partial class YoutubeClient : IDisposable
    {
        private readonly IRequestService _requestService;
        private readonly Dictionary<string, PlayerSource> _playerSourceCache = new Dictionary<string, PlayerSource>();

        /// <summary>
        /// Whether to get file sizes of video streams (one HEAD request per stream)
        /// </summary>
        public bool ShouldGetVideoFileSizes { get; set; } = true;

        /// <summary>
        /// Construct with custom services
        /// </summary>
        public YoutubeClient(IRequestService requestService)
        {
            _requestService = requestService;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public YoutubeClient()
            : this(new DefaultRequestService())
        {
        }

        private async Task<PlayerSource> GetPlayerSourceAsync(string version)
        {
            if (version.IsBlank())
                throw new ArgumentNullException(nameof(version));

            // Try get cached player source
            var playerSource = _playerSourceCache.GetOrDefault(version);

            // If not available - decompile a new one
            if (playerSource == null)
            {
                // Get the javascript source URL
                string response = await _requestService.GetStringAsync($"https://www.youtube.com/yts/jsbin/player-{version}/base.js")
                                                       .ConfigureAwait(false);
                if (response.IsBlank())
                    throw new Exception("Could not get the video player source code");

                // Decompile
                playerSource = Parser.ParsePlayerSourceJs(response);
                playerSource.Version = version;

                // Cache
                _playerSourceCache[version] = playerSource;
            }

            return playerSource;
        }

        private async Task DecipherAsync(VideoInfo videoInfo)
        {
            if (videoInfo == null)
                throw new ArgumentNullException(nameof(videoInfo));
            if (!videoInfo.NeedsDeciphering)
                throw new Exception("Given video info does not need to be deciphered");

            // Get player source
            var playerSource = await GetPlayerSourceAsync(videoInfo.PlayerVersion).ConfigureAwait(false);

            // Unscramble streams
            foreach (var streamInfo in videoInfo.Streams.Where(s => s.NeedsDeciphering))
            {
                string sig = streamInfo.Signature;
                string newSig = playerSource.Unscramble(sig);
                streamInfo.Url = streamInfo.Url.SetQueryParameter("signature", newSig);
                streamInfo.NeedsDeciphering = false;
            }

            // Unscramble dash manifest
            if (videoInfo.DashManifest != null && videoInfo.DashManifest.NeedsDeciphering)
            {
                string sig = videoInfo.DashManifest.Signature;
                string newSig = playerSource.Unscramble(sig);
                videoInfo.DashManifest.Url = videoInfo.DashManifest.Url.Replace($"/s/{sig}", $"/signature/{newSig}");
                videoInfo.DashManifest.NeedsDeciphering = false;
            }
        }

        /// <summary>
        /// Get full information about a video by its ID
        /// </summary>
        /// <param name="videoId">The ID of the video</param>
        /// <returns><see cref="VideoInfo"/> object with the information on the given video</returns>
        public async Task<VideoInfo> GetVideoInfoAsync(string videoId)
        {
            if (videoId.IsBlank())
                throw new ArgumentNullException(nameof(videoId));
            if (!ValidateVideoId(videoId))
                throw new ArgumentException("Is not a valid Youtube video ID", nameof(videoId));

            // Get video info
            string eurl = $"https://youtube.googleapis.com/v/{videoId}".UrlEncode();
            string response = await _requestService.GetStringAsync($"https://www.youtube.com/get_video_info?video_id={videoId}&sts=17221&eurl={eurl}")
                                                   .ConfigureAwait(false);
            if (response.IsBlank())
                throw new Exception("Could not get video info");

            // Parse video info
            var result = Parser.ParseVideoInfoUrlEncoded(response);
            if (result == null)
                throw new Exception("Could not parse video info");

            // Get player version
            response = await _requestService.GetStringAsync($"https://www.youtube.com/watch?v={videoId}&gl=US&hl=en&has_verified=1&bpctr=9999999999")
                                            .ConfigureAwait(false);
            if (response.IsBlank())
                throw new Exception("Could not get player version");

            // Parse player version
            result.PlayerVersion = Parser.ParsePlayerVersionHtml(response);

            // Decipher
            if (result.NeedsDeciphering)
            {
                await DecipherAsync(result).ConfigureAwait(false);
            }

            // Get additional streams from dash if available
            if (result.DashManifest != null)
            {
                // Get
                response = await _requestService.GetStringAsync(result.DashManifest.Url)
                                                .ConfigureAwait(false);
                if (response.IsBlank())
                    throw new Exception("Could not get dash manifest");

                // Parse
                var dashStreams = Parser.ParseVideoStreamInfosMpd(response);
                result.Streams = result.Streams.With(dashStreams).ToArray();
            }

            // Finalize the stream list
            result.Streams = result.Streams
                .Distinct(s => s.Itag) // only one stream per itag
                .OrderByDescending(s => s.Quality) // sort by quality
                .ThenByDescending(s => s.Bitrate) // then by bitrate
                .ThenByDescending(s => s.FileSize) // then by filesize
                .ThenByDescending(s => s.Type) // then by type
                .ToArray();

            // Get file size of streams
            if (ShouldGetVideoFileSizes)
            {
                foreach (var streamInfo in result.Streams)
                    await GetFileSizeAsync(streamInfo).ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>
        /// Gets and populates file size of the given video stream
        /// </summary>
        public async Task<long> GetFileSizeAsync(VideoStreamInfo streamInfo)
        {
            if (streamInfo == null)
                throw new ArgumentNullException(nameof(streamInfo));
            if (streamInfo.Url.IsBlank())
                throw new Exception("Given stream does not have a URL");
            if (streamInfo.NeedsDeciphering)
                throw new Exception("Given stream's signature needs to be deciphered first");

            // Get the headers
            var headers = await _requestService.GetHeadersAsync(streamInfo.Url)
                                               .ConfigureAwait(false);
            if (headers == null)
                throw new Exception("Could not obtain headers");

            // Get file size header
            if (!headers.ContainsKey("Content-Length"))
                throw new Exception("Content-Length header not found");

            return streamInfo.FileSize = headers["Content-Length"].ParseLongOrDefault();
        }


        /// <summary>
        /// Downloads the given video stream
        /// </summary>
        public async Task<Stream> DownloadVideoAsync(VideoStreamInfo streamInfo)
        {
            if (streamInfo == null)
                throw new ArgumentNullException(nameof(streamInfo));
            if (streamInfo.Url.IsBlank())
                throw new Exception("Given stream does not have a URL");
            if (streamInfo.NeedsDeciphering)
                throw new Exception("Given stream's signature needs to be deciphered first");

            // Get stream
            var stream = await _requestService.GetStreamAsync(streamInfo.Url)
                                              .ConfigureAwait(false);
            if (stream == null)
                throw new Exception("Could not get response stream");

            return stream;
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
            (_requestService as IDisposable)?.Dispose();
        }
    }

    public partial class YoutubeClient
    {
        /// <summary>
        /// Verifies that the given string is a valid youtube video ID
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public static bool ValidateVideoId(string videoId)
        {
            if (videoId.IsBlank())
                return false;

            return !Regex.IsMatch(videoId, @"[^0-9a-zA-Z_\-]");
        }

        /// <summary>
        /// Tries to parse video ID from a youtube video URL
        /// </summary>
        /// <returns>Whether the execution was successful or not</returns>
        public static bool TryParseVideoId(string videoUrl, out string videoId)
        {
            videoId = default(string);

            if (videoUrl.IsBlank())
                return false;

            // https://www.youtube.com/watch?v=yIVRs6YSbOM
            string regularMatch = Regex.Match(videoUrl, @"youtube\..+?/watch\?.*?v=(.+?)(?:&|$)").Groups[1].Value;
            if (regularMatch.IsNotBlank() && ValidateVideoId(regularMatch))
            {
                videoId = regularMatch;
                return true;
            }

            // https://youtu.be/yIVRs6YSbOM
            string shortMatch = Regex.Match(videoUrl, @"youtu.be/(.+?)(?:&|$)").Groups[1].Value;
            if (shortMatch.IsNotBlank() && ValidateVideoId(shortMatch))
            {
                videoId = shortMatch;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses video ID from a youtube video URL
        /// </summary>
        /// <returns>Video ID</returns>
        public static string ParseVideoId(string videoUrl)
        {
            if (videoUrl.IsBlank())
                throw new ArgumentNullException(nameof(videoUrl));

            string result;
            bool success = TryParseVideoId(videoUrl, out result);
            if (success)
                return result;

            throw new FormatException("Could not parse Video ID from given string");
        }
    }
}