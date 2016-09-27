#region using directives

using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Responses;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public static class CatchIncensePokemonsTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!session.GlobalSettings.PokemonConfig.CatchPokemon) return;
            
            Logger.Write(session.Translation.GetTranslation(TranslationString.LookingForIncensePokemon), LogLevel.Debug);

            var incensePokemon = await session.Client.Map.GetIncensePokemons();

            if (incensePokemon.Result == GetIncensePokemonResponse.Types.Result.IncenseEncounterAvailable)
            {
                var pokemon = new MapPokemon
                {
                    EncounterId = incensePokemon.EncounterId,
                    ExpirationTimestampMs = incensePokemon.DisappearTimestampMs,
                    Latitude = incensePokemon.Latitude,
                    Longitude = incensePokemon.Longitude,
                    PokemonId = incensePokemon.PokemonId,
                    SpawnPointId = incensePokemon.EncounterLocation
                };

                if( ( session.GlobalSettings.PokemonConfig.UsePokemonSniperFilterOnly && !session.GlobalSettings.PokemonToSnipe.Pokemon.Contains( pokemon.PokemonId ) ) ||
                    ( session.GlobalSettings.PokemonConfig.UsePokemonToNotCatchFilter && session.GlobalSettings.PokemonsToIgnore.Contains( pokemon.PokemonId ) ) )
                {
                    Logger.Write(session.Translation.GetTranslation(TranslationString.PokemonIgnoreFilter,
                        session.Translation.GetPokemonTranslation(pokemon.PokemonId)));
                }
                else
                {
                    var distance = LocationUtils.CalculateDistanceInMeters(session.Client.CurrentLatitude,
                        session.Client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);
                    await Task.Delay(distance > 100 ? 500 : 100, cancellationToken);

                    var encounter =
                        await
                            session.Client.Encounter.EncounterIncensePokemon((ulong) pokemon.EncounterId,
                                pokemon.SpawnPointId);

                    if (encounter.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess && session.GlobalSettings.PokemonConfig.CatchPokemon)
                    {

                        //await CatchPokemonTask.Execute(session, cancellationToken, encounter, pokemon);
                        await CatchPokemonTask.Execute(session, cancellationToken, encounter, pokemon, 
                            currentFortData: null, sessionAllowTransfer: true);
                    }
                    else if (encounter.Result == IncenseEncounterResponse.Types.Result.PokemonInventoryFull)
                    {
						if (session.GlobalSettings.PokemonConfig.TransferDuplicatePokemon || session.GlobalSettings.PokemonConfig.TransferWeakPokemon)
						{
							session.EventDispatcher.Send(new WarnEvent
							{
								Message = session.Translation.GetTranslation(TranslationString.InvFullTransferring)
							});
							if(session.GlobalSettings.PokemonConfig.TransferDuplicatePokemon)
								await TransferDuplicatePokemonTask.Execute(session, cancellationToken);
							if(session.GlobalSettings.PokemonConfig.TransferWeakPokemon)
								await TransferWeakPokemonTask.Execute(session, cancellationToken);
						}
                        else
                            session.EventDispatcher.Send(new WarnEvent
                            {
                                Message = session.Translation.GetTranslation(TranslationString.InvFullTransferManually)
                            });
                    }
                    else
                    {
                        session.EventDispatcher.Send(new WarnEvent
                        {
                            Message =
                                session.Translation.GetTranslation(TranslationString.EncounterProblem, encounter.Result)
                        });
                    }
                }
            }
        }
    }
}
