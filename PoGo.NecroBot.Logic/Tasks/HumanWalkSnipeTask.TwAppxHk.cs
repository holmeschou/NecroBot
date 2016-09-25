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
        public class TwAppxHkWrap
        {
            public int i { get; set; }
            public double a { get; set; }
            public double o { get; set; }
            public double t { get; set; }
            public int w { get; set; }
        }

        private static SnipePokemonInfo Map(TwAppxHkWrap result)
        {
            return new SnipePokemonInfo()
            {
                Latitude = result.a,
                Longitude = result.o,
                Id = result.i,
                ExpiredTime = UnixTimeStampToDateTime(result.t),
                Source = "TwAppxHk"
            };
        }
         private static async Task<List<SnipePokemonInfo>> FetchFromTwAppxHk(double lat, double lng)
        {
            List<SnipePokemonInfo> results = new List<SnipePokemonInfo>();
            if (!_setting.HumanWalkingSnipeUseTwAppxHk) return results;

            //var startFetchTime = DateTime.Now;

            try
            {
                var baseAddress = new Uri("http://tw-pogo.appx.hk/");
                var handler = new HttpClientHandler
                {
                    UseCookies = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                var client = new HttpClient(handler) { BaseAddress = baseAddress };
                double offset = _setting.HumanWalkingSnipeSnipingScanOffset;

                var message = new HttpRequestMessage(HttpMethod.Get, $"http://tw-pogo.appx.hk/");
                message.Headers.Host = "tw-pogo.appx.hk";
                message.Headers.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
                message.Headers.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36");
                message.Headers.AcceptEncoding.ParseAdd("gzip, deflate");
                message.Headers.Referrer = new Uri("https://tw.appx.hk/map");

                var result = await client.SendAsync(message);
                result.EnsureSuccessStatusCode();
                
                var task = await result.Content.ReadAsStringAsync();
                //Logger.Write($"FetchFromTwAppxHk responsed {task}", LogLevel.Info, ConsoleColor.White);

                var data = JsonConvert.DeserializeObject<List<TwAppxHkWrap>>(task);
                foreach (var item in data)
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
                Logger.Write($"Error loading data from TwAppxHk {ex}", LogLevel.Error, ConsoleColor.DarkRed);
            }

            //var endFetchTime = DateTime.Now;
            //Logger.Write($"FetchFromPokecrew spent {(endFetchTime - startFetchTime).TotalSeconds} seconds", LogLevel.Info, ConsoleColor.White);
            return results;
        }

    }
}
