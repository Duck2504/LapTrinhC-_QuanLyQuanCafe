namespace QuanLyCafe.Application.Reporting;

public sealed class ReportShiftOptions
{
    public TimeOnly MorningStart { get; init; }
    public TimeOnly AfternoonStart { get; init; }
    public TimeOnly EveningStart { get; init; }

    public bool HasOrderedStarts() =>
        MorningStart < AfternoonStart && AfternoonStart < EveningStart;

    public int GetShiftIndex(TimeOnly time)
    {
        if (time < MorningStart || time >= EveningStart)
            return 2;

        return time < AfternoonStart ? 0 : 1;
    }
}
