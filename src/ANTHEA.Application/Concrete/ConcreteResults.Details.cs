using Native = Anthea.Calculations;
namespace Anthea.Application.Concrete;

public static partial class ConcreteResults
{
    public sealed class DetailingCheck
    {
        internal Native.DetailingCheck Source { get; }
        public string Name { get; }
        public double? Actual { get; }
        public double? Limit { get; }
        public string Unit { get; }
        public bool? Passed { get; }
        public string Reference { get; }
        public string Explanation { get; }
        internal DetailingCheck(Native.DetailingCheck value)
        {
            Source = value;
            Name = value.Name;
            Actual = value.Actual;
            Limit = value.Limit;
            Unit = value.Unit;
            Passed = value.Passed;
            Reference = value.Reference;
            Explanation = value.Explanation;
        }
    }
    public sealed class AnchorageResult
    {
        internal Native.AnchorageResult Source { get; }
        public double Fbd { get; }
        public double BasicLength { get; }
        public double RequiredLength { get; }
        public bool Passed { get; }
        public string Expression { get; }
        public double Eta1 { get; }
        public double Eta2 { get; }
        public double Alpha6 { get; }
        public double MaximumLapClearDistance { get; }
        public bool LengthPassed { get; }
        public bool LapClearDistancePassed { get; }
        internal AnchorageResult(Native.AnchorageResult value)
        {
            Source = value;
            Fbd = value.Fbd;
            BasicLength = value.BasicLength;
            RequiredLength = value.RequiredLength;
            Passed = value.Passed;
            Expression = value.Expression;
            Eta1 = value.Eta1;
            Eta2 = value.Eta2;
            Alpha6 = value.Alpha6;
            MaximumLapClearDistance = value.MaximumLapClearDistance;
            LengthPassed = value.LengthPassed;
            LapClearDistancePassed = value.LapClearDistancePassed;
        }
    }
}
