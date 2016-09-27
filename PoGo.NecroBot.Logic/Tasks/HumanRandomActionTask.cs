using PoGo.NecroBot.Logic.State;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.NecroBot.Logic.Tasks
{
    public class HumanRandomActionTask
    {
        private static Random ActionRandom = new Random();

        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var randomCommand = Enumerable.Range(1, 9).OrderBy(x => ActionRandom.Next()).Take(9).ToList();
            for (int i = 0; i < 9; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (randomCommand[i])
                {
                    case 1:
                        if (session.GlobalSettings.PokemonConfig.EvolveAllPokemonAboveIv || session.GlobalSettings.PokemonConfig.EvolveAllPokemonWithEnoughCandy
                            || session.GlobalSettings.PokemonConfig.UseLuckyEggsWhileEvolving || session.GlobalSettings.PokemonConfig.KeepPokemonsThatCanEvolve)
                            if (ActionRandom.Next(1, 10) > 4)
                                await EvolvePokemonTask.Execute(session, cancellationToken);
                        break;
                    case 2:
                        if (session.GlobalSettings.PokemonConfig.UseEggIncubators)
                            if (ActionRandom.Next(1, 10) > 4)
                                await UseIncubatorsTask.Execute(session, cancellationToken);
                        break;
                    case 3:
                        if (session.GlobalSettings.PokemonConfig.TransferDuplicatePokemon)
                            if (ActionRandom.Next(1, 10) > 4)
                                await TransferDuplicatePokemonTask.Execute(session, cancellationToken);
                        break;
                    case 4:
                        if (session.GlobalSettings.PokemonConfig.UseLuckyEggConstantly)
                            if (ActionRandom.Next(1, 10) > 4)
                                await UseLuckyEggConstantlyTask.Execute(session, cancellationToken);
                        break;
                    case 5:
                        if (session.GlobalSettings.PokemonConfig.UseIncenseConstantly)
                            if (ActionRandom.Next(1, 10) > 4)
                                await UseIncenseConstantlyTask.Execute(session, cancellationToken);
                        break;
                    case 6:
                        if (session.GlobalSettings.PokemonConfig.RenamePokemon)
                            if (ActionRandom.Next(1, 10) > 4)
                                await RenamePokemonTask.Execute(session, cancellationToken);
                        break;
                    case 7:
                        if (session.GlobalSettings.PokemonConfig.AutoFavoritePokemon)
                            if (ActionRandom.Next(1, 10) > 4)
                                await FavoritePokemonTask.Execute(session, cancellationToken);
                        break;
                    case 8:
                        if (ActionRandom.Next(1, 10) > 4)
                            await RecycleItemsTask.Execute(session, cancellationToken);
                        break;
                    case 9:
                        if (session.GlobalSettings.PokemonConfig.AutomaticallyLevelUpPokemon)
                            if (ActionRandom.Next(1, 10) > 4)
                                await LevelUpPokemonTask.Execute(session, cancellationToken);
                        break;
                }
            }

            await GetPokeDexCount.Execute(session, cancellationToken);
        }

        public static async Task TransferRandom(ISession session, CancellationToken cancellationToken)
        {
            if (ActionRandom.Next(1, 10) > 4)
                await TransferDuplicatePokemonTask.Execute(session, cancellationToken);
        }
    }
}
