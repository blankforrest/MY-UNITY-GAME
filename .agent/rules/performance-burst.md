# Burst Compiler & Performance Rules

- Always use Burst compiler when doable by adding [BurstCompile] attribute to jobs
- Always use Unity.Jobs (IJob, IJobParallelFor) instead of plain threads for parallelism
- Always use NativeArray, NativeList, or other NativeCollections instead of managed arrays inside Burst-compiled jobs
- Never use managed objects (classes, strings, delegates) inside [BurstCompile] methods
- Always add [ReadOnly] or [WriteOnly] attributes to NativeArray parameters in jobs for safety and performance
- Always schedule jobs with .Schedule() or .ScheduleParallel() instead of running them on the main thread when possible
- Always call JobHandle.Complete() only when the result is actually needed, not immediately after scheduling
- Prefer struct-based jobs over class-based MonoBehaviour logic for performance-critical systems
- Always use Mathematics library (Unity.Mathematics) inside Burst jobs instead of Mathf
- Always dispose NativeCollections after use to avoid memory leaks