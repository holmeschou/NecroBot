﻿using Caching;
using PoGo.NecroBot.Logic.Interfaces.Configuration;
using PoGo.NecroBot.Logic.Model.Settings;
using System;

namespace PoGo.NecroBot.Logic.Service.Elevation
{
    public class RandomElevationService : BaseElevationService
    {
        private double minElevation = 5;
        private double maxElevation = 50;
        private Random rand = new Random();

        public RandomElevationService(GlobalSettings globalSettings, LRUCache<string, double> cache) : base(globalSettings, cache)
        {
        }

        public override string GetServiceId()
        {
            return "Random Elevation Service (Necrobot Default)";
        }

        public override double GetElevationFromWebService(double lat, double lng)
        {
            return rand.NextDouble() * (maxElevation - minElevation) + minElevation;
        }
    }
}
