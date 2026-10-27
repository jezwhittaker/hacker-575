using Xunit;

// Repository tests select their SQLite file through the process-wide DB_FILE variable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
