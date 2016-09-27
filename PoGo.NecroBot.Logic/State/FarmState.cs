#region using directives

using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Tasks;

#endregion

namespace PoGo.NecroBot.Logic.State
{
    public class FarmState : IState
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Await.Warning", "CS4014:Await.Warning")]

        public async Task<IState> Execute(ISession session, CancellationToken cancellationToken)
        {
            if (session.GlobalSettings.PlayerConfig.UseNearActionRandom)
            {
                await HumanRandomActionTask.Execute(session, cancellationToken);
            }
            else
            {
                if (session.GlobalSettings.PokemonConfig.EvolveAllPokemonAboveIv || session.GlobalSettings.PokemonConfig.EvolveAllPokemonWithEnoughCandy
                   || session.GlobalSettings.PokemonConfig.UseLuckyEggsWhileEvolving || session.GlobalSettings.PokemonConfig.KeepPokemonsThatCanEvolve)
                    await EvolvePokemonTask.Execute(session, cancellationToken);
                if (session.GlobalSettings.PokemonConfig.UseEggIncubators)
                    await UseIncubatorsTask.Execute(session, cancellationToken);
                if (session.GlobalSettings.PokemonConfig.TransferDuplicatePokemon)
                    await TransferDuplicatePokemonTask.Execute(session, cancellationToken);
                if (session.GlobalSettings.PokemonConfig.UseLuckyEggConstantly)
                    await UseLuckyEggConstantlyTask.Execute(session, cancellationToken);
                if (session.GlobalSettings.PokemonConfig.UseIncenseConstantly)
                    await UseIncenseConstantlyTask.Execute(session, cancellationToken);

                await GetPokeDexCount.Execute(session, cancellationToken);

                if (session.GlobalSettings.PokemonConfig.RenamePokemon)
                    await RenamePokemonTask.Execute(session, cancellationToken);

                await RecycleItemsTask.Execute(session, cancellationToken);

                if (session.GlobalSettings.PokemonConfig.AutomaticallyLevelUpPokemon)
                    await LevelUpPokemonTask.Execute(session, cancellationToken);
            }

            if (session.GlobalSettings.GPXConfig.UseGpxPathing)
                await FarmPokestopsGpxTask.Execute(session, cancellationToken);
            else
                await FarmPokestopsTask.Execute(session, cancellationToken);

            return this;
        }
    }
}
