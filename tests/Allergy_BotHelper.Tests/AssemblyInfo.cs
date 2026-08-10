using Xunit;

// The integration tests share one MongoDB database; run the whole suite serially
// so test classes never delete each other's documents mid-test.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
