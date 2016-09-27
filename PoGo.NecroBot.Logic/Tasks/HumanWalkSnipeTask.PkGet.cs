using Newtonsoft.Json;
using PoGo.NecroBot.Logic.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Tasks
{
    public partial class HumanWalkSnipeTask
    {
        public class PkGetWrap
        {
            public class PkGetItem
            {
                public int d1 { get; set; }
                public double d3 { get; set; }
                public double d4 { get; set; }
                public double d5 { get; set; }
                public int d7 { get; set; }
                public string d9 { get; set; }
            }
            public List<PkGetItem> pk123 { get; set; }
        }

        private static SnipePokemonInfo Map(PkGetWrap.PkGetItem result)
        {
            return new SnipePokemonInfo()
            {
                Latitude = result.d4,
                Longitude = result.d5,
                Id = result.d1,
                ExpiredTime = UnixTimeStampToDateTime(result.d3/ 1000),
                Source = "PkGet"
            };
        }
         private static async Task<List<SnipePokemonInfo>> FetchFromPkGet(double lat, double lng)
        {
            List<SnipePokemonInfo> results = new List<SnipePokemonInfo>();
            if (!_session.GlobalSettings.HumanWalkSnipeConfig.UsePkGet) return results;

            //var startFetchTime = DateTime.Now;

            try
            {
                var baseAddress = new Uri("https://pkget.com");
                var handler = new HttpClientHandler
                {
                    UseCookies = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                var client = new HttpClient(handler) { BaseAddress = baseAddress };
                double offset = _session.GlobalSettings.HumanWalkSnipeConfig.SnipingScanOffset;

                var message = new HttpRequestMessage(HttpMethod.Get, $"https://pkget.com/pkm333.ashx?v1=111&v2={lat + offset}&v3={lng + offset}&v4={lat - offset}&v5={lng - offset}&v6=0");
                message.Headers.Host = "pkget.com";
                message.Headers.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
                message.Headers.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36");
                message.Headers.AcceptEncoding.ParseAdd("gzip, deflate");
                message.Headers.Referrer = new Uri("https://pkget.com/");
                message.Headers.Add("X-Requested-With", "XMLHttpRequest");
                message.Headers.Add($"Cookie", "_gat=1; ASP.NET_SessionId=3vckegrdaiaaoirm3aobgt4b; _ga=GA1.2.1195159019.1473693191; pkgetcom=lat0={lat + offset}&lng0={lng + offset}&lat1={lat - offset}&lng1={lng - offset}");

                var result = await client.SendAsync(message);
                result.EnsureSuccessStatusCode();
                
                var task = await result.Content.ReadAsStringAsync();
                //Logger.Write($"FetchFromPkGet responsed {task}", LogLevel.Info, ConsoleColor.White);

                var data = JsonConvert.DeserializeObject<PkGetWrap>(task);
                foreach (var item in data.pk123)
                {
                    var pItem = Map(item);
                    if (pItem != null)
                    {
                        results.Add(pItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error loading data from PkGet {ex}", LogLevel.Error, ConsoleColor.DarkRed);
            }

            //var endFetchTime = DateTime.Now;
            //Logger.Write($"FetchFromPokecrew spent {(endFetchTime - startFetchTime).TotalSeconds} seconds", LogLevel.Info, ConsoleColor.White);
            return results;
        }

    }
}
