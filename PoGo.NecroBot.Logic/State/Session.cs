#region using directives
using System.Linq;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces.Configuration;
using PoGo.NecroBot.Logic.Model.Settings;
using PoGo.NecroBot.Logic.Service;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Responses;
using PoGo.NecroBot.Logic.Service.Elevation;
using System.Collections.Generic;
using POGOProtos.Map.Fort;
using System;
using PokemonGo.RocketAPI.Extensions;
using PoGo.NecroBot.Logic.Model;
using System.Threading.Tasks;

#endregion

namespace PoGo.NecroBot.Logic.State
{
    public interface ISession
    {
        ISettings Settings { get; set; }
        Inventory Inventory { get; }
        Client Client { get; }
        GetPlayerResponse Profile { get; set; }
        Navigation Navigation { get; }
        ILogicSettings LogicSettings { get; }
        ITranslation Translation { get; }
        IEventDispatcher EventDispatcher { get; }
        TelegramService Telegram { get; set; }
        SessionStats Stats { get; }
        ElevationService ElevationService { get; }
        List<FortData> Forts { get; set; }
        void AddForts(List<FortData> mapObjects);
        Task<bool> WaitUntilActionAccept(BotActions action, int timeout = 30000);
        List<BotActions> Actions { get; }
    }


    public class Session : ISession
    {
        public Session(ISettings settings, ILogicSettings logicSettings) : this(settings, logicSettings, Common.Translation.Load(logicSettings))
        {

        }
        public List<BotActions> Actions { get { return this.botActions; } }
        public Session(ISettings settings, ILogicSettings logicSettings, ITranslation translation)
        {
            Forts = new List<FortData>();

            EventDispatcher = new EventDispatcher();
            LogicSettings = logicSettings;

            ElevationService = new ElevationService(this);

            // Update current altitude before assigning settings.
            settings.DefaultAltitude = ElevationService.GetElevation(settings.DefaultLatitude, settings.DefaultLongitude);

            Settings = settings;

            Translation = translation;
            Reset(settings, LogicSettings);
            Stats = new SessionStats();
        }
        public List<FortData> Forts { get; set; }
        public GlobalSettings GlobalSettings { get; set; }

        public ISettings Settings { get; set; }

        public Inventory Inventory { get; private set; }

        public Client Client { get; private set; }

        public GetPlayerResponse Profile { get; set; }
        public Navigation Navigation { get; private set; }

        public ILogicSettings LogicSettings { get; set; }

        public ITranslation Translation { get; }

        public IEventDispatcher EventDispatcher { get; }

        public TelegramService Telegram { get; set; }

        public SessionStats Stats { get; set; }

        public ElevationService ElevationService { get; }

        private List<BotActions> botActions = new List<BotActions>();
        public void Reset(ISettings settings, ILogicSettings logicSettings)
        {
            ApiFailureStrategy _apiStrategy = new ApiFailureStrategy(this);
            Client = new Client(Settings, _apiStrategy);
            // ferox wants us to set this manually
            Inventory = new Inventory(Client, logicSettings);
            Navigation = new Navigation(Client, logicSettings);
        }

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
            while (true && waitTimes < timeout)
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