using cAlgo.API;
using cAlgo.API.Indicators;
using System;
using System.Collections.Generic;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RobBookerKnoxvilleDivergence : Indicator
    {
        private Color _upDivergenceColor, _downDivergenceColor;

        private MomentumOscillator _momentumOscillator;

        private RelativeStrengthIndex _relativeStrengthIndex;

        [Parameter("Source", Group = "Divergence")]
        public DataSeries Source { get; set; }

        [Parameter("Periods", DefaultValue = 300, MinValue = 20, Group = "Divergence")]
        public int Periods { get; set; }

        [Parameter("Min Distance", DefaultValue = 10, MinValue = 1, Group = "Divergence")]
        public int MinDistance { get; set; }

        [Parameter("Color", DefaultValue = "Lime", Group = "Up Divergence")]
        public string UpDivergenceColor { get; set; }

        [Parameter("Thickness", DefaultValue = 1, Group = "Up Divergence")]
        public int UpDivergenceThickness { get; set; }

        [Parameter("Style", DefaultValue = LineStyle.Solid, Group = "Up Divergence")]
        public LineStyle UpDivergenceStyle { get; set; }

        [Parameter("Color", DefaultValue = "Red", Group = "Down Divergence")]
        public string DownDivergenceColor { get; set; }

        [Parameter("Thickness", DefaultValue = 1, Group = "Down Divergence")]
        public int DownDivergenceThickness { get; set; }

        [Parameter("Style", DefaultValue = LineStyle.Solid, Group = "Down Divergence")]
        public LineStyle DownDivergenceStyle { get; set; }

        [Parameter("Periods", DefaultValue = 14, MinValue = 1, Group = "Momentum")]
        public int MomentumPeriods { get; set; }

        [Parameter("Source", Group = "Momentum")]
        public DataSeries MomentumSource { get; set; }

        [Parameter("Periods", DefaultValue = 14, MinValue = 1, Group = "RSI")]
        public int RsiPeriods { get; set; }

        [Parameter("Overbought", DefaultValue = 70, Group = "RSI")]
        public double RsiOverbought { get; set; }

        [Parameter("Oversold", DefaultValue = 30, Group = "RSI")]
        public double RsiOversold { get; set; }

        [Parameter("Source", Group = "RSI")]
        public DataSeries RsiSource { get; set; }

        protected override void Initialize()
        {
            _upDivergenceColor = GetColor(UpDivergenceColor);
            _downDivergenceColor = GetColor(DownDivergenceColor);

            _momentumOscillator = Indicators.MomentumOscillator(MomentumSource, MomentumPeriods);
            _relativeStrengthIndex = Indicators.RelativeStrengthIndex(RsiSource, RsiPeriods);
        }

        public override void Calculate(int index)
        {
            if (index <= Periods)
            {
                return;
            }

            var divergences = Source.GetDivergence(_momentumOscillator.Result, index, Periods, MinDistance).ToArray();

            foreach (var divergence in divergences)
            {
                if ((divergence.Type == DivergenceType.Up && _relativeStrengthIndex.Result[index] > RsiOversold) || (divergence.Type == DivergenceType.Down && _relativeStrengthIndex.Result[index] < RsiOverbought))
                {
                    continue;
                }

                PlotDivergence(divergence);
            }
        }

        private void PlotDivergence(Divergence divergence)
        {
            var color = divergence.Type == DivergenceType.Up ? _upDivergenceColor : _downDivergenceColor;

            var thickness = divergence.Type == DivergenceType.Up ? UpDivergenceThickness : DownDivergenceThickness;

            var lineStyle = divergence.Type == DivergenceType.Up ? UpDivergenceStyle : DownDivergenceStyle;

            Chart.DrawTrendLine(divergence.DrawingObjectName, divergence.StartIndex, Source[divergence.StartIndex], divergence.EndIndex, Source[divergence.EndIndex], color, thickness, lineStyle);
        }

        private Color GetColor(string colorString, int alpha = 255)
        {
            var color = colorString[0] == '#' ? Color.FromHex(colorString) : Color.FromName(colorString);

            return Color.FromArgb(alpha, color);
        }
    }

    public static class DivergenceExtensions
    {
        /// <summary>
        /// Returns the divergences between two data series based on provided index
        /// </summary>
        /// <param name="firstSeries">The first data series</param>
        /// <param name="secondSeries">The second data series</param>
        /// <param name="index">Index of the value you want to get its divergences</param>
        /// <param name="periods">This number of previous values from index will be checked to find divergence in both data series</param>
        /// <param name="minDistance">The minimum distance in bars between start and end of divergence</param>
        /// <returns>List of divergences</returns>
        public static List<Divergence> GetDivergence(
            this DataSeries firstSeries, DataSeries secondSeries, int index, int periods, int minDistance)
        {
            var result = new List<Divergence>();

            for (var i = index - minDistance; i >= index - periods; i--)
            {
                var isDiverged = firstSeries.IsDiverged(secondSeries, i, index);

                if (!isDiverged)
                {
                    continue;
                }

                var isHigherHigh = firstSeries.IsHigher(i, minDistance);
                var isLowerLow = firstSeries.IsLower(i, minDistance);

                if (firstSeries[i] < firstSeries[index] && firstSeries.IsConnectionPossible(i, index, Direction.Up) &&
                    secondSeries.IsConnectionPossible(i, index, Direction.Up) && isLowerLow)
                {
                    var divergence = new Divergence
                    {
                        StartIndex = i,
                        EndIndex = index,
                        Type = DivergenceType.Up
                    };

                    result.Add(divergence);
                }
                else if (firstSeries[i] > firstSeries[index] && firstSeries.IsConnectionPossible(i, index, Direction.Down) &&
                    secondSeries.IsConnectionPossible(i, index, Direction.Down) && isHigherHigh)
                {
                    var divergence = new Divergence
                    {
                        StartIndex = i,
                        EndIndex = index,
                        Type = DivergenceType.Down
                    };

                    result.Add(divergence);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns True if data series moved in cross direction on firstPointIndex and secondPointIndex
        /// </summary>
        /// <param name="firstSeries">The first data series</param>
        /// <param name="secondSeries">The second data series</param>
        /// <param name="startIndex">The first point index in data series</param>
        /// <param name="endIndex">The second point index in data series</param>
        /// <returns></returns>
        public static bool IsDiverged(this DataSeries firstSeries, DataSeries secondSeries, int startIndex, int endIndex)
        {
            if (startIndex >= endIndex)
            {
                throw new ArgumentException("The 'startIndex' must be less than 'secondPointIndex'");
            }

            if (firstSeries[startIndex] >= firstSeries[endIndex] && secondSeries[startIndex] < secondSeries[endIndex])
            {
                return true;
            }

            if (firstSeries[startIndex] <= firstSeries[endIndex] && secondSeries[startIndex] > secondSeries[endIndex])
            {
                return true;
            }

            if (firstSeries[startIndex] > firstSeries[endIndex] && secondSeries[startIndex] <= secondSeries[endIndex])
            {
                return true;
            }

            if (firstSeries[startIndex] < firstSeries[endIndex] && secondSeries[startIndex] >= secondSeries[endIndex])
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns True if connecting two provided data point based on cross side is possible otherwise False
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="startIndex">The first point index in data series</param>
        /// <param name="endIndex">The second point index in data series</param>
        /// <param name="direction">The line direction, is it on up direction or low direction?</param>
        /// <returns>bool</returns>
        public static bool IsConnectionPossible(this DataSeries dataSeries, int startIndex, int endIndex, Direction direction)
        {
            if (startIndex >= endIndex)
            {
                throw new ArgumentException("The 'startIndex' must be less than 'secondPointIndex'");
            }

            var slope = dataSeries.GetSlope(startIndex, endIndex);

            var counter = 0;

            for (var i = startIndex + 1; i <= endIndex; i++)
            {
                counter++;

                if (direction == Direction.Up && dataSeries[i] < dataSeries[startIndex] + slope * counter)
                {
                    return false;
                }
                else if (direction == Direction.Down && dataSeries[i] > dataSeries[startIndex] + slope * counter)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the index value is higher than x previous and future values in a data series
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="index">Dataseries value index</param>
        /// <param name="previousValues">The number of index previous values to check</param>
        /// <param name="futureValues">The number of index future values to check</param>
        /// <param name="equal">Check for equality</param>
        /// <returns>bool</returns>
        public static bool IsHigher(
            this DataSeries dataSeries, int index, int previousValues = 0, int futureValues = 0, bool equal = true)
        {
            var previousBarsHighest = previousValues > 0 ? dataSeries.Maximum(index - previousValues, index - 1) : double.NegativeInfinity;
            var futureBarsHighest = futureValues > 0 ? dataSeries.Maximum(index + 1, index + futureValues) : double.NegativeInfinity;

            if (equal)
            {
                return dataSeries[index] >= previousBarsHighest && dataSeries[index] >= futureBarsHighest;
            }
            else
            {
                return dataSeries[index] > previousBarsHighest && dataSeries[index] > futureBarsHighest;
            }
        }

        /// <summary>
        /// Checks if the index value is lower than x previous and future values in a data series
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="index">Dataseries value index</param>
        /// <param name="previousValues">The number of index previous values to check</param>
        /// <param name="futureValues">The number of index future values to check</param>
        /// <param name="equal">Check for equality</param>
        /// <returns>bool</returns>
        public static bool IsLower(
            this DataSeries dataSeries, int index, int previousValues = 0, int futureValues = 0, bool equal = true)
        {
            var previousBarsLowest = previousValues > 0 ? dataSeries.Minimum(index - previousValues, index - 1) : double.PositiveInfinity;
            var futureBarsLowest = futureValues > 0 ? dataSeries.Minimum(index + 1, index + futureValues) : double.PositiveInfinity;

            if (equal)
            {
                return dataSeries[index] <= previousBarsLowest && dataSeries[index] <= futureBarsLowest;
            }
            else
            {
                return dataSeries[index] < previousBarsLowest && dataSeries[index] < futureBarsLowest;
            }
        }

        /// <summary>
        /// Returns the amount of slope between two level in a data series
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="startIndex">The first point index in data series</param>
        /// <param name="endIndex">The second point index in data series</param>
        /// <returns>double</returns>
        public static double GetSlope(this DataSeries dataSeries, int startIndex, int endIndex)
        {
            return (dataSeries[endIndex] - dataSeries[startIndex]) / (endIndex - startIndex);
        }

        /// <summary>
        /// Returns the maximum value between start and end (inclusive) index in a dataseries
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="startIndex">Start index (Ex: 1)</param>
        /// <param name="endIndex">End index (Ex: 10)</param>
        /// <returns>double</returns>
        public static double Maximum(this DataSeries dataSeries, int startIndex, int endIndex)
        {
            var max = double.NegativeInfinity;

            for (var i = startIndex; i <= endIndex; i++)
            {
                max = Math.Max(dataSeries[i], max);
            }

            return max;
        }

        /// <summary>
        /// Returns the minimum value between start and end (inclusive) index in a dataseries
        /// </summary>
        /// <param name="dataSeries"></param>
        /// <param name="startIndex">Start index (Ex: 1)</param>
        /// <param name="endIndex">End index (Ex: 10)</param>
        /// <returns>double</returns>
        public static double Minimum(this DataSeries dataSeries, int startIndex, int endIndex)
        {
            var min = double.PositiveInfinity;

            for (var i = startIndex; i <= endIndex; i++)
            {
                min = Math.Min(dataSeries[i], min);
            }

            return min;
        }
    }

    public class Divergence
    {
        public DivergenceType Type { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        public int BarsInBetween
        {
            get
            {
                return EndIndex - StartIndex;
            }
        }

        public string DrawingObjectName
        {
            get
            {
                return string.Format("{0} {1} {2}", Type, StartIndex, EndIndex);
            }
        }
    }

    public enum Direction
    {
        None,
        Up,
        Down
    }

    public enum DivergenceType
    {
        Up,
        Down
    }
}