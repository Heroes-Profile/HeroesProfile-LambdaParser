using Amazon.Lambda.Core;
using AWSSignatureV4_S3_Sample.Signers;
using Heroes.ReplayParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace hotslambda
{
    public class Function
    {
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public object FunctionHandler(IDictionary<string, string> dict, ILambdaContext context)
        {
            return MainAsync(dict["input"], dict["access"], dict["secret"], dict["fingerprint"]).GetAwaiter().GetResult();
        }

        static readonly string Prefix = "http://heroesprofile-replayfile-storage.s3.amazonaws.com/";

        public static async System.Threading.Tasks.Task<object> MainAsync(string uri, string AWSAccessKey, string AWSSecretKey, string fingerprint)
        {
            var sp = uri.Split('/');
            uri = Prefix + sp.Last();

            // for a simple GET, we have no body so supply the precomputed 'empty' hash
            var headers = new Dictionary<string, string>
            {
                {AWS4SignerBase.X_Amz_Content_SHA256, AWS4SignerBase.EMPTY_BODY_SHA256},
                {"content-type", "text/plain"},
                {"x-amz-request-payer", "requester"}
            };

            var signer = new AWS4SignerForAuthorizationHeader
            {
                EndpointUri = new Uri(uri),
                HttpMethod = "GET",
                Service = "s3",
                Region = "us-east-1"
            };

            var authorization = signer.ComputeSignature(headers,
                "",   // no query parameters
                AWS4SignerBase.EMPTY_BODY_SHA256,
                AWSAccessKey,
                AWSSecretKey);

            // place the computed signature into a formatted 'Authorization' header 
            // and call S3
            headers.Add("Authorization", authorization);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            foreach (var header in headers.Keys)
            {
                request.Headers[header] = headers[header];
            }
            
            byte[] bytes;
            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var dst = new MemoryStream())
            {
                stream.CopyTo(dst);
                bytes = dst.ToArray();
            }
            
            var result = DataParser.ParseReplay(bytes, ParseOptions.MediumParsing);
            if (result.Item1 != DataParser.ReplayParseResult.Success || result.Item2 == null)
            {
                return $"Error parsing replay: {result.Item1}";
            }
            string calculated_fingerprint = GetFingerprint(result.Item2);

            bool match = true;

            if (calculated_fingerprint != fingerprint)
            {
                match = false;

            }
            return ToJson(result.Item2, match, calculated_fingerprint);
        }

        public static object ToJson(Replay replay, bool match, string calculated_fingerprint)
        {
            var obj = new
            {
                random_value = replay.RandomValue,
                calculated_fingerprint = calculated_fingerprint,
                fingerprint_match = match,
                mode = replay.GameMode.ToString(),
                region = replay.Players[0].BattleNetRegionId,
                date = replay.Timestamp,
                length = replay.ReplayLength,
                map = replay.Map,
                map_short = replay.MapAlternativeName,
                version = replay.ReplayVersion,
                version_major = replay.ReplayVersionMajor,
                version_build = replay.ReplayBuild,
                bans = replay.TeamHeroBans,
                draft_order = replay.DraftOrder,
                team_experience = replay.TeamPeriodicXPBreakdown,
                players = from p in replay.Players
                          select new
                          {
                              battletag_name = p.Name,
                              battletag_id = p.BattleTag,
                              blizz_id = p.BattleNetId,
                              account_level = p.AccountLevel,
                              hero = p.Character,
                              hero_level = p.CharacterLevel,
                              hero_level_taunt = p.HeroMasteryTiers,
                              team = p.Team,
                              winner = p.IsWinner,
                              silenced = p.IsSilenced,
                              party = p.PartyValue,
                              talents = p.Talents.Select(t => t.TalentName),
                              score = p.ScoreResult,
                              staff = p.IsBlizzardStaff,
                              announcer = p.AnnouncerPackAttributeId,
                              banner = p.BannerAttributeId,
                              skin_title = p.SkinAndSkinTint,
                              hero_skin = p.SkinAndSkinTintAttributeId,
                              mount_title = p.MountAndMountTint,
                              mount = p.MountAndMountTintAttributeId,
                              spray_title = p.Spray,
                              spray = p.SprayAttributeId,
                              voice_line_title = p.VoiceLine,
                              voice_line = p.VoiceLineAttributeId,
                          }
            };
            return obj;
        }

        private static string GetFingerprint(Replay replay)
        {
            var str = new StringBuilder();
            replay.Players.Select(p => p.BattleNetId).OrderBy(x => x).Map(x => str.Append(x.ToString()));
            str.Append(replay.RandomValue);
            var md5 = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str.ToString()));
            var result = new Guid(md5);
            return result.ToString();
        }
    }
}
