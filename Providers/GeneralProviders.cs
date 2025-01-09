using QuantitiesNet;
using static QuantitiesNet.Units;
using UnityEngine;
using DV.Simulation.Cars;

namespace DvMod.HeadsUpDisplay
{
    public static class GeneralProviders
    {
        // U+2007 FIGURE SPACE
        // U+002B PLUS SIGN
        // U+2212 MINUS SIGN
        private const string GradeFormat = "\u002b0.0' %';\u22120.0' %'";

        public static void Register()
        {
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Length>(
                "Altitude",
                car => new Quantities.Length(car.transform.position.y - 110f)));

            Registry.Register(new QuantityQueryDataProvider<Dimensions.Velocity>(
                "Speed",
                car => new Quantities.Velocity(Mathf.Abs(car.GetForwardSpeed()), MetersPerSecond)));

            Registry.Register(new QuantityQueryDataProvider<Dimensions.Velocity>(
                "Speed Limit",
                car =>
                {
                    var bogie = car.Bogies[1];
                    var track = bogie.track;
                    if (track == null)
                        return new Quantities.Velocity(0);
                    var startSpan = bogie.traveller.Span;
                    var locoDirection = PlayerManager.LastLoco?.GetComponent<SimController>()?.controlsOverrider.Reverser.Value >= 0.5f;
                    var direction = !locoDirection ^ (bogie.TrackDirectionSign > 0);
                    return new Quantities.Velocity(TrackFollower.GetSpeedLimit(track, startSpan, direction)??0, KilometersPerHour);
                }));

            Registry.Register(new FloatQueryDataProvider(
                "Grade",
                car =>
                {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => f.ToString(GradeFormat)));

            Registry.Register(new QuantityQueryDataProvider<Dimensions.Pressure>(
                "Brake pipe",
                car => new Quantities.Pressure(car.brakeSystem.brakePipePressure, Bar)));
        }
    }
}
