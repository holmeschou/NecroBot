using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace PoGo.NecroBot.Logic.Model.Settings
{
    [JsonObject(Title = "Console Config", Description = "Set your console settings.", ItemRequired = Required.DisallowNull)]
    public class ConsoleConfig
    {
        [DefaultValue("en")]
        [RegularExpression(@"^[a-zA-Z]{2}(-[a-zA-Z]{2})*$")]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 1)]
        public string TranslationLanguageCode = "en";

        [DefaultValue(2)]
        [Range(0, 100)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 3)]
        public int AmountOfPokemonToDisplayOnStart = 2;

        [DefaultValue(true)]
        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Populate, Order = 4)]
        public bool DetailedCountsBeforeRecycling = true;
    }
}