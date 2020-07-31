using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class fix_invalid_profile_referencesFixture : MigrationTest<fix_invalid_profile_references>
    {
        private void AddDefaultProfile(fix_invalid_profile_references m, string name, int profileId)
        {
            var allowed = new Quality[] { Quality.WEBDL720p };

            var items = Quality.DefaultQualityDefinitions
                .OrderBy(v => v.Weight)
                .Select(v => new { Quality = (int)v.Quality, Allowed = allowed.Contains(v.Quality) })
                .ToList();

            var profile = new
            {
                Id = profileId,
                Name = name,
                FormatItems = new List<ProfileFormatItem>().ToJson(),
                Cutoff = (int)Quality.WEBDL720p,
                Items = items.ToJson(),
                Language = (int)Language.English,
                MinFormatScore = 0,
                CutOffFormatScore = 0
            };

            m.Insert.IntoTable("Profiles").Row(profile);
        }

        private void AddMovie(fix_invalid_profile_references m, string movieTitle, int tmdbId, int profileId)
        {
            var movie = new
            {
                Id = tmdbId,
                Monitored = true,
                Title = movieTitle,
                CleanTitle = movieTitle,
                Status = MovieStatusType.Announced,
                MinimumAvailability = MovieStatusType.Announced,
                Images = new[] { new { CoverType = "Poster" } }.ToJson(),
                Recommendations = new[] { 1 }.ToJson(),
                HasPreDBEntry = false,
                Runtime = 90,
                OriginalLanguage = 1,
                ProfileId = profileId,
                MovieFileId = 1,
                Path = string.Format("/Movies/{0}", movieTitle),
                TitleSlug = movieTitle,
                TmdbId = tmdbId
            };

            m.Insert.IntoTable("Movies").Row(movie);
        }

        [Test]
        public void should_not_change_movies_with_valid_profile()
        {
            var profileId = 2;

            var db = WithMigrationTestDb(c =>
            {
                AddDefaultProfile(c, "My Custom Profile", profileId);
                AddMovie(c, "movie", 123456, profileId);
            });

            var items = db.Query<Movie179>("SELECT Id, ProfileId FROM Movies");

            items.Should().HaveCount(1);
            items.First().ProfileId.Should().Be(profileId);
        }

        [Test]
        public void should_change_movies_with_bad_profile_id()
        {
            var profileId = 2;

            var db = WithMigrationTestDb(c =>
            {
                AddDefaultProfile(c, "My Custom Profile", profileId);
                AddMovie(c, "movie", 123456, 1);
            });

            var items = db.Query<Movie179>("SELECT Id, ProfileId FROM Movies");

            items.Should().HaveCount(1);
            items.First().ProfileId.Should().Be(profileId);
        }

        [Test]
        public void should_change_to_most_common_valid_profile_in_library()
        {
            var commonProfileId = 2;
            var otherProfileId = 3;

            var db = WithMigrationTestDb(c =>
            {
                AddDefaultProfile(c, "My Custom Profile", commonProfileId);
                AddDefaultProfile(c, "My Custom Profile 2", otherProfileId);
                AddMovie(c, "movie1", 123451, 1);
                AddMovie(c, "movie2", 123452, 1);
                AddMovie(c, "movie3", 123453, 1);
                AddMovie(c, "movie4", 123454, 1);
                AddMovie(c, "movie5", 123455, commonProfileId);
                AddMovie(c, "movie6", 123456, commonProfileId);
                AddMovie(c, "movie7", 123457, commonProfileId);
                AddMovie(c, "movie8", 123458, otherProfileId);
                AddMovie(c, "movie9", 123459, otherProfileId);
            });

            var items = db.Query<Movie179>("SELECT Id, ProfileId FROM Movies");

            items.Should().HaveCount(9);
            items.Where(x => x.ProfileId == commonProfileId).Should().HaveCount(7);
        }
    }

    public class Movie179
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
    }
}
