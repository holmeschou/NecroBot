#region using directives

using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Responses;
using PoGo.NecroBot.Logic.Utils;
using System.Linq;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public static class CatchLurePokemonsTask
    {
        public static async Task Execute(ISession session, FortData currentFortData, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!session.GlobalSettings.PokemonConfig.CatchPokemon) return;

            Logger.Write(session.Translation.GetTranslation(TranslationString.LookingForLurePokemon), LogLevel.Debug);

            var fortId = currentFortData.Id;
            var pokemonId = currentFortData.LureInfo.ActivePokemonId;
			
            if( ( session.GlobalSettings.PokemonConfig.UsePokemonSniperFilterOnly && !session.GlobalSettings.PokemonToSnipe.Pokemon.Contains( pokemonId ) ) ||
                    ( session.GlobalSettings.PokemonConfig.UsePokemonToNotCatchFilter && session.GlobalSettings.PokemonsToIgnore.Contains( pokemonId ) ) )
            {
                session.EventDispatcher.Send(new NoticeEvent
                {
                    Message = session.Translation.GetTranslation(TranslationString.PokemonSkipped, pokemonId)
                });
            }
            else
            {
                var encounterId = currentFortData.LureInfo.EncounterId;
                var encounter = await session.Client.Encounter.EncounterLurePokemon(encounterId, fortId);

                if (encounter.Result == DiskEncounterResponse.Types.Result.Success && session.GlobalSettings.PokemonConfig.CatchPokemon)
                {
					var pokemon = new MapPokemon
					{
						EncounterId = encounterId,
						ExpirationTimestampMs = currentFortData.LureInfo.LureExpiresTimestampMs,
						Latitude = currentFortData.Latitude,
						Longitude = currentFortData.Longitude,
						PokemonId = currentFortData.LureInfo.ActivePokemonId,
						SpawnPointId = currentFortData.Id
					};

                    // Catch the Pokemon
                    await CatchPokemonTask.Execute(session, cancellationToken, encounter, pokemon, 
                        currentFortData, sessionAllowTransfer: true);

                }
                else if (encounter.Result == DiskEncounterResponse.Types.Result.PokemonInventoryFull)
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
                    if (encounter.Result.ToString().Contains("NotAvailable")) return;
                    session.EventDispatcher.Send(new WarnEvent
                    {
                        Message =
                            session.Translation.GetTranslation(TranslationString.EncounterProblemLurePokemon,
                                encounter.Result)
                    });
                }
            }
        }

        public static async Task Execute(ISession session, CancellationToken cancellationToken)     
        {
            // Looking for any lure pokestop neaby

            var mapObjects = await session.Client.Map.GetMapObjects();
            if (session.GlobalSettings.HumanWalkSnipeConfig.Enable)
            {
                var pokeStops = mapObjects.Item1.MapCells.SelectMany(i => i.Forts)
                    .Where(
                        i =>
                            (i.Type == FortType.Checkpoint || i.Type == FortType.Gym)
                    );
                session.AddForts(pokeStops.ToList());
                session.EventDispatcher.Send(new PokeStopListEvent { Forts = pokeStops.ToList() });
            }

            var forts = session.Forts.Where(p => p.Type == FortType.Checkpoint);
            foreach (FortData fort in forts)
            {
                var distance =  LocationUtils.CalculateDistanceInMeters(session.Client.CurrentLatitude, session.Client.CurrentLongitude, fort.Latitude, fort.Longitude);
                if(distance < 40 && fort.LureInfo != null)
                {
                    await Execute(session, fort, cancellationToken);
                }
            };            
        }
    }
}
