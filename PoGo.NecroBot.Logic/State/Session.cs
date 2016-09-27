#region using directives
using System.Linq;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Model.Settings;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Responses;
using System.Collections.Generic;
using POGOProtos.Map.Fort;
using PoGo.NecroBot.Logic.Model;
using System.Threading.Tasks;

#endregion

namespace PoGo.NecroBot.Logic.State
{
    public interface ISession
    {
        ISettings Settings { get; set; }
        GlobalSettings GlobalSettings { get; }
        ITranslation Translation { get; }
        Client Client { get; }
        GetPlayerResponse Profile { get; set; }
        Inventory Inventory { get; }
        Navigation Navigation { get; }
        IEventDispatcher EventDispatcher { get; }
        List<FortData> Forts { get; set; }
        SessionStats Stats { get; }
        List<BotActions> Actions { get; }

        void AddForts(List<FortData> mapObjects);
        Task<bool> WaitUntilActionAccept(BotActions action, int timeout = 30000);
    }

    public class Session : ISession
    {
        public Session(ISettings settings, GlobalSettings globalSettings, ITranslation translation)
        {
            Settings = settings;
            GlobalSettings = globalSettings;
            Translation = translation;

            Client = new Client(Settings, new ApiFailureStrategy(this));
            Inventory = new Inventory(Client, GlobalSettings);
            Navigation = new Navigation(Client, GlobalSettings);

            EventDispatcher = new EventDispatcher();

            Forts = new List<FortData>();

            Stats = new SessionStats();
        }

        public ISettings Settings { get; set; }

        public GlobalSettings GlobalSettings { get; set; }

        public ITranslation Translation { get; }

        public Client Client { get; private set; }

        public GetPlayerResponse Profile { get; set; }

        public Inventory Inventory { get; private set; }

        public Navigation Navigation { get; private set; }

        public IEventDispatcher EventDispatcher { get; }

        public List<FortData> Forts { get; set; }

        public SessionStats Stats { get; set; }

        public List<BotActions> Actions { get { return this.botActions; } }

        private List<BotActions> botActions = new List<BotActions>();

        public void AddForts(List<FortData> data)
        {
            Forts.RemoveAll(p => data.Any(x =>
                x.Id == p.Id &&
                (x.Type == FortType.Checkpoint || x.Type == FortType.Gym)
            ));
            Forts.AddRange(data.Where(x => (x.Type == FortType.Checkpoint || x.Type == FortType.Gym)));
        }

        public async Task<bool> WaitUntilActionAccept(BotActions action, int timeout = 30000)
        {
            if (botActions.Count == 0) return true;
            var waitTimes = 0;
            while (waitTimes < timeout)
            {
                if (botActions.Count == 0) return true;
                ///implement logic of action dependent
                waitTimes += 1000;
                await Task.Delay(1000);
            }
            return false; //timedout
        }
    }
}