#region using directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PokemonGo.RocketAPI.Enums;
using POGOProtos.Enums;
using PoGo.NecroBot.Logic.Service.Elevation;
using PokemonGo.RocketAPI.Extensions;

#endregion

namespace PoGo.NecroBot.Logic.Model.Settings
{
    [JsonObject(Title = " Global Settings", Description = "Set your global settings.", ItemRequired = Required.DisallowNull)]
    public class GlobalSettings
    {
        [JsonIgnore]
        public AuthSettings Auth = new AuthSettings();
        [JsonIgnore]
        public string GeneralConfigPath;
        [JsonIgnore]
        public string ProfileConfigPath;
        [JsonIgnore]
        public string ProfilePath;

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ConsoleConfig ConsoleConfig = new ConsoleConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public UpdateConfig UpdateConfig = new UpdateConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public WebsocketsConfig WebsocketsConfig = new WebsocketsConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public LocationConfig LocationConfig = new LocationConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public GpxConfig GPXConfig = new GpxConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SnipeConfig SnipeConfig = new SnipeConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public HumanWalkSnipeConfig HumanWalkSnipeConfig = new HumanWalkSnipeConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DataSharingConfig DataSharingConfig = new DataSharingConfig();


        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PokeStopConfig PokeStopConfig = new PokeStopConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public GymConfig GymConfig = new GymConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PokemonConfig PokemonConfig = new PokemonConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ItemRecycleConfig RecycleConfig = new ItemRecycleConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public CustomCatchConfig CustomCatchConfig = new CustomCatchConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PlayerConfig PlayerConfig = new PlayerConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SoftBanConfig SoftBanConfig = new SoftBanConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public GoogleWalkConfig GoogleWalkConfig = new GoogleWalkConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public YoursWalkConfig YoursWalkConfig = new YoursWalkConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public MapzenWalkConfig MapzenWalkConfig = new MapzenWalkConfig();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ItemRecycleFilter> ItemRecycleFilter = Settings.ItemRecycleFilter.ItemRecycleFilterDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<PokemonId> PokemonsNotToTransfer = TransferConfig.PokemonsNotToTransferDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<PokemonId> PokemonsToEvolve = EvolveConfig.PokemonsToEvolveDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<PokemonId> PokemonsToLevelUp = LevelUpConfig.PokemonsToLevelUpDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<PokemonId> PokemonsToIgnore = CatchConfig.PokemonsToIgnoreDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<PokemonId, TransferFilter> PokemonsTransferFilter = TransferFilter.TransferFilterDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SnipeSettings PokemonToSnipe = SnipeSettings.Default();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<PokemonId> PokemonToUseMasterball = CatchConfig.PokemonsToUseMasterballDefault();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<PokemonId, HumanWalkSnipeFilter> HumanWalkSnipeFilters = HumanWalkSnipeFilter.Default();

        [JsonProperty(Required = Required.DisallowNull, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<PokemonId, UpgradeFilter> PokemonUpgradeFilters = UpgradeFilter.Default();

        public GlobalSettings()
        {
            InitializePropertyDefaultValues(this);
        }

        public void InitializePropertyDefaultValues(object obj)
        {
            var fields = obj.GetType().GetFields();

            foreach (var field in fields)
            {
                var d = field.GetCustomAttribute<DefaultValueAttribute>();

                if (d != null)
                    field.SetValue(obj, d.Value);
            }
        }

        public static GlobalSettings Default => new GlobalSettings();

        private static JSchema _schema;

        private static JSchema JsonSchema
        {
            get
            {
                if (_schema != null)
                    return _schema;
                // JSON Schemas from .NET types
                var generator = new JSchemaGenerator
                {
                    // change contract resolver so property names are camel case
                    //ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    // types with no defined ID have their type name as the ID
                    SchemaIdGenerationHandling = SchemaIdGenerationHandling.TypeName,
                    // use the default order of properties.
                    SchemaPropertyOrderHandling = SchemaPropertyOrderHandling.Default,
                    // referenced schemas are inline.
                    SchemaLocationHandling = SchemaLocationHandling.Inline,
                    // no schemas can be referenced.    
                    SchemaReferenceHandling = SchemaReferenceHandling.None
                };
                // change Zone enum to generate a string property
                var strEnumGen = new StringEnumGenerationProvider {CamelCaseText = true};
                generator.GenerationProviders.Add(strEnumGen);
                // generate json schema 
                var type = typeof(GlobalSettings);
                var schema = generator.Generate(type);
                schema.Title = type.Name;
                //
                _schema = schema;
                return _schema;
            }
        }

        public static GlobalSettings Load(string path, bool boolSkipSave = false, bool validate = false)
        {
            GlobalSettings settings;
            var profilePath = Path.Combine(Directory.GetCurrentDirectory(), path);
            var profileConfigPath = Path.Combine(profilePath, "config");
            var configFile = Path.Combine(profileConfigPath, "config.json");
            var shouldExit = false;

            if (File.Exists(configFile))
            {
                try
                {
                    //if the file exists, load the settings
                    string input;
                    var count = 0;
                    while (true)
                    {
                        try
                        {
                            input = File.ReadAllText(configFile, Encoding.UTF8);
                            break;
                        }
                        catch (Exception exception)
                        {
                            if (count > 10)
                            {
                                //sometimes we have to wait close to config.json for access
                                Logger.Write("configFile: " + exception.Message, LogLevel.Error);
                            }
                            count++;
                            Thread.Sleep(1000);
                        }
                    }

                    var jsonSettings = new JsonSerializerSettings();
                    jsonSettings.Converters.Add(new StringEnumConverter {CamelCaseText = true});
                    jsonSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    jsonSettings.DefaultValueHandling = DefaultValueHandling.Populate;

                    try
                    {
                        // validate Json using JsonSchema
                        if (validate)
                        {
                            Logger.Write("Validating config.json...");
                            var jsonObj = JObject.Parse(input);
                            IList<ValidationError> errors = null;
                            bool valid;
                            try
                            {
                                valid = jsonObj.IsValid(JsonSchema, out errors);
                            }
                            catch (JSchemaException ex)
                            {
                                if (ex.Message.Contains("commercial licence") || ex.Message.Contains("free-quota"))
                                {
                                    Logger.Write(
                                        "config.json: " + ex.Message);
                                    valid = false;
                                }
                                else
                                {
                                    throw;
                                }
                            }
                            if (!valid)
                            {
                                if (errors != null)
                                    foreach (var error in errors)
                                    {
                                        Logger.Write(
                                            "config.json [Line: " + error.LineNumber + ", Position: " + error.LinePosition +
                                            "]: " +
                                            error.Path + " " +
                                            error.Message, LogLevel.Error);
                                    }

                                Logger.Write(
                                    "Fix config.json and restart NecroBot or press a key to ignore and continue...",
                                    LogLevel.Warning);
                                Console.ReadKey();
                            }

                            // Now we know it's valid so update input with the migrated version.
                            input = jsonObj.ToString();
                        }

                        settings = JsonConvert.DeserializeObject<GlobalSettings>(input, jsonSettings);
                    }
                    catch (JsonSerializationException exception)
                    {
                        Logger.Write("JSON Exception: " + exception.Message, LogLevel.Error);
                        return null;
                    }
                    catch (JsonReaderException exception)
                    {
                        Logger.Write("JSON Exception: " + exception.Message, LogLevel.Error);
                        return null;
                    }

                    //This makes sure that existing config files dont get null values which lead to an exception
                    foreach (var filter in settings.PokemonsTransferFilter.Where(x => x.Value.KeepMinOperator == null))
                    {
                        filter.Value.KeepMinOperator = "or";
                    }
                    foreach (var filter in settings.PokemonsTransferFilter.Where(x => x.Value.Moves == null))
                    {
                        filter.Value.Moves = new List<List<PokemonMove>>();
                    }
                    foreach (var filter in settings.PokemonsTransferFilter.Where(x => x.Value.MovesOperator == null))
                    {
                        filter.Value.MovesOperator = "or";
                    }
                }
                catch (JsonReaderException exception)
                {
                    Logger.Write("JSON Exception: " + exception.Message, LogLevel.Error);
                    return null;
                }
            }
            else
            {
                settings = new GlobalSettings();
                shouldExit = true;
            }

            settings.ProfilePath = profilePath;
            settings.ProfileConfigPath = profileConfigPath;
            settings.GeneralConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config");

            if (!boolSkipSave)
            {
                settings.Save(configFile);
                settings.Auth.Load(Path.Combine(profileConfigPath, "auth.json"), boolSkipSave, validate);
            }

            return shouldExit ? null : settings;
        }

        private static void MigrateSettings(JObject settings, string configFile, string schemaFile)
        {
            if (settings["UpdateConfig"]?["SchemaVersion"] == null)
            {
                // The is the first time setup for old config.json files without the SchemaVersion.
                // Just set this to 0 so that we can handle the upgrade in case 0.
                settings["UpdateConfig"]["SchemaVersion"] = 0;
            }

            int schemaVersion = (int)settings["UpdateConfig"]["SchemaVersion"];
            if (schemaVersion == UpdateConfig.CURRENT_SCHEMA_VERSION)
            {
                Logger.Write("Configuration is up-to-date. Schema version: " + schemaVersion);
                return;
            }

            // Backup old config file.
            long ts = DateTime.UtcNow.ToUnixTime(); // Add timestamp to avoid file conflicts
            string backupPath = configFile.Replace(".json", $"-{schemaVersion}-{ts}.backup.json");
            Logger.Write($"Backing up config.json to: {backupPath}", LogLevel.Info);
            File.Copy(configFile, backupPath);

            // Add future schema migrations below.
            int version;
            for (version = schemaVersion; version < UpdateConfig.CURRENT_SCHEMA_VERSION; version++)
            {
                Logger.Write($"Migrating configuration from schema version {version} to {version + 1}", LogLevel.Info);
                switch(version)
                {
                    case 1:
                        // Delete the auto complete tutorial settings.
                        ((JObject)settings["PlayerConfig"]).Remove("AutoCompleteTutorial");
                        ((JObject)settings["PlayerConfig"]).Remove("DesiredNickname");
                        ((JObject)settings["PlayerConfig"]).Remove("DesiredGender");
                        ((JObject)settings["PlayerConfig"]).Remove("DesiredStarter");
                        break;

                    case 2:
                        // Remove the TransferConfigAndAuthOnUpdate setting since we always transfer now.
                        ((JObject)settings["UpdateConfig"]).Remove("TransferConfigAndAuthOnUpdate");
                        break;

                    // Add more here.
                }
            }

            // After migration we need to update the schema version to the latest version.
            settings["UpdateConfig"]["SchemaVersion"] = UpdateConfig.CURRENT_SCHEMA_VERSION;
        }

        public void CheckProxy(ITranslation translator)
        {
            Auth.CheckProxy(translator);
        }

        private void Save(string fullPath)
        {
            var output = JsonConvert.SerializeObject(this, Formatting.Indented,
                new StringEnumConverter {CamelCaseText = true});

            var folder = Path.GetDirectoryName(fullPath);
            if (folder != null && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(fullPath, output, Encoding.UTF8);

            //JsonSchema
            //File.WriteAllText(fullPath.Replace(".json", ".schema.json"), JsonSchema.ToString(), Encoding.UTF8);

            // validate Json using JsonSchema
            Logger.Write("Validating config.json...");
            var jsonObj = JObject.Parse(output);
            IList<ValidationError> errors;
            var valid = jsonObj.IsValid(JsonSchema, out errors);
            if (valid) return;
            foreach (var error in errors)
            {
                Logger.Write(
                    "config.json [Line: " + error.LineNumber + ", Position: " + error.LinePosition + "]: " + error.Path +
                    " " +
                    error.Message, LogLevel.Error);
                //"Default value is '" + error.Schema.Default + "'"
            }
            Logger.Write("Fix config.json and restart NecroBot or press a key to ignore and continue...",
                LogLevel.Warning);
            Console.ReadKey();
        }

        public static bool PromptForBoolean(ITranslation translator, string initialPrompt, string errorPrompt = null)
        {
            while (true)
            {
                Logger.Write(initialPrompt, LogLevel.Info);
                var strInput = Console.ReadLine().ToLower();

                switch (strInput)
                {
                    case "y":
                        return true;
                    case "n":
                        return false;
                    default:
                        if (string.IsNullOrEmpty(errorPrompt))
                            errorPrompt = translator.GetTranslation(TranslationString.PromptError, "y", "n");

                        Logger.Write(errorPrompt, LogLevel.Error);
                        continue;
                }
            }
        }

        public static double PromptForDouble(ITranslation translator, string initialPrompt, string errorPrompt = null)
        {
            while (true)
            {
                Logger.Write(initialPrompt, LogLevel.Info);
                var strInput = Console.ReadLine();

                double doubleVal;
                if (double.TryParse(strInput, out doubleVal))
                {
                    return doubleVal;
                }
                else
                {
                    if (string.IsNullOrEmpty(errorPrompt))
                        errorPrompt = translator.GetTranslation(TranslationString.PromptErrorDouble);

                    Logger.Write(errorPrompt, LogLevel.Error);
                }
            }
        }

        public static int PromptForInteger(ITranslation translator, string initialPrompt, string errorPrompt = null)
        {
            while (true)
            {
                Logger.Write(initialPrompt, LogLevel.Info);
                var strInput = Console.ReadLine();

                int intVal;
                if (int.TryParse(strInput, out intVal))
                {
                    return intVal;
                }
                else
                {
                    if (string.IsNullOrEmpty(errorPrompt))
                        errorPrompt = translator.GetTranslation(TranslationString.PromptErrorInteger);

                    Logger.Write(errorPrompt, LogLevel.Error);
                }
            }
        }

        public static string PromptForString(ITranslation translator, string initialPrompt, string[] validStrings = null, string errorPrompt = null, bool caseSensitive = true)
        {
            while (true)
            {
                Logger.Write(initialPrompt, LogLevel.Info);
                // For now this just reads from the console, but in the future, we may change this to read from the GUI.
                string strInput = Console.ReadLine();

                if (!caseSensitive)
                    strInput = strInput.ToLower();

                // If no valid strings to validate, then return immediately.
                if (validStrings == null)
                    return strInput;

                // Validate string
                foreach (string validString in validStrings)
                {
                    if (String.Equals(strInput, validString, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                        return strInput;
                }

                // If we got here, no valid strings.
                if (string.IsNullOrEmpty(errorPrompt))
                {
                    errorPrompt = translator.GetTranslation(TranslationString.PromptErrorString, string.Join(",", validStrings));
                }
                Logger.Write(errorPrompt, LogLevel.Error);
            }
        }
    }
}
