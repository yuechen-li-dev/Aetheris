using Aetheris.Firmament.FrictionLab.Harness;

var runner = new FrictionLabRunner();
var summary = runner.Run();

Console.WriteLine($"FrictionLab processed {summary.TotalCases} case(s): success={summary.SuccessCount}, partial={summary.PartialCount}, failure={summary.FailureCount}.");
Console.WriteLine($"Summary: {summary.SummaryPath}");
