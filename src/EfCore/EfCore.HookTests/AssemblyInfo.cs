// HookTest tracks hook invocations in process-global static counters, so these test classes
// cannot run concurrently without polluting each other's assertions. Serialise the assembly.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
