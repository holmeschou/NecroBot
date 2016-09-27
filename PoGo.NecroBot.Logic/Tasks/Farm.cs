#region using directives

using System.Threading;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;

#endregion

namespace PoGo.NecroBot.Logic.Service
{
    public interface IFarm
    {
        void Run(CancellationToken cancellationToken);
    }

    public class Farm : IFarm
    {
        private readonly ISession _session;

        public Farm(ISession session)
        {
            _session = session;
        }

        public void Run(CancellationToken cancellationToken)
        {
            if (_session.GlobalSettings.PlayerConfig.UseNearActionRandom)
            {
                HumanRandomActionTask.Execute(_session, cancellationToken).Wait(cancellationToken);
            }
            else
            {
                if (_session.GlobalSettings.PokemonConfig.EvolveAllPokemonAboveIv || _session.GlobalSettings.PokemonConfig.EvolveAllPokemonWithEnoughCandy
                    || _session.GlobalSettings.PokemonConfig.UseLuckyEggsWhileEvolving || _session.GlobalSettings.PokemonConfig.KeepPokemonsThatCanEvolve)
                    EvolvePokemonTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.AutomaticallyLevelUpPokemon)
                    LevelUpPokemonTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.UseLuckyEggConstantly)
                    UseLuckyEggConstantlyTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.UseIncenseConstantly)
                    UseIncenseConstantlyTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.TransferDuplicatePokemon)
                    TransferDuplicatePokemonTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.TransferWeakPokemon)
                    TransferWeakPokemonTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                if (_session.GlobalSettings.PokemonConfig.RenamePokemon)
                    RenamePokemonTask.Execute(_session, cancellationToken).Wait(cancellationToken);

                RecycleItemsTask.Execute(_session, cancellationToken).Wait(cancellationToken);
                GetPokeDexCount.Execute(_session, cancellationToken).Wait(cancellationToken);

                if (_session.GlobalSettings.PokemonConfig.UseEggIncubators)
                    UseIncubatorsTask.Execute(_session, cancellationToken).Wait(cancellationToken);
            }

            if (_session.GlobalSettings.GPXConfig.UseGpxPathing)
                FarmPokestopsGpxTask.Execute(_session, cancellationToken).Wait(cancellationToken);
            else
                FarmPokestopsTask.Execute(_session, cancellationToken).Wait(cancellationToken);
        }
    }
}