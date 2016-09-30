#region using directives

using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Networking.Responses;

#endregion

namespace PoGo.NecroBot.Logic
{
    public class StatisticsAggregator
    {
        private readonly Statistics _stats;

        public StatisticsAggregator(Statistics stats)
        {
            _stats = stats;
        }

        public void Listen(IEvent evt, ISession session)
        {
            dynamic eve = evt;

            try
            {
                HandleEvent(eve, session);
            }
            catch
            {
            }
        }

        private void HandleEvent(string evt, ISession session) { }
        
        private void HandleEvent(UseLuckyEggEvent event1, ISession session) { }

        private void HandleEvent(UpdateEvent evt, ISession session) { }

        private void HandleEvent(UpdatePositionEvent evt, ISession session) { }

        private void HandleEvent(EggIncubatorStatusEvent evt, ISession session) { }

        private void HandleEvent(ProfileEvent evt, ISession session)
        {
            _stats.SetUsername(evt.Profile);
            _stats.Dirty(session.Inventory);
        }

        private void HandleEvent(SnipeModeEvent evt, ISession session) { }

        private void HandleEvent(ErrorEvent evt, ISession session) { }

        private void HandleEvent(SnipeScanEvent evt, ISession session) { }

        private void HandleEvent(NoticeEvent evt, ISession session) { }

        private void HandleEvent(WarnEvent evt, ISession session) { }

        private void HandleEvent(PokemonEvolveEvent evt, ISession session)
        {
            _stats.TotalExperience += evt.Exp;
            _stats.Dirty(session.Inventory);
        }

        private void HandleEvent(TransferPokemonEvent evt, ISession session)
        {
            _stats.TotalPokemonTransferred++;
            _stats.Dirty(session.Inventory);
        }

        private void HandleEvent(ItemRecycledEvent evt, ISession session)
        {
            _stats.TotalItemsRemoved++;
            _stats.Dirty(session.Inventory);
        }

        private void HandleEvent(FortUsedEvent evt, ISession session)
        {
            _stats.TotalExperience += evt.Exp;
            _stats.Dirty(session.Inventory);
        }

        private void HandleEvent(FortTargetEvent evt, ISession session) { }

        private void HandleEvent(PokemonCaptureEvent evt, ISession session)
        {
            if (evt.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
            {
                _stats.TotalExperience += evt.Exp;
                _stats.TotalPokemons++;
                _stats.TotalStardust = evt.Stardust;
                _stats.Dirty(session.Inventory);
            }
        }

        private void HandleEvent(NoPokeballEvent evt, ISession session) { }

        private void HandleEvent(DisplayHighestsPokemonEvent evt, ISession session) { }

        private void HandleEvent(UseBerryEvent evt, ISession session) { }

        private void HandleEvent(PokeStopListEvent event1, ISession session) { }
        
        private void HandleEvent(EggHatchedEvent event1, ISession session) { }

        private void HandleEvent(EggsListEvent event1, ISession session) { }

        private void HandleEvent(EvolveCountEvent event1, ISession session) { }

        private void HandleEvent(FortFailedEvent event1, ISession session) { }

        private void HandleEvent(PokemonListEvent event1, ISession session) { }

        private void HandleEvent(SnipeEvent event1, ISession session) { }
    }
}