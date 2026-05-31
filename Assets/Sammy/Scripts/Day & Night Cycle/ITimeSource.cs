public interface ITimeSource
{
    int Hours { get; }
    int Minutes { get; }
    int Days { get; }
    float TimeOfDay01 { get; }
}