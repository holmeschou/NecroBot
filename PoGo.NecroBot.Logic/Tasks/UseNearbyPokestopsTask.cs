﻿#region using directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeoCoordinatePortable;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Strategies.Walk;
using PoGo.NecroBot.Logic.Utils;
using PokemonGo.RocketAPI.Extensions;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;
using PoGo.NecroBot.Logic.Event.Gym;
using PoGo.NecroBot.Logic.Model;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public delegate void UpdateTimeStampsPokestopDelegate();

    public class UseNearbyPokestopsTask
    {
        private static int _stopsHit;
        private static int _randomStop;
        private static Random _rc; //initialize pokestop random cleanup counter first time
        private static int _storeRi;
        private static int _randomNumber;
        public static bool _pokestopLimitReached;
        public static bool _pokestopTimerReached;

        public static event UpdateTimeStampsPokestopDelegate UpdateTimeStampsPokestop;

        internal static void Initialize()
        {
            _stopsHit = 0;
            _randomStop = 0;
            _rc = new Random();
            _storeRi = _rc.Next(8, 15);
            _randomNumber = _rc.Next(4, 11);
            _pokestopLimitReached = false;
            _pokestopTimerReached = false;
        }

        private static bool SearchThresholdExceeds(ISession session)
        {
            if (!session.LogicSettings.UsePokeStopLimit) return false;
            if (_pokestopLimitReached || _pokestopTimerReached) return true;

            // Check if user defined max Pokestops reached
            if (!session.Stats.PokeStopTimestamps.Any()) return false;
            var timeDiff = (DateTime.Now - new DateTime(session.Stats.PokeStopTimestamps.First()));

            if (session.Stats.PokeStopTimestamps.Count >= session.LogicSettings.PokeStopLimit)
            {
                session.EventDispatcher.Send(new ErrorEvent
                {
                    Message = session.Translation.GetTranslation(TranslationString.PokestopLimitReached)
                });

                // Check Timestamps & delete older than 24h
                var TSminus24h = DateTime.Now.AddHours(-24).Ticks;
                for (int i = 0; i < session.Stats.PokeStopTimestamps.Count; i++)
                {
                    if (session.Stats.PokeStopTimestamps[i] < TSminus24h)
                    {
                        Logger.Write($"Removing stored Pokestop timestamp {session.Stats.PokeStopTimestamps[i]}", LogLevel.Info);
                        session.Stats.PokeStopTimestamps.Remove(session.Stats.PokeStopTimestamps[i]);
                    }
                }

                UpdateTimeStampsPokestop?.Invoke();
                _pokestopLimitReached = true;
                return true;
            }

            // Check if user defined time since start reached
            else if (timeDiff.TotalSeconds >= session.LogicSettings.PokeStopLimitMinutes * 60)
            {
                session.EventDispatcher.Send(new ErrorEvent
                {
                    Message = session.Translation.GetTranslation(TranslationString.PokestopTimerReached)
                });

                // Check Timestamps & delete older than 24h
                var TSminus24h = DateTime.Now.AddHours(-24).Ticks;
                for (int i = 0; i < session.Stats.PokeStopTimestamps.Count; i++)
                {
                    if (session.Stats.PokeStopTimestamps[i] < TSminus24h)
                    {
                        session.Stats.PokeStopTimestamps.Remove(session.Stats.PokeStopTimestamps[i]);
                    }
                }

                UpdateTimeStampsPokestop?.Invoke();
                _pokestopTimerReached = true;
                return true;
            }

            return false; // Continue running
        }

        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //request map objects to referesh data. keep all fort in session
            var pokeStops = await UpdateFortsData(session);

            var pokeStop = await GetNextPokeStop(session);
            while (pokeStop != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Exit this task if both catching and looting has reached its limits
                if ((UseNearbyPokestopsTask._pokestopLimitReached || UseNearbyPokestopsTask._pokestopTimerReached) &&
                    (CatchPokemonTask._catchPokemonLimitReached || CatchPokemonTask._catchPokemonTimerReached))
                    return;

					var fortInfo = pokeStop.Id == SetMoveToTargetTask.TARGET_ID ? SetMoveToTargetTask.FortInfo : await session.Client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);

                await WalkingToPokeStop(session, cancellationToken, pokeStop, fortInfo);

                await DoActionAtPokeStop(session, cancellationToken, pokeStop, fortInfo);

                await UseGymBattleTask.Execute(session, cancellationToken, pokeStop, fortInfo);

                if (session.LogicSettings.SnipeAtPokestops || session.LogicSettings.UseSnipeLocationServer)
                    await SnipePokemonTask.Execute(session, cancellationToken);
                
                if (!await SetMoveToTargetTask.IsReachedDestination(pokeStop, session, cancellationToken))
                {
                    pokeStop.CooldownCompleteTimestampMs = DateTime.UtcNow.ToUnixTime() + (pokeStop.Type == FortType.Gym ? session.LogicSettings.GymVisitTimeout : 5) * 60 * 1000; //5 minutes to cooldown
                    session.AddForts(new List<FortData>() { pokeStop }); //replace object in memory.
                }

                if (session.LogicSettings.EnableHumanWalkingSnipe)
                {
                    await HumanWalkSnipeTask.Execute(session, cancellationToken, pokeStop);
                }
                pokeStop = await GetNextPokeStop(session);
            }
        }

        private static async Task WalkingToPokeStop(ISession session, CancellationToken cancellationToken, FortData pokeStop, FortDetailsResponse fortInfo)
        {
            // we only move to the PokeStop, and send the associated FortTargetEvent, when not using GPX
            // also, GPX pathing uses its own EggWalker and calls the CatchPokemon tasks internally.
            if (!session.LogicSettings.UseGpxPathing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var eggWalker = new EggWalker(1000, session);

                var distance = session.Navigation.WalkStrategy.CalculateDistance(
                    session.Client.CurrentLatitude,
                    session.Client.CurrentLongitude,
                    pokeStop.Latitude,
                    pokeStop.Longitude,
                    session
                );

                var pokeStopDestination = new FortLocation(pokeStop.Latitude, pokeStop.Longitude,
                    LocationUtils.getElevation(session, pokeStop.Latitude, pokeStop.Longitude), pokeStop, fortInfo);

                await session.Navigation.Move(pokeStopDestination,
                    async () =>
                    {
                        if (session.LogicSettings.ActivateMSniper)
                        {
                            await MSniperServiceTask.Execute(session, cancellationToken);
                        }
                        await OnWalkingToPokeStopOrGym(session, pokeStop, cancellationToken);
                    },
                    session,
                    cancellationToken);

                // we have moved this distance, so apply it immediately to the egg walker.
                await eggWalker.ApplyDistance(distance, cancellationToken);
            }
        }

        private static async Task OnWalkingToPokeStopOrGym(ISession session, FortData pokeStop, CancellationToken cancellationToken)
        {
            // Catch normal map Pokemon
            await CatchNearbyPokemonsTask.Execute(session, cancellationToken);
            //Catch Incense Pokemon
            await CatchIncensePokemonsTask.Execute(session, cancellationToken);

            if (!session.LogicSettings.UseGpxPathing)
            {
                // Spin as long as we haven't reached the user defined limits
                if (!_pokestopLimitReached && !_pokestopTimerReached)
                {
                    await SpinPokestopNearBy(session, cancellationToken, pokeStop);
                }
            }
        }

        private static async Task<FortData> GetNextPokeStop(ISession session)
        {
            var priorityTarget = await SetMoveToTargetTask.GetTarget(session);
            if (priorityTarget != null) return priorityTarget;

            if (session.Forts == null ||
                session.Forts.Count == 0 ||
                session.Forts.Count(p => p.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime()) == 0)
            {
                return null;
            };

            var pokeStopes = session.Forts.Where(p => p.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime()).ToList();
            pokeStopes = pokeStopes.OrderBy(
                        p =>
                            //session.Navigation.WalkStrategy.CalculateDistance(
                            LocationUtils.CalculateDistanceInMeters(
                                session.Client.CurrentLatitude,
                                session.Client.CurrentLongitude,
                                p.Latitude,
                                p.Longitude)
                                ).ToList();

            var idxNearestPokeStop = 0;
            var NearestDistance = 0.0;
            Logger.Write($"(Holmes) pokeStopes.Count = {pokeStopes.Count}", LogLevel.Info, ConsoleColor.Yellow);
            for (var i = 0; i < Math.Min(3, pokeStopes.Count); i++)
            {
                var CurrentDistanceSt = LocationUtils.CalculateDistanceInMeters(
                        session.Client.CurrentLatitude,
                        session.Client.CurrentLongitude,
                        pokeStopes[i].Latitude,
                        pokeStopes[i].Longitude);

                var CurrentDistance = session.Navigation.WalkStrategy.CalculateDistance(
                        session.Client.CurrentLatitude,
                        session.Client.CurrentLongitude,
                        pokeStopes[i].Latitude,
                        pokeStopes[i].Longitude,
                        session);
                if (i == 0)
                    NearestDistance = CurrentDistance;

                if (CurrentDistance < NearestDistance)
                {
                    NearestDistance = CurrentDistance;
                    idxNearestPokeStop = i;
                }
                Logger.Write($"(Holmes) currST = {CurrentDistanceSt}, curr = {CurrentDistance}, nearest = {NearestDistance}", LogLevel.Info, ConsoleColor.Yellow);
            }
            Logger.Write($"(Holmes) idxNearestPokeStop = {idxNearestPokeStop}", LogLevel.Info, ConsoleColor.Yellow);

            if (session.LogicSettings.UseGpxPathing)
            {
                pokeStopes = pokeStopes.Where(p => LocationUtils.CalculateDistanceInMeters(p.Latitude, p.Longitude, session.Client.CurrentLatitude, session.Client.CurrentLongitude) < 40).ToList();
            }

            session.EventDispatcher.Send(new PokeStopListEvent { Forts = session.Forts });

            //if (pokeStopes.Count == 1) return pokeStopes.FirstOrDefault();

            if (session.LogicSettings.GymAllowed && session.Inventory.GetPlayerStats().Result.FirstOrDefault().Level > 5)
            {
                var gyms = pokeStopes.Where(x =>
                    x.Type == FortType.Gym &&
                    LocationUtils.CalculateDistanceInMeters(x.Latitude, x.Longitude, session.Client.CurrentLatitude, session.Client.CurrentLongitude) < session.LogicSettings.GymMaxDistance &&
                    x.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime()
                );

                //TODO: Why Gym has higher priority?
                if (gyms.Count() > 0) return gyms.FirstOrDefault();
            }

            //return pokeStopes.Skip((int)DateTime.Now.Ticks % 2).FirstOrDefault();
            return pokeStopes.Skip(idxNearestPokeStop).FirstOrDefault();
        }

        public static async Task SpinPokestopNearBy(ISession session, CancellationToken cancellationToken, FortData destinationFort = null)
        {
            var allForts = session.Forts.Where(p => p.Type == FortType.Checkpoint).ToList();

            if (allForts.Count > 1)
            {
                var spinablePokestops = allForts.Where(
                    i =>
                        (
                            LocationUtils.CalculateDistanceInMeters(
                                session.Client.CurrentLatitude, session.Client.CurrentLongitude, i.Latitude, i.Longitude) < 40 &&
                                i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime() &&
                                (destinationFort == null || destinationFort.Id != i.Id))
                ).ToList();

                List<FortData> spinedPokeStops = new List<FortData>();
                if (spinablePokestops.Count >= 1)
                {
                    foreach (var pokeStop in spinablePokestops)
                    {
                        var fortInfo = await session.Client.Fort.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                        await FarmPokestop(session, pokeStop, fortInfo, cancellationToken, true);
                        pokeStop.CooldownCompleteTimestampMs = DateTime.UtcNow.ToUnixTime() + 5 * 60 * 1000;
                        spinedPokeStops.Add(pokeStop);
                        if (spinablePokestops.Count > 1)
                        {
                            await Task.Delay(1000);
                        }
                    }
                }
                session.AddForts(spinablePokestops);
            }
        }

        private static async Task DoActionAtPokeStop(ISession session, CancellationToken cancellationToken, FortData pokeStop, FortDetailsResponse fortInfo, bool doNotTrySpin = false)
        {
            if (pokeStop.Type != FortType.Checkpoint ) return;

            //Catch Lure Pokemon
            if (pokeStop.LureInfo != null)
            {
                // added for cooldowns
                await Task.Delay(Math.Min(session.LogicSettings.DelayBetweenPlayerActions, 3000)); //TODO: Why choice minimal? 
                await CatchLurePokemonsTask.Execute(session, pokeStop, cancellationToken);
            }

            // Spin as long as we haven't reached the user defined limits
            if (!_pokestopLimitReached && !_pokestopTimerReached)
            {
                await FarmPokestop(session, pokeStop, fortInfo, cancellationToken, doNotTrySpin);
            }

            if (++_stopsHit >= _storeRi) //TODO: OR item/pokemon bag is full //check stopsHit against storeRI random without dividing.
            {
                _storeRi = _rc.Next(6, 12); //set new storeRI for new random value
                _stopsHit = 0;

                if (session.LogicSettings.UseNearActionRandom)
                {
                    await HumanRandomActionTask.Execute(session, cancellationToken);
                }
                else
                {
                    await RecycleItemsTask.Execute(session, cancellationToken);

                    if (session.LogicSettings.EvolveAllPokemonWithEnoughCandy ||
                        session.LogicSettings.EvolveAllPokemonAboveIv ||
                        session.LogicSettings.UseLuckyEggsWhileEvolving ||
                        session.LogicSettings.KeepPokemonsThatCanEvolve)
                        await EvolvePokemonTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.UseLuckyEggConstantly)
                        await UseLuckyEggConstantlyTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.UseIncenseConstantly)
                        await UseIncenseConstantlyTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.TransferDuplicatePokemon)
                        await TransferDuplicatePokemonTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.TransferWeakPokemon)
                        await TransferWeakPokemonTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.RenamePokemon)
                        await RenamePokemonTask.Execute(session, cancellationToken);
                    if (session.LogicSettings.AutomaticallyLevelUpPokemon)
                        await LevelUpPokemonTask.Execute(session, cancellationToken);

                    await GetPokeDexCount.Execute(session, cancellationToken);
                }
            }
        }

        private static async Task FarmPokestop(ISession session, FortData pokeStop, FortDetailsResponse fortInfo, CancellationToken cancellationToken, bool doNotRetry = false)
        {
            // If the cooldown is in the future than don't farm the pokestop.
            if (pokeStop.CooldownCompleteTimestampMs > DateTime.UtcNow.ToUnixTime())
                return;

            FortSearchResponse fortSearch;
            var timesZeroXPawarded = 0;
            var fortTry = 0; //Current check
            const int retryNumber = 50; //How many times it needs to check to clear softban
            const int zeroCheck = 5; //How many times it checks fort before it thinks it's softban
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (SearchThresholdExceeds(session))
                {
                    break;
                }

                fortSearch = await session.Client.Fort.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                if (fortSearch.ExperienceAwarded > 0 && timesZeroXPawarded > 0) timesZeroXPawarded = 0;
                if (fortSearch.ExperienceAwarded == 0)
                {
                    timesZeroXPawarded++;

                    if (timesZeroXPawarded > zeroCheck)
                    {
                        if ((int)fortSearch.CooldownCompleteTimestampMs != 0)
                        {
                            break; // Check if successfully looted, if so program can continue as this was "false alarm".
                        }

                        fortTry += 1;

                        session.EventDispatcher.Send(new FortFailedEvent
                        {
                            Name = fortInfo.Name,
                            Try = fortTry,
                            Max = retryNumber - zeroCheck,
                            Looted = false
                        });
                        if (doNotRetry)
                        {
                            break;
                        }
                        if (!session.LogicSettings.FastSoftBanBypass)
                        {
                            DelayingUtils.Delay(session.LogicSettings.DelayBetweenPlayerActions, 0);
                        }
                    }
                }
                else
                {
                    if (fortTry != 0)
                    {
                        session.EventDispatcher.Send(new FortFailedEvent
                        {
                            Name = fortInfo.Name,
                            Try = fortTry + 1,
                            Max = retryNumber - zeroCheck,
                            Looted = true
                        });
                    }

                    session.EventDispatcher.Send(new FortUsedEvent
                    {
                        Id = pokeStop.Id,
                        Name = fortInfo.Name,
                        Exp = fortSearch.ExperienceAwarded,
                        Gems = fortSearch.GemsAwarded,
                        Items = StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded),
                        Latitude = pokeStop.Latitude,
                        Longitude = pokeStop.Longitude,
                        Altitude = session.Client.CurrentAltitude,
                        InventoryFull = fortSearch.Result == FortSearchResponse.Types.Result.InventoryFull
                    });

                    if (fortSearch.Result == FortSearchResponse.Types.Result.InventoryFull)
                        _storeRi = 1;

                    if (session.LogicSettings.UsePokeStopLimit)
                    {
                        session.Stats.PokeStopTimestamps.Add(DateTime.Now.Ticks);
                        UpdateTimeStampsPokestop?.Invoke();
                        Logger.Write($"(POKESTOP LIMIT) {session.Stats.PokeStopTimestamps.Count}/{session.LogicSettings.PokeStopLimit}",
                            LogLevel.Info, ConsoleColor.Yellow);
                    }
                    break; //Continue with program as loot was succesfull.
                }
            } while (fortTry < retryNumber - zeroCheck);
            //Stop trying if softban is cleaned earlier or if 40 times fort looting failed.

            if (session.LogicSettings.RandomlyPauseAtStops && !doNotRetry)
            {
                if (++_randomStop >= _randomNumber)
                {
                    _randomNumber = _rc.Next(4, 11);
                    _randomStop = 0;
                    int randomWaitTime = _rc.Next(30, 120);
                    await Task.Delay(randomWaitTime, cancellationToken);
                }
            }

        }

        public static async Task<List<FortData>> UpdateFortsData(ISession session)
        {
            var mapObjects = await session.Client.Map.GetMapObjects();
            var pokeStops = mapObjects.Item1.MapCells.SelectMany(i => i.Forts)
                .Where(
                    i =>
                        (i.Type == FortType.Checkpoint || i.Type == FortType.Gym) &&
                        (
                            LocationUtils.CalculateDistanceInMeters(
                                session.Settings.DefaultLatitude, session.Settings.DefaultLongitude,
                                i.Latitude, i.Longitude) < session.LogicSettings.MaxTravelDistanceInMeters) ||
                        session.LogicSettings.MaxTravelDistanceInMeters == 0
                );

            //session.Forts.Clear();
            session.AddForts(pokeStops.ToList());

            if (!session.LogicSettings.UseGpxPathing)
            {
                if (pokeStops.ToList().Count <= 0)
                {
                    // only send this for non GPX because otherwise we generate false positives
                    session.EventDispatcher.Send(new WarnEvent
                    {
                        Message = session.Translation.GetTranslation(TranslationString.FarmPokestopsNoUsableFound)
                    });
                }
                
                session.EventDispatcher.Send(new PokeStopListEvent { Forts = pokeStops.ToList() });
            }

            return pokeStops.ToList();
        }
    }
}