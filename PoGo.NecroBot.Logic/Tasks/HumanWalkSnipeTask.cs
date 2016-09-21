﻿using Newtonsoft.Json;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces.Configuration;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.Model;
using PoGo.NecroBot.Logic.Model.Settings;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Enums;
using POGOProtos.Inventory.Item;
using POGOProtos.Map.Fort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Tasks
{
    //need refactor this class, move list snipping pokemon to session and split function out to smaller class.
    public partial class HumanWalkSnipeTask
    {
        public class SnipePokemonInfo
        {
            public double Distance { get; set; }
            public double EstimatedTime { get; set; }
            public bool IsCatching { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int Id { get; set; }
            public DateTime ExpiredTime { get; set; }
            public bool IsFake { get; set; }
            public bool IsVisited { get; set; }
            public HumanWalkSnipeFilter Setting { get; set; }
            public PokemonId PokemonId
            {
                get
                {
                    return (PokemonId)(Id);
                }
            }
            public string UniqueId
            {
                get
                {
                    return $"{Id:000}-{Latitude:0.000000}-{Longitude:0.000000}";
                }
            }
            public string Source { get; set; }
            public double IV { get; internal set; }
        }

        private static List<SnipePokemonInfo> rarePokemons = new List<SnipePokemonInfo>();
        private static ISession _session;
        private static ILogicSettings _setting;
        private static int pokestopCount = 0;
        private static List<PokemonId> pokemonToBeSnipedIds = null;
        static bool prioritySnipeFlag = false;
        private static DateTime lastUpdated = DateTime.Now.AddMinutes(-10);

        public static async Task AddSnipePokemon(string source, PokemonId id, double latitude, double longitude, DateTime expirationTimestamp, double iV = 0, ISession session = null)
        {
            Logger.Write($"(Holmes) AddSnipePokemon", LogLevel.Info, ConsoleColor.Red);
            if (session != null)
            {
                InitSession(session);
            }
            if (_session == null || _session == null) return;

            
            await PostProcessDataFetched(new List<SnipePokemonInfo> {
                new SnipePokemonInfo()
                {
                    Latitude =latitude,
                    Longitude = longitude,
                    Id = (int) id,
                    ExpiredTime = expirationTimestamp,
                    Source = source,
                    IV = iV
                }
            }, false);
        }

        public static async Task<bool> CheckPokeballsToSnipe(int minPokeballs, ISession session,
            CancellationToken cancellationToken)
        {
            Logger.Write($"(Holmes) CheckPokeballsToSnipe", LogLevel.Info, ConsoleColor.Red);
            cancellationToken.ThrowIfCancellationRequested();

            // Refresh inventory so that the player stats are fresh
            await session.Inventory.RefreshCachedInventory();
            var pokeBallsCount = await session.Inventory.GetItemAmountByType(ItemId.ItemPokeBall);
            pokeBallsCount += await session.Inventory.GetItemAmountByType(ItemId.ItemGreatBall);
            pokeBallsCount += await session.Inventory.GetItemAmountByType(ItemId.ItemUltraBall);
            pokeBallsCount += await session.Inventory.GetItemAmountByType(ItemId.ItemMasterBall);

            if (pokeBallsCount < minPokeballs)
            {
                session.EventDispatcher.Send(new HumanWalkSnipeEvent
                {
                    Type = HumanWalkSnipeEventTypes.NotEnoughtPalls,
                    CurrentBalls = pokeBallsCount,
                    MinBallsToSnipe = minPokeballs,
                });
                return false;
            }
            return true;
        }

        public static async Task ExecuteFetchData(ISession session)
        {
            Logger.Write($"(Holmes) ExecuteFetchData", LogLevel.Info, ConsoleColor.Red);
            InitSession(session);

            if (!_setting.EnableHumanWalkingSnipe)
                return;

            await FetchData(_session.Client.CurrentLatitude, _session.Client.CurrentLongitude, true);
        }

        private static void InitSession(ISession session)
        {
            _session = session;
            _setting = _session.LogicSettings;
            pokemonToBeSnipedIds = _setting.HumanWalkingSnipeUseSnipePokemonList ? _setting.PokemonToSnipe.Pokemon : new List<PokemonId>();
            pokemonToBeSnipedIds.AddRange(_setting.HumanWalkSnipeFilters.Where(x => !pokemonToBeSnipedIds.Any(t => t == x.Key)).Select(x => x.Key).ToList());      //this will combine with pokemon snipe filter
        }

        public static List<SnipePokemonInfo> ApplyFilter(List<SnipePokemonInfo> source)
        {
            Logger.Write($"(Holmes) ApplyFilter", LogLevel.Info, ConsoleColor.Red);
            return source.Where(p => !p.IsVisited
            && !p.IsFake
            && p.ExpiredTime > DateTime.Now.AddSeconds(p.EstimatedTime))
            .ToList();
        }

        public static async Task Execute(ISession session, CancellationToken cancellationToken, FortData originalPokestop)
        {
            Logger.Write($"(Holmes) Execute", LogLevel.Info, ConsoleColor.Red);
            pokestopCount++;
            pokestopCount = pokestopCount % 3;

            //if (pokestopCount > 0 && !prioritySnipeFlag) return;

            InitSession(session);
            if (!_setting.CatchPokemon && !prioritySnipeFlag) return;

            cancellationToken.ThrowIfCancellationRequested();

            if (_setting.HumanWalkingSnipeTryCatchEmAll)
            {
                var checkBall = await CheckPokeballsToSnipe(_setting.HumanWalkingSnipeCatchEmAllMinBalls, session, cancellationToken);
                if (!checkBall && !prioritySnipeFlag) return;
            }

            bool caughtAnyPokemonInThisWalk = false;
            SnipePokemonInfo pokemon = null;
            do
            {
                prioritySnipeFlag = false;
                pokemon = await GetNextSnipeablePokemon(session.Client.CurrentLatitude, session.Client.CurrentLongitude, !caughtAnyPokemonInThisWalk);
                if (pokemon != null)
                {
                    if (session.LogicSettings.ActivateMSniper)
                    {
                        await MSniperServiceTask.Execute(session, cancellationToken);
                    }

                    caughtAnyPokemonInThisWalk = true;
                    CalculateDistanceAndEstTime(pokemon);
                    var remainTimes = (pokemon.ExpiredTime - DateTime.Now).TotalSeconds * 0.95; //just use 90% times
                    var catchPokemonTimeEST = (pokemon.Distance / 100) * 10;  //assume that 100m we catch 1 pokemon and it took 10 second for each.
                    string strPokemon = session.Translation.GetPokemonTranslation(pokemon.PokemonId);
                    var spinPokestopEST = (pokemon.Distance / 100) * 5;

                    bool catchPokemon = (pokemon.EstimatedTime + catchPokemonTimeEST) < remainTimes && pokemon.Setting.CatchPokemonWhileWalking;
                    bool spinPokestop = pokemon.Setting.SpinPokestopWhileWalking && (pokemon.EstimatedTime + catchPokemonTimeEST + spinPokestopEST) < remainTimes;
                    pokemon.IsCatching = true;
                    session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                    {
                        PokemonId = pokemon.PokemonId,
                        Latitude = pokemon.Latitude,
                        Longitude = pokemon.Longitude,
                        Distance = pokemon.Distance,
                        Expires = (pokemon.ExpiredTime - DateTime.Now).TotalSeconds,
                        Estimate = (int)pokemon.EstimatedTime,
                        Setting = pokemon.Setting,
                        CatchPokemon = catchPokemon,
                        Pokemons = ApplyFilter(rarePokemons),
                        SpinPokeStop = pokemon.Setting.SpinPokestopWhileWalking,
                        WalkSpeedApplied = pokemon.Setting.AllowSpeedUp ? pokemon.Setting.MaxSpeedUpSpeed : _session.LogicSettings.WalkingSpeedInKilometerPerHour,
                        Type = HumanWalkSnipeEventTypes.StartWalking,
                        Rarity = PokemonGradeHelper.GetPokemonGrade(pokemon.PokemonId).ToString()
                    });
                    var snipeTarget = new SnipeLocation(pokemon.Latitude, pokemon.Longitude,
                           LocationUtils.getElevation(session, pokemon.Latitude, pokemon.Longitude));

                    await session.Navigation.Move(snipeTarget,
                        async () =>
                        {
                            if (session.LogicSettings.ActivateMSniper)
                            {
                                await MSniperServiceTask.Execute(session, cancellationToken);
                            }
                            await ActionsWhenTravelToSnipeTarget(session, cancellationToken, pokemon, catchPokemon, spinPokestop);
                        },
                       session,
                       cancellationToken, pokemon.Setting.AllowSpeedUp ? pokemon.Setting.MaxSpeedUpSpeed : 0);
                    session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                    {
                        Latitude = pokemon.Latitude,
                        Longitude = pokemon.Longitude,
                        PauseDuration = pokemon.Setting.DelayTimeAtDestination / 1000,
                        Type = HumanWalkSnipeEventTypes.DestinationReached,
                        UniqueId = pokemon.UniqueId
                    });

                    await Task.Delay(pokemon.Setting.DelayTimeAtDestination);
                    await CatchNearbyPokemonsTask.Execute(session, cancellationToken, pokemon.PokemonId, false);
                    await Task.Delay(1000);
                    if (!pokemon.IsVisited)
                    {
                        await CatchLurePokemonsTask.Execute(session, cancellationToken);

                    }
                    pokemon.IsVisited = true;
                    pokemon.IsCatching = false;
                }
            }
            while (pokemon != null && _setting.HumanWalkingSnipeTryCatchEmAll);

            if (caughtAnyPokemonInThisWalk && (!_setting.HumanWalkingSnipeAlwaysWalkBack || _setting.UseGpxPathing))
            {
                if (session.LogicSettings.UseGpxPathing)
                {
                    await WalkingBackGPXPath(session, cancellationToken, originalPokestop);
                }
                else
                    await UpdateFarmingPokestop(session, cancellationToken);
            }
        }

        private static async Task WalkingBackGPXPath(ISession session, CancellationToken cancellationToken, FortData originalPokestop)
        {
            Logger.Write($"(Holmes) WalkingBackGPXPath", LogLevel.Info, ConsoleColor.Red);
            var destination = new FortLocation(originalPokestop.Latitude, originalPokestop.Longitude,
                         LocationUtils.getElevation(session, originalPokestop.Latitude, originalPokestop.Longitude), originalPokestop, null);
            await session.Navigation.Move(destination,
               async () =>
               {
                   if (session.LogicSettings.ActivateMSniper)
                   {
                       await MSniperServiceTask.Execute(session, cancellationToken);
                   }
                   await CatchNearbyPokemonsTask.Execute(session, cancellationToken);
                   await UseNearbyPokestopsTask.SpinPokestopNearBy(session, cancellationToken);
               },
               session,
               cancellationToken);
        }

        private static async Task UpdateFarmingPokestop(ISession session, CancellationToken cancellationToken)
        {
            Logger.Write($"(Holmes) UpdateFarmingPokestop", LogLevel.Info, ConsoleColor.Red);
            cancellationToken.ThrowIfCancellationRequested();

            var nearestStop = session.Forts.OrderBy(i =>
                LocationUtils.CalculateDistanceInMeters(session.Client.CurrentLatitude,
                    session.Client.CurrentLongitude, i.Latitude, i.Longitude)).FirstOrDefault();

            if (nearestStop != null)
            {
                var walkedDistance = LocationUtils.CalculateDistanceInMeters(nearestStop.Latitude, nearestStop.Longitude, session.Client.CurrentLatitude, session.Client.CurrentLongitude);
                if (walkedDistance > session.LogicSettings.HumanWalkingSnipeWalkbackDistanceLimit)
                {
                    await Task.Delay(3000, cancellationToken);
                    var nearbyPokeStops = await UseNearbyPokestopsTask.UpdateFortsData(session);
                    session.EventDispatcher.Send(new HumanWalkSnipeEvent
                    {
                        Type = HumanWalkSnipeEventTypes.PokestopUpdated,
                        Pokestops = nearbyPokeStops,
                        NearestDistance = walkedDistance
                    });
                }
            }
        }

        private static async Task ActionsWhenTravelToSnipeTarget(ISession session, CancellationToken cancellationToken, SnipePokemonInfo pokemon, bool allowCatchPokemon, bool allowSpinPokeStop)
        {
            Logger.Write($"(Holmes) ActionsWhenTravelToSnipeTarget", LogLevel.Info, ConsoleColor.Red);
            var distance = LocationUtils.CalculateDistanceInMeters(pokemon.Latitude, pokemon.Longitude, session.Client.CurrentLatitude, session.Client.CurrentLongitude);

            if (allowCatchPokemon && distance > 50.0)
            {
                // Catch normal map Pokemon
                await CatchNearbyPokemonsTask.Execute(session, cancellationToken, sessionAllowTransfer: false);
            }
            if (allowSpinPokeStop)
            {
                //looking for neaby pokestop. spin it
                await UseNearbyPokestopsTask.SpinPokestopNearBy(session, cancellationToken, null);
            }
        }

        static void CalculateDistanceAndEstTime(SnipePokemonInfo p)
        {
            Logger.Write($"(Holmes) CalculateDistanceAndEstTime", LogLevel.Info, ConsoleColor.Red);
            double speed = p.Setting.AllowSpeedUp ? p.Setting.MaxSpeedUpSpeed : _setting.WalkingSpeedInKilometerPerHour;
            var speedInMetersPerSecond = speed / 3.6;

            p.Distance = CalculateDistanceInMeters(_session.Client.CurrentLatitude, _session.Client.CurrentLongitude, p.Latitude, p.Longitude);
            p.EstimatedTime = p.Distance / speedInMetersPerSecond + p.Setting.DelayTimeAtDestination / 1000 + 15; //margin 30 second

        }

        private static async Task<SnipePokemonInfo> GetNextSnipeablePokemon(double lat, double lng, bool refreshData = true)
        {
            Logger.Write($"(Holmes) GetNextSnipeablePokemon", LogLevel.Info, ConsoleColor.Red);
            if (refreshData)
            {
                await FetchData(lat, lng);
            }

            rarePokemons.RemoveAll(p => p.ExpiredTime < DateTime.Now);

            rarePokemons.ForEach(CalculateDistanceAndEstTime);

            //remove list not reach able (expired)
            if (rarePokemons.Count > 0)
            {
                var ordered = rarePokemons.Where(p => !p.IsVisited &&
                    !p.IsFake &&
                    (p.Setting.Priority == 0 || (
                    p.Distance < p.Setting.MaxDistance &&
                    p.EstimatedTime < p.Setting.MaxWalkTimes)
                    && p.ExpiredTime > DateTime.Now.AddSeconds(p.EstimatedTime)
                    )
                )
                .OrderBy(p => p.Setting.Priority)
                .ThenBy(p => p.Distance);
                if (ordered != null && ordered.Count() > 0)
                {
                    var first = ordered.First();
                    return first;
                }
            }
            return null;
        }

        private static async Task FetchData(double lat, double lng, bool silent = false)
        {
            Logger.Write($"(Holmes) FetchData", LogLevel.Info, ConsoleColor.Red);
            if (lastUpdated > DateTime.Now.AddSeconds(-30) && !silent) return;

            if (lastUpdated < DateTime.Now.AddSeconds(-30) && silent && rarePokemons != null && rarePokemons.Count > 0)
            {
                rarePokemons.ForEach(CalculateDistanceAndEstTime);
                rarePokemons = rarePokemons.OrderBy(p => p.Setting.Priority).ThenBy(p => p.Distance).ToList();
                _session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                {
                    Type = HumanWalkSnipeEventTypes.ClientRequestUpdate,
                    Pokemons = ApplyFilter(rarePokemons),
                });
            }

            List<Task<List<SnipePokemonInfo>>> allTasks = new List<Task<List<SnipePokemonInfo>>>()
            {
                FetchFromPokeradar(lat, lng),
                FetchFromSkiplagged(lat, lng),
                FetchFromPokecrew(lat, lng) ,
                FetchFromPokesnipers(lat, lng),
                FetchFromPokeZZ(lat, lng),
                FetchFromFastPokemap(lat, lng),
                FetchFromPokeWatcher(lat, lng)
            };
            if (_setting.HumanWalkingSnipeIncludeDefaultLocation &&
                LocationUtils.CalculateDistanceInMeters(lat, lng, _session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude) > 1000)
            {
                allTasks.Add(FetchFromPokeradar(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromSkiplagged(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromPokecrew(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromPokesnipers(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromPokeZZ(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromFastPokemap(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
                allTasks.Add(FetchFromPokeWatcher(_session.Settings.DefaultLatitude, _session.Settings.DefaultLongitude));
            }

            Task.WaitAll(allTasks.ToArray());
            lastUpdated = DateTime.Now;
            var fetchedPokemons = allTasks.SelectMany(p => p.Result);

            await PostProcessDataFetched(fetchedPokemons, !silent);
        }

        public static T Clone<T>(object item)
        {
            if (item != null)
            {
                string json = JsonConvert.SerializeObject(item);
                return JsonConvert.DeserializeObject<T>(json);
            }
            else
                return default(T);
        }

        private static async Task PostProcessDataFetched(IEnumerable<SnipePokemonInfo> pokemons, bool displayList = true)
        {
            Logger.Write($"(Holmes) PostProcessDataFetched", LogLevel.Info, ConsoleColor.Red);
            var rw = new Random();
            var speedInMetersPerSecond = _setting.WalkingSpeedInKilometerPerHour / 3.6;
            int count = 0;
            await Task.Run(() =>
            {
                foreach (var item in pokemons)
                {
                    //the pokemon data already in the list
                    if (rarePokemons.Any(x => x.UniqueId == item.UniqueId ||
                    (LocationUtils.CalculateDistanceInMeters(x.Latitude, x.Longitude, item.Latitude, item.Longitude) < 10 && item.Id == x.Id)))
                    {
                        continue;
                    }
                    //check if pokemon in the snip list
                    if (!pokemonToBeSnipedIds.Any(x => x == item.PokemonId)) continue;

                    count++;
                    var snipeSetting = _setting.HumanWalkSnipeFilters.FirstOrDefault(x => x.Key == item.PokemonId);

                    HumanWalkSnipeFilter config = new HumanWalkSnipeFilter(_setting.HumanWalkingSnipeMaxDistance,
                        _setting.HumanWalkingSnipeMaxEstimateTime,
                        3, //default priority
                        _setting.HumanWalkingSnipeTryCatchEmAll,
                        _setting.HumanWalkingSnipeSpinWhileWalking,
                        _setting.HumanWalkingSnipeAllowSpeedUp,
                        _setting.HumanWalkingSnipeMaxSpeedUpSpeed,
                        _setting.HumanWalkingSnipeDelayTimeAtDestination);

                    if (_setting.HumanWalkSnipeFilters.Any(x => x.Key == item.PokemonId))
                    {
                        config = _setting.HumanWalkSnipeFilters.First(x => x.Key == item.PokemonId).Value;
                    }
                    item.Setting = Clone<HumanWalkSnipeFilter>(config);

                    CalculateDistanceAndEstTime(item);

                    if (item.Distance < 10000 && item.Distance != 0)  //only add if distance <10km
                    {
                        rarePokemons.Add(item);
                    }
                }

                rarePokemons = rarePokemons.OrderBy(p => p.Setting.Priority).ThenBy(p => p.Distance).ToList();
            });
            if (count > 0)
            {
                _session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                {
                    Type = HumanWalkSnipeEventTypes.PokemonScanned,
                    Pokemons = ApplyFilter(rarePokemons),
                });

                if (_setting.HumanWalkingSnipeDisplayList)
                {
                    var ordered = rarePokemons.Where(p => p.ExpiredTime > DateTime.Now.AddSeconds(p.EstimatedTime) && !p.IsVisited).ToList();

                    if (ordered.Count > 0 && displayList)
                    {
                        Logger.Write(string.Format("     Source      |  Name               |    Distance    |   Expires        |  Travel times   | Catchable"));
                        foreach (var pokemon in ordered)
                        {
                            string name = _session.Translation.GetPokemonTranslation(pokemon.PokemonId);
                            name += "".PadLeft(16 - name.Length, ' ');
                            string source = pokemon.Source;
                            source += "".PadLeft(16 - source.Length, ' ');
                            Logger.Write(string.Format(" {0} |  {1}  |  {2:0.00}m  \t|  {3:mm} min {3:ss} sec  |  {4:00} min {5:00} sec  | {6}",
                                source,
                                name,
                                pokemon.Distance,
                                pokemon.ExpiredTime - DateTime.Now,
                                pokemon.EstimatedTime / 60,
                                pokemon.EstimatedTime % 60,
                                pokemon.ExpiredTime > DateTime.Now.AddSeconds(pokemon.EstimatedTime) ? "Possible" : "Missied"
                                ));
                        }
                    }
                }
            }
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static Task PriorityPokemon(ISession session, string id)
        {
            Logger.Write($"(Holmes) PriorityPokemon", LogLevel.Info, ConsoleColor.Red);
            return Task.Run(() =>
            {
                var pokemonItem = rarePokemons.FirstOrDefault(p => p.UniqueId == id);
                if (pokemonItem != null)
                {
                    pokemonItem.Setting.Priority = 0;//will be going to catch next check. TODO  add code to trigger catch now
                }
            });
        }

        public static Task<List<SnipePokemonInfo>> GetCurrentQueueItems(ISession session)
        {
            Logger.Write($"(Holmes) GetCurrentQueueItems", LogLevel.Info, ConsoleColor.Red);
            return Task.FromResult(rarePokemons);
        }

        public static Task TargetPokemonSnip(ISession session, string id)
        {
            Logger.Write($"(Holmes) TargetPokemonSnip", LogLevel.Info, ConsoleColor.Red);
            return Task.Run(() =>
            {
                var ele = rarePokemons.FirstOrDefault(p => p.UniqueId == id);
                if (ele != null)
                {
                    ele.Setting.Priority = 0;
                    rarePokemons = rarePokemons.OrderBy(p => p.Setting.Priority).ThenBy(p => p.Distance).ToList();
                    _session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                    {
                        Type = HumanWalkSnipeEventTypes.TargetedPokemon,
                        Pokemons = ApplyFilter(rarePokemons),
                    });
                }
                prioritySnipeFlag = true;
            });
        }

        public static double CalculateDistanceInMeters(double sourceLat, double sourceLng, double destinationLat, double destinationLng)
        {
            Logger.Write($"(Holmes) CalculateDistanceInMeters", LogLevel.Info, ConsoleColor.Red);
            var distance = LocationUtils.CalculateDistanceInMeters(sourceLat, sourceLng, destinationLat, destinationLng);
            if (distance > 2000)
                return 0;
            else
                //return _session.Navigation.WalkStrategy.CalculateDistance(sourceLat, sourceLng, destinationLat, destinationLng);
                return distance;
        }

        public static void UpdateCatchPokemon(double latitude, double longitude, PokemonId id)
        {
            Logger.Write($"(Holmes) UpdateCatchPokemon", LogLevel.Info, ConsoleColor.Red);
            bool exist = false;
            rarePokemons.ForEach((p) =>
            {
                if (LocationUtils.CalculateDistanceInMeters(latitude, longitude, p.Latitude, p.Longitude) < 30.0 &&
                        p.PokemonId == id &&
                        !p.IsVisited)
                {
                    p.IsVisited = true;
                    exist = true;
                    _session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                    {
                        UniqueId = p.UniqueId,
                        Type = HumanWalkSnipeEventTypes.EncounterSnipePokemon,
                        PokemonId = id,
                        Latitude = latitude,
                        Longitude = longitude,
                        Pokemons = ApplyFilter(rarePokemons),
                    });
                }
            });

            //in some case, we caught the pokemon before data refresh, we need add a fake pokemon to list to avoid it add back and waste time 
            if (!exist && pokemonToBeSnipedIds.Any(p => p == id))
            {
                rarePokemons.Add(new SnipePokemonInfo()
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Id = (int)id,
                    IsFake = true,
                    IsVisited = true,
                    ExpiredTime = DateTime.Now.AddMinutes(14),
                    Setting = new HumanWalkSnipeFilter(1, 1, 100, false, false, false, 0),//not being used. just fake to make code valid
                });
            }
        }

        public static Task RemovePokemonFromQueue(ISession session, string id)
        {
            Logger.Write($"(Holmes) RemovePokemonFromQueue", LogLevel.Info, ConsoleColor.Red);
            return Task.Run(() =>
            {
                var ele = rarePokemons.FirstOrDefault(p => p.UniqueId == id);
                if (ele != null)
                {
                    ele.IsVisited = true; //set pokemon to visited, then it won't appear on the list
                    rarePokemons = rarePokemons.OrderBy(p => p.Setting.Priority).ThenBy(p => p.Distance).ToList();
                    _session.EventDispatcher.Send(new HumanWalkSnipeEvent()
                    {
                        Type = HumanWalkSnipeEventTypes.QueueUpdated,
                        Pokemons = ApplyFilter(rarePokemons),
                    });
                }
            });

        }

    }
}
