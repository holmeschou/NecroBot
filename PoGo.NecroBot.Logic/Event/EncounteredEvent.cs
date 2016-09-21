using POGOProtos.Enums;
using System;

namespace PoGo.NecroBot.Logic.Event
{
    public class EncounteredEvent : IEvent
    {
        public PokemonId PokemonId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double IV { get; set; }
        public int Level { get; set; }
        public DateTime Expires { get; set; }
        public double ExpireTimestamp { get; set; }
        public string SpawnPointId{ get; set; }
        public string EncounterId { get; internal set; }
    }
}
