using VertexBPMN.Tests.Perf;

namespace VertexBPMN.PerformanceRunner;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== VertexBPMN Token Engine Performance Validation ===");
        Console.WriteLine();
        
        try
        {
            var results = await TokenEnginePerformanceValidator.RunValidationAsync();
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