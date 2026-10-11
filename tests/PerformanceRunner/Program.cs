using PerformanceRunner.Perf;

namespace PerformanceRunner;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Local engineering diagnostics are deliberately English, not localized product UI.")]
internal static class Program
{
	private static async Task Main()
	{
		Console.WriteLine("=== VertexBPMN Token Engine Performance Validation ===");
		Console.WriteLine();

		try
		{
			var results = await TokenEnginePerformanceValidator.RunValidationAsync();
			Console.WriteLine(results);
			results = await DistributedProcessEnginePerformanceValidator.RunValidationAsync();
			Console.WriteLine(results);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Performance validation failed: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
		}

		Console.WriteLine();
		Console.WriteLine("Press any key to exit...");
		Console.ReadKey();
	}
}
