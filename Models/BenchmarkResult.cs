namespace BenchmarkingApp.Models
{
    public class BenchmarkResult
    {
        public string Operation { get; set; }
        public long ExecutionTimeMs { get; set; }
        public bool IsParallel { get; set; }

        // Câmpuri suplimentare pentru grafic/filtrare
        public string RunLabel { get; set; }        // ex: "Run 1"
        public string Entity { get; set; }          // ex: "Orders"
        public string OperationType { get; set; }   // ex: "INSERT"
        public string ExecutionMode { get; set; }   // ex: "Parallel" sau "Sequential"
    }

}
