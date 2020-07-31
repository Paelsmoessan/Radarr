using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(180)]
    public class fix_invalid_profile_references : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(FixMovies);
            Execute.WithConnection(FixLists);
        }

        private void FixMovies(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = GetProfileIds(conn, tran);

            var rows = conn.Query<ProfileEntity179>($"SELECT Id, ProfileId FROM Movies");

            var mostCommonProfileId = rows.Select(x => x.ProfileId)
                                          .Where(x => profiles.Contains(x))
                                          .GroupBy(p => p)
                                          .OrderByDescending(g => g.Count())
                                          .Select(g => g.Key)
                                          .FirstOrDefault();

            if (mostCommonProfileId == 0)
            {
                mostCommonProfileId = profiles.First();
            }

            var sql = $"UPDATE Movies SET ProfileId = {mostCommonProfileId} WHERE Id IN(SELECT Movies.Id FROM Movies LEFT OUTER JOIN Profiles ON Movies.ProfileId = Profiles.Id WHERE Profiles.Id IS NULL)";
            conn.Execute(sql, transaction: tran);
        }

        private void FixLists(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = GetProfileIds(conn, tran);

            var rows = conn.Query<ProfileEntity179>($"SELECT Id, ProfileId FROM NetImport");

            var mostCommonProfileId = rows.Select(x => x.ProfileId)
                                          .Where(x => profiles.Contains(x))
                                          .GroupBy(p => p)
                                          .OrderByDescending(g => g.Count())
                                          .Select(g => g.Key)
                                          .FirstOrDefault();

            if (mostCommonProfileId == 0)
            {
                mostCommonProfileId = profiles.First();
            }

            var sql = $"UPDATE NetImport SET ProfileId = {mostCommonProfileId} WHERE Id IN(SELECT NetImport.Id FROM NetImport LEFT OUTER JOIN Profiles ON NetImport.ProfileId = Profiles.Id WHERE Profiles.Id IS NULL)";
            conn.Execute(sql, transaction: tran);
        }

        private List<int> GetProfileIds(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = new List<int>();

            using (var getProfilesCmd = conn.CreateCommand())
            {
                getProfilesCmd.Transaction = tran;
                getProfilesCmd.CommandText = @"SELECT Id FROM Profiles";

                using (var profileReader = getProfilesCmd.ExecuteReader())
                {
                    while (profileReader.Read())
                    {
                        profiles.Add(profileReader.GetInt32(0));
                    }
                }
            }

            return profiles;
        }

        private class ProfileEntity179
        {
            public int Id { get; set; }
            public int ProfileId { get; set; }
        }
    }
}
