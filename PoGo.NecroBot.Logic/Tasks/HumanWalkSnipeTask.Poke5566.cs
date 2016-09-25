﻿using Newtonsoft.Json;
using PoGo.NecroBot.Logic.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Tasks
{
    public partial class HumanWalkSnipeTask
    {
        public class Poke5566Wrap
        {
            public class Poke5566Item
            {
                public int id { get; set; }
                public double time { get; set; }
                public double lat { get; set; }
                public double lng { get; set; }
            }
            public List<Poke5566Item> pokemons { get; set; }
        }

        private static SnipePokemonInfo Map(Poke5566Wrap.Poke5566Item result)
        {
            return new SnipePokemonInfo()
            {
                Latitude = result.lat,
                Longitude = result.lng,
                Id = result.id,
                ExpiredTime = UnixTimeStampToDateTime(result.time / 1000),
                Source = "Poke5566"
            };
        }
         private static async Task<List<SnipePokemonInfo>> FetchFromPoke5566(double lat, double lng)
        {
            List<SnipePokemonInfo> results = new List<SnipePokemonInfo>();
            //if (!_setting.HumanWalkingSnipeUsePokecrew) return results;

            //var startFetchTime = DateTime.Now;

            try
            {
                var baseAddress = new Uri("https://poke5566.com");
                var handler = new HttpClientHandler { UseCookies = false };
                var client = new HttpClient(handler) { BaseAddress = baseAddress };
                double offset = _setting.HumanWalkingSnipeSnipingScanOffset;

                var message = new HttpRequestMessage(HttpMethod.Get, $"https://poke5566.com/pokemons?lat0={lat + offset}&lng0={lng + offset}&lat1={lat - offset}&lng1={lng - offset}");
                message.Headers.Host = "poke5566.com";
                message.Headers.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
                message.Headers.AcceptEncoding.ParseAdd("gzip, deflate, sdch, br");
                message.Headers.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36");
                message.Headers.Referrer = new Uri("https://poke5566.com/");
                message.Headers.Add("X-Requested-With", "XMLHttpRequest");
                message.Headers.Add($"Cookie", "_gat=1; poke5566=lat0={lat + offset}&lng0={lng + offset}&lat1={lat - offset}&lng1={lng - offset}; _ga=GA1.2.1937162754.1473467316");

                var result = await client.SendAsync(message);
                result.EnsureSuccessStatusCode();
                
                Logger.Write($"FetchFromPoke5566 called", LogLevel.Info, ConsoleColor.White);
                var task = await result.Content.ReadAsStringAsync();
                Logger.Write($"FetchFromPoke5566 responsed {task}", LogLevel.Info, ConsoleColor.White);

                var data = JsonConvert.DeserializeObject<Poke5566Wrap>(task);
                foreach (var item in data.pokemons)
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
                Logger.Write($"Error loading data from Poke5566 {ex}", LogLevel.Error, ConsoleColor.DarkRed);
            }

            //var endFetchTime = DateTime.Now;
            //Logger.Write($"FetchFromPokecrew spent {(endFetchTime - startFetchTime).TotalSeconds} seconds", LogLevel.Info, ConsoleColor.White);
            return results;
        }

    }
}
