using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using POGOProtos.Enums;

namespace PoGo.NecroBot.Logic.Model.Settings
{
    [JsonObject(Description = "", ItemRequired = Required.DisallowNull)] //Dont set Title
    public class TransferFilter
    {
        internal enum Operator
        {
            or,
            and
        }

        public TransferFilter()
        {
        }

        public TransferFilter(int keepMinCp, int keepMinLvl, bool useKeepMinLvl, float keepMinIvPercentage, string keepMinOperator, int keepMinDuplicatePokemon,
            List<List<PokemonMove>> moves = null, string movesOperator = "or")
        {
            KeepMinCp = keepMinCp;
            KeepMinLvl = keepMinLvl;
            UseKeepMinLvl = useKeepMinLvl;
            KeepMinIvPercentage = keepMinIvPercentage;
            KeepMinDuplicatePokemon = keepMinDuplicatePokemon;
            KeepMinOperator = keepMinOperator;
            Moves = moves ?? new List<List<PokemonMove>>();
            MovesOperator = movesOperator;
        }

        [DefaultValue(1250)]
        [Range(0, 9999)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 1)]
        public int KeepMinCp { get; set; }

        [DefaultValue(90)]
        [Range(0, 100)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 2)]
        public float KeepMinIvPercentage { get; set; }

        [DefaultValue(6)]
        [Range(0, 99)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 3)]
        public int KeepMinLvl { get; set; }

        [DefaultValue(false)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 4)]
        public bool UseKeepMinLvl { get; set; }

        [DefaultValue("or")]
        [EnumDataType(typeof(Operator))]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 5)]
        public string KeepMinOperator { get; set; }

        [DefaultValue(1)]
        [Range(0, 999)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 6)]
        public int KeepMinDuplicatePokemon { get; set; }

        [DefaultValue(null)]
        [JsonProperty(Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate, Order = 7)]
        public List<List<PokemonMove>> Moves { get; set; }

        [DefaultValue("and")]
        [EnumDataType(typeof(Operator))]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 8)]
        public string MovesOperator { get; set; }

        internal static Dictionary<PokemonId, TransferFilter> TransferFilterDefault()
        {
            return new Dictionary<PokemonId, TransferFilter>
            {
				//criteria: based on NY Central Park and Tokyo variety + sniping optimization
				{PokemonId.Golduck,    new TransferFilter(1800, 6, false, 95, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.WaterGunFast,PokemonMove.HydroPump }},"and")},
                {PokemonId.Aerodactyl, new TransferFilter(1250, 6, false, 80, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.BiteFast,PokemonMove.HyperBeam }},"and")},
                {PokemonId.Venusaur,   new TransferFilter(1800, 6, false, 95, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.VineWhipFast,PokemonMove.SolarBeam }},"and")},
                {PokemonId.Farfetchd,  new TransferFilter(1250, 6, false, 80, "or", 1)},
                {PokemonId.Krabby,     new TransferFilter(1250, 6, false, 95, "or", 1)},
                {PokemonId.Kangaskhan, new TransferFilter(1500, 6, false, 60, "or", 1)},
                {PokemonId.Horsea,     new TransferFilter(1250, 6, false, 95, "or", 1)},
                {PokemonId.Staryu,     new TransferFilter(1250, 6, false, 95, "or", 1)},
                {PokemonId.MrMime,     new TransferFilter(1250, 6, false, 40, "or", 1)},
                {PokemonId.Scyther,    new TransferFilter(1800, 6, false, 80, "or", 1)},
                {PokemonId.Jynx,       new TransferFilter(1250, 6, false, 95, "or", 1)},
                {PokemonId.Charizard,  new TransferFilter(1250, 6, false, 80, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.WingAttackFast,PokemonMove.FireBlast }}, "and")},
                {PokemonId.Electabuzz, new TransferFilter(1250, 6, false, 80, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.ThunderShockFast,PokemonMove.Thunder }}, "and")},
                {PokemonId.Magmar,     new TransferFilter(1500, 6, false, 80, "or", 1)},
                {PokemonId.Pinsir,     new TransferFilter(1800, 6, false, 95, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.RockSmashFast,PokemonMove.XScissor }},   "and")},
                {PokemonId.Tauros,     new TransferFilter(1250, 6, false, 90, "or", 1)},
                {PokemonId.Magikarp,   new TransferFilter(200,  6, false, 95, "or", 1)},
                {PokemonId.Exeggutor,  new TransferFilter(1800, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.ZenHeadbuttFast,PokemonMove.SolarBeam }},"and")},
                {PokemonId.Gyarados,   new TransferFilter(1250, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.DragonBreath,PokemonMove.HydroPump }},   "and")},
                {PokemonId.Lapras,     new TransferFilter(1800, 6, false, 80, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.FrostBreathFast,PokemonMove.Blizzard }}, "and")},
                {PokemonId.Eevee,      new TransferFilter(1250, 6, false, 95, "or", 1)},
                {PokemonId.Vaporeon,   new TransferFilter(1500, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.WaterGun,PokemonMove.HydroPump }},       "and")},
                {PokemonId.Jolteon,    new TransferFilter(1500, 6, false, 90, "or", 1)},
                {PokemonId.Flareon,    new TransferFilter(1500, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.Ember,PokemonMove.FireBlast }},          "and")},
                {PokemonId.Porygon,    new TransferFilter(1250, 6, false, 60, "or", 1)},
                {PokemonId.Arcanine,   new TransferFilter(1800, 6, false, 80, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.FireFangFast,PokemonMove.FireBlast }},   "and")},
                {PokemonId.Snorlax,    new TransferFilter(2600, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.ZenHeadbuttFast,PokemonMove.HyperBeam }},"and")},
                {PokemonId.Dragonite,  new TransferFilter(2600, 6, false, 90, "or", 1,new List<List<PokemonMove>>() { new List<PokemonMove>() { PokemonMove.DragonBreath,PokemonMove.DragonClaw }},  "and")},

            };
        }
    }
}