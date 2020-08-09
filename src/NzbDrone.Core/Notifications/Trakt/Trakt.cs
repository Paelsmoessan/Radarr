using System;
using System.Collections.Generic;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Notifications.Trakt.Resource;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Trakt
{
    public class Trakt : NotificationBase<TraktSettings>
    {
        private readonly ITraktProxy _proxy;
        private readonly INotificationRepository _notificationRepository;
        private readonly Logger _logger;

        public Trakt(ITraktProxy proxy, INotificationRepository notificationRepository, Logger logger)
        {
            _proxy = proxy;
            _notificationRepository = notificationRepository;
            _logger = logger;
        }

        public override string Link => "https://trakt.tv/";
        public override string Name => "Trakt";

        public override void OnDownload(DownloadMessage message)
        {
            var payload = new TraktCollectMoviesResource
            {
                Movies = new List<TraktCollectMovie>()
            };

            var traktResolution = MapResolution(message.MovieFile.Quality.Quality.Resolution);
            var mediaType = MapMediaType(message.MovieFile.Quality.Quality.Source);
            var audio = MapAudio(message.MovieFile);

            payload.Movies.Add(new TraktCollectMovie
            {
                Title = message.Movie.Title,
                Year = message.Movie.Year,
                CollectedAt = DateTime.Now,
                Resolution = traktResolution,
                MediaType = mediaType,
                AudioChannels = MediaInfoFormatter.FormatAudioChannels(message.MovieFile.MediaInfo).ToString(),
                Audio = audio,
                Ids = new TraktMovieIdsResource
                {
                    Tmdb = message.Movie.TmdbId,
                    Imdb = message.Movie.ImdbId ?? "",
                }
            });

            _proxy.AddToCollection(payload, Settings.AccessToken);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }

        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            if (action == "startOAuth")
            {
                var request = _proxy.GetOAuthRequest(query["callbackUrl"]);

                return new
                {
                    OauthUrl = request.Url.ToString()
                };
            }
            else if (action == "getOAuthToken")
            {
                return new
                {
                    accessToken = query["access_token"],
                    expires = DateTime.UtcNow.AddSeconds(int.Parse(query["expires_in"])),
                    refreshToken = query["refresh_token"],
                    authUser = _proxy.GetUserName(query["access_token"])
                };
            }

            return new { };
        }

        public void RefreshToken()
        {
            _logger.Trace("Refreshing Token");

            Settings.Validate().Filter("RefreshToken").ThrowOnError();

            try
            {
                var response = _proxy.RefreshAuthToken(Settings.RefreshToken);

                if (response != null)
                {
                    var token = response;
                    Settings.AccessToken = token.AccessToken;
                    Settings.Expires = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                    Settings.RefreshToken = token.RefreshToken ?? Settings.RefreshToken;

                    if (Definition.Id > 0)
                    {
                        _notificationRepository.UpdateSettings((NotificationDefinition)Definition);
                    }
                }
            }
            catch (HttpException)
            {
                _logger.Warn($"Error refreshing trakt access token");
            }
        }

        private string MapMediaType(Source source)
        {
            var traktSource = string.Empty;

            switch (source)
            {
                case Source.BLURAY:
                    traktSource = "bluray";
                    break;
                case Source.WEBDL:
                    traktSource = "digital";
                    break;
                case Source.WEBRIP:
                    traktSource = "digital";
                    break;
                case Source.DVD:
                    traktSource = "dvd";
                    break;
                case Source.TV:
                    traktSource = "dvd";
                    break;
            }

            return traktSource;
        }

        private string MapResolution(int resolution)
        {
            var traktResolution = string.Empty;

            switch (resolution)
            {
                case 2160:
                    traktResolution = "uhd_4k";
                    break;
                case 1080:
                    traktResolution = "hd_1080p";
                    break;
                case 720:
                    traktResolution = "hd_720p";
                    break;
                case 576:
                    traktResolution = "sd_576p";
                    break;
                case 480:
                    traktResolution = "sd_480p";
                    break;
            }

            return traktResolution;
        }

        private string MapAudio(MovieFile movieFile)
        {
            var traktAudioFormat = string.Empty;

            var audioCodec = MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, movieFile.SceneName);

            switch (audioCodec)
            {
                case "AC3":
                    traktAudioFormat = "dolby_digital";
                    break;
                case "EAC3":
                    traktAudioFormat = "dolby_digital_plus";
                    break;
                case "TrueHD":
                    traktAudioFormat = "dolby_truehd";
                    break;
                case "EAC3 Atmos":
                case "TrueHD Atmos":
                    traktAudioFormat = "dolby_atmos";
                    break;
                case "DTS":
                case "DTS-ES":
                    traktAudioFormat = "dts";
                    break;
                case "DTS-HD MA":
                    traktAudioFormat = "dts_ma";
                    break;
                case "DTS-HD HRA":
                    traktAudioFormat = "dts_hr";
                    break;
                case "DTS-X":
                    traktAudioFormat = "dts_x";
                    break;
                case "MP3":
                    traktAudioFormat = "mp3";
                    break;
                case "Vorbis":
                    traktAudioFormat = "ogg";
                    break;
                case "WMA":
                    traktAudioFormat = "wma";
                    break;
            }

            return traktAudioFormat;
        }
    }
}
