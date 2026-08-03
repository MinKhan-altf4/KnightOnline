using Xunit;

// All tests currently share the configured PostgreSQL database. Schema
// migration and cleanup must not race across test classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
