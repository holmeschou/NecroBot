﻿#region using directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.PoGoUtils;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Data;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public class TransferWeakPokemonTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.GlobalSettings.PokemonConfig.AutoFavoritePokemon)
                await FavoritePokemonTask.Execute(session, cancellationToken);

            await session.Inventory.RefreshCachedInventory();
            var pokemons = await session.Inventory.GetPokemons();
            var pokemonDatas = pokemons as IList<PokemonData> ?? pokemons.ToList();
            var pokemonsFiltered =
                pokemonDatas.Where(pokemon => !session.GlobalSettings.PokemonsNotToTransfer.Contains(pokemon.PokemonId))
                    .ToList().OrderBy( poke => poke.Cp );

            if (session.GlobalSettings.PokemonConfig.KeepPokemonsThatCanEvolve)
                pokemonsFiltered =
                    pokemonDatas.Where(pokemon => !session.GlobalSettings.PokemonsToEvolve.Contains(pokemon.PokemonId))
                        .ToList().OrderBy( poke => poke.Cp );

            var orderedPokemon = pokemonsFiltered.OrderBy( poke => poke.Cp );

            foreach (var pokemon in orderedPokemon )
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((pokemon.Cp >= session.GlobalSettings.PokemonConfig.KeepMinCp) ||
                    (PokemonInfo.CalculatePokemonPerfection(pokemon) >= session.GlobalSettings.PokemonConfig.KeepMinIvPercentage &&
                     session.GlobalSettings.PokemonConfig.PrioritizeIvOverCp) ||
                     (PokemonInfo.GetLevel(pokemon) >= session.GlobalSettings.PokemonConfig.KeepMinLvl && session.GlobalSettings.PokemonConfig.UseKeepMinLvl) ||
                    pokemon.Favorite == 1)
                    continue;

                await session.Client.Inventory.TransferPokemon(pokemon.Id);
                await session.Inventory.DeletePokemonFromInvById(pokemon.Id);
                var bestPokemonOfType = (session.GlobalSettings.PokemonConfig.PrioritizeIvOverCp
                    ? await session.Inventory.GetHighestPokemonOfTypeByIv(pokemon)
                    : await session.Inventory.GetHighestPokemonOfTypeByCp(pokemon)) ?? pokemon;

                var setting = session.Inventory.GetPokemonSettings()
                    .Result.Single(q => q.PokemonId == pokemon.PokemonId);
                var family = session.Inventory.GetPokemonFamilies().Result.First(q => q.FamilyId == setting.FamilyId);

                family.Candy_++;

                session.EventDispatcher.Send(new TransferPokemonEvent
                {
                    Id = pokemon.PokemonId,
                    Perfection = PokemonInfo.CalculatePokemonPerfection(pokemon),
                    Cp = pokemon.Cp,
                    BestCp = bestPokemonOfType.Cp,
                    BestPerfection = PokemonInfo.CalculatePokemonPerfection(bestPokemonOfType),
                    FamilyCandies = family.Candy_
                });

                DelayingUtils.Delay(session.GlobalSettings.PlayerConfig.TransferActionDelay, 0);
            }
        }
    }
}