using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeoCoordinatePortable;
using PoGo.NecroBot.Logic.Service;
using PoGo.NecroBot.Logic.State;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Responses;
using PoGo.NecroBot.Logic.Model.Mapzen;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Utils;
using PoGo.NecroBot.Logic.Model;

namespace PoGo.NecroBot.Logic.Strategies.Walk
{
    class MapzenNavigationStrategy : BaseWalkStrategy, IWalkStrategy
    {
        private MapzenDirectionsService _mapzenDirectionsService;

        public MapzenNavigationStrategy(Client client) : base(client)
        {
            _mapzenDirectionsService = null;
        }

        public override string RouteName => "Mapzen Walk";

        public override async Task<PlayerUpdateResponse> Walk(IGeoLocation targetLocation, Func<Task> functionExecutedWhileWalking, ISession session, CancellationToken cancellationToken, double walkSpeed = 0.0)
        {
            GetMapzenInstance(session);
            var sourceLocation = new GeoCoordinate(_client.CurrentLatitude, _client.CurrentLongitude, _client.CurrentAltitude);
            var destinaionCoordinate = new GeoCoordinate(targetLocation.Latitude, targetLocation.Longitude);
            MapzenWalk mapzenWalk = _mapzenDirectionsService.GetDirections(sourceLocation, destinaionCoordinate);

            if (mapzenWalk == null)
            {
                return await RedirectToNextFallbackStrategy(session.GlobalSettings, targetLocation, functionExecutedWhileWalking, session, cancellationToken);
            }
            
            base.OnStartWalking(session, targetLocation, mapzenWalk.Distance);
            List<GeoCoordinate> points = mapzenWalk.Waypoints;
            return await DoWalk(points, session, functionExecutedWhileWalking, sourceLocation, destinaionCoordinate, cancellationToken, walkSpeed);
        }

        private void GetMapzenInstance(ISession session)
        {
            if (_mapzenDirectionsService == null)
                _mapzenDirectionsService = new MapzenDirectionsService(session);
        }

        public override double CalculateDistance(double sourceLat, double sourceLng, double destinationLat, double destinationLng, ISession session = null)
        {
            // Too expensive to calculate true distance.
            //return 1.5 * base.CalculateDistance(sourceLat, sourceLng, destinationLat, destinationLng);

            
            if (session != null)
                GetMapzenInstance(session);

            if (_mapzenDirectionsService != null)
            {
                MapzenWalk mapzenWalk = _mapzenDirectionsService.GetDirections(new GeoCoordinate(sourceLat, sourceLng), new GeoCoordinate(destinationLat, destinationLng));
                if (mapzenWalk == null)
                {
                    return 1.5 * base.CalculateDistance(sourceLat, sourceLng, destinationLat, destinationLng);
                }
                else
                {
                    //There are two ways to get distance value. One is counting from waypoint list.
                    //The other is getting from google service. The result value is different.

                    ////Count distance from waypoint list
                    List<GeoCoordinate> points = mapzenWalk.Waypoints;
                    double distance = 0;
                    for (var i = 0; i < points.Count; i++)
                    {
                        if (i > 0)
                        {
                            distance += LocationUtils.CalculateDistanceInMeters(points[i - 1], points[i]);
                        }
                    }
                    return distance;

                    //return mapzenWalk.Distance;
                }
            }
            else
            {
                return 1.5 * base.CalculateDistance(sourceLat, sourceLng, destinationLat, destinationLng);
            }
            
        }
    }
}
