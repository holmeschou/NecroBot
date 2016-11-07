#region using directives

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using PoGo.NecroBot.CLI.CommandLineUtility;
using PoGo.NecroBot.Logic;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.Model.Settings;
using PoGo.NecroBot.Logic.Service;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;
using PoGo.NecroBot.Logic.Utils;

#endregion

namespace PoGo.NecroBot.CLI
{
    internal class Program
    {
        private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);
        private static string _subPath = "";
        private static bool _enableJsonValidation = true;

        private static readonly Uri StrKillSwitchUri =
            new Uri("https://raw.githubusercontent.com/Necrobot-Private/Necrobot2/master/KillSwitch.txt");

        private static Session _session;

        private static void Main(string[] args)
        {
            var strCulture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

            var culture = CultureInfo.CreateSpecificCulture("en");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;

            Console.Title = @"NecroBot2";
            Console.CancelKeyPress += (sender, eArgs) =>
            {
                QuitEvent.Set();
                eArgs.Cancel = true;
            };

            // Command line parsing
            var commandLine = new Arguments(args);
            // Look for specific arguments values
            if (commandLine["subpath"] != null && commandLine["subpath"].Length > 0)
            {
                _subPath = commandLine["subpath"];
            }
            if (commandLine["jsonvalid"] != null && commandLine["jsonvalid"].Length > 0)
            {
                switch (commandLine["jsonvalid"])
                {
                    case "true":
                        _enableJsonValidation = true;
                        break;
                    case "false":
                        _enableJsonValidation = false;
                        break;
                }
            }

            Logger.SetLogger(new ConsoleLogger(LogLevel.Service), _subPath);

            var profilePath = Path.Combine(Directory.GetCurrentDirectory(), _subPath);
            var profileConfigPath = Path.Combine(profilePath, "config");
            var configFile = Path.Combine(profileConfigPath, "config.json");
            GlobalSettings settings;

            // Load the settings from the config file or generate default
            settings = GlobalSettings.Load(_subPath, false, _enableJsonValidation);
            if (settings == null)
            {
                Logger.Write("Configuration files are not exist. We had generated default configuation files. Please edit it and run again. \nPress a Key to continue...", LogLevel.Warning);
                Console.ReadKey();
                return;
            }

            if (commandLine["latlng"] != null && commandLine["latlng"].Length > 0)
            {
                var crds = commandLine["latlng"].Split(',');
                try
                {
                    var lat = double.Parse(crds[0]);
                    var lng = double.Parse(crds[1]);
                    settings.LocationConfig.DefaultLatitude = lat;
                    settings.LocationConfig.DefaultLongitude = lng;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            var lastPosFile = Path.Combine(profileConfigPath, "LastPos.ini");
            if (File.Exists(lastPosFile) && settings.LocationConfig.StartFromLastPosition)
            {
                var text = File.ReadAllText(lastPosFile);
                var crds = text.Split(':');
                try
                {
                    var lat = double.Parse(crds[0]);
                    var lng = double.Parse(crds[1]);
                    settings.LocationConfig.DefaultLatitude = lat;
                    settings.LocationConfig.DefaultLongitude = lng;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            var translation = Translation.Load(settings);

            if (settings.GPXConfig.UseGpxPathing)
            {
                var xmlString = File.ReadAllText(settings.GPXConfig.GpxFile);
                var readgpx = new GpxReader(xmlString, translation);
                var nearestPt = readgpx.Tracks.SelectMany(
                    (trk, trkindex) =>
                        trk.Segments.SelectMany(
                            (seg, segindex) =>
                                seg.TrackPoints.Select(
                                    (pt, ptindex) =>
                                        new
                                        {
                                            TrackPoint = pt,
                                            TrackIndex = trkindex,
                                            SegIndex = segindex,
                                            PtIndex = ptindex,
                                            Latitude = Convert.ToDouble(pt.Lat, CultureInfo.InvariantCulture),
                                            Longitude = Convert.ToDouble(pt.Lon, CultureInfo.InvariantCulture),
                                            Distance = LocationUtils.CalculateDistanceInMeters(
                                                settings.LocationConfig.DefaultLatitude,
                                                settings.LocationConfig.DefaultLongitude,
                                                Convert.ToDouble(pt.Lat, CultureInfo.InvariantCulture),
                                                Convert.ToDouble(pt.Lon, CultureInfo.InvariantCulture)
                                                )
                                        }
                                    )
                            )
                    ).OrderBy(pt => pt.Distance).FirstOrDefault(pt => pt.Distance <= 5000);

                if (nearestPt != null)
                {
                    settings.LocationConfig.DefaultLatitude = nearestPt.Latitude;
                    settings.LocationConfig.DefaultLongitude = nearestPt.Longitude;
                    settings.GPXConfig.ResumeTrack = nearestPt.TrackIndex;
                    settings.GPXConfig.ResumeTrackSeg = nearestPt.SegIndex;
                    settings.GPXConfig.ResumeTrackPt = nearestPt.PtIndex;
                }
            }

            _session = new Session(new ClientSettings(settings), settings, translation);
            Logger.SetLoggerContext(_session);

            FileSystemWatcher configWatcher = new FileSystemWatcher();
            configWatcher.Path = profileConfigPath;
            configWatcher.Filter = "config.json";
            configWatcher.NotifyFilter = NotifyFilters.LastWrite;
            configWatcher.EnableRaisingEvents = true;
            configWatcher.Changed += (sender, e) =>
            {
                if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    _session.GlobalSettings = GlobalSettings.Load(_subPath, false, _enableJsonValidation);
                    configWatcher.EnableRaisingEvents = !configWatcher.EnableRaisingEvents;
                    configWatcher.EnableRaisingEvents = !configWatcher.EnableRaisingEvents;
                    Logger.Write(" ##### config.json ##### ", LogLevel.Info);
                }
            };

            var stats = new Statistics();
            var strVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
            stats.DirtyEvent +=
                () =>
                    Console.Title = $"[Necrobot2 v{strVersion}] " +
                                    stats.GetTemplatedStats(
                                        _session.Translation.GetTranslation(TranslationString.StatsTemplateString),
                                        _session.Translation.GetTranslation(TranslationString.StatsXpTemplateString));

            if (settings.WebsocketsConfig.UseWebsocket)
            {
                var websocket = new WebSocketInterface(settings.WebsocketsConfig.WebSocketPort, _session);
                _session.EventDispatcher.EventReceived += evt => websocket.Listen(evt, _session);
            }

            var aggregator = new StatisticsAggregator(stats);
            _session.EventDispatcher.EventReceived += evt => aggregator.Listen(evt, _session);

            var listener = new ConsoleEventListener();
            _session.EventDispatcher.EventReceived += evt => listener.Listen(evt, _session);

            if (_session.GlobalSettings.HumanWalkSnipeConfig.Enable)
            {
                var snipeEventListener = new SniperEventListener();
                _session.EventDispatcher.EventReceived += evt => snipeEventListener.Listen(evt, _session);
            }

            if (_session.GlobalSettings.DataSharingConfig.EnableSyncData)
            {
                BotDataSocketClient.StartAsync(_session);
                _session.EventDispatcher.EventReceived += evt => BotDataSocketClient.Listen(evt, _session);
            }

            _session.Navigation.WalkStrategy.UpdatePositionEvent +=
                (lat, lng) => _session.EventDispatcher.Send(new UpdatePositionEvent { Latitude = lat, Longitude = lng });
            _session.Navigation.WalkStrategy.UpdatePositionEvent += SaveLocationToDisk;
            UseNearbyPokestopsTask.UpdateTimeStampsPokestop += SaveTimeStampsPokestopToDisk;
            CatchPokemonTask.UpdateTimeStampsPokemon += SaveTimeStampsPokemonToDisk;

            settings.Auth.CheckProxy(_session.Translation);

            var machine = new StateMachine();
            machine.SetFailureState(new LoginState());
            machine.AsyncStart(new VersionCheckState(), _session);

            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
            }

            if (_session.GlobalSettings.SnipeConfig.UseSnipeLocationServer ||
                _session.GlobalSettings.HumanWalkSnipeConfig.UsePogoLocationFeeder)
                SnipePokemonTask.AsyncStart(_session);

            QuitEvent.WaitOne();

            configWatcher.EnableRaisingEvents = false;
            configWatcher.Dispose();
        }

        private static void EventDispatcher_EventReceived(IEvent evt)
        {
            throw new NotImplementedException();
        }

        private static void SaveLocationToDisk(double lat, double lng)
        {
            var coordsPath = Path.Combine(_session.GlobalSettings.ProfileConfigPath, "LastPos.ini");
            File.WriteAllText(coordsPath, $"{lat}:{lng}");
        }

        private static void SaveTimeStampsPokestopToDisk()
        {
            if (_session == null) return;

            var path = Path.Combine(_session.GlobalSettings.ProfileConfigPath, "PokestopTS.txt");
            var fileContent = _session.Stats.PokeStopTimestamps.Select(t => t.ToString()).ToList();

            if (fileContent.Count > 0)
                File.WriteAllLines(path, fileContent.ToArray());
        }

        private static void SaveTimeStampsPokemonToDisk()
        {
            if (_session == null) return;

            var path = Path.Combine(_session.GlobalSettings.ProfileConfigPath, "PokemonTS.txt");

            var fileContent = _session.Stats.PokemonTimestamps.Select(t => t.ToString()).ToList();

            if (fileContent.Count > 0)
                File.WriteAllLines(path, fileContent.ToArray());
        }

        private static void UnhandledExceptionEventHandler(object obj, UnhandledExceptionEventArgs args)
        {
            Logger.Write("Exception caught, writing LogBuffer.", force: true);
            throw new Exception();
        }
    }
}
