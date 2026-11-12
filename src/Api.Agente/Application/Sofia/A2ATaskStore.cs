using System.Collections.Concurrent;

namespace Api.Agente.Application.Sofia;

public interface IA2ATaskStore
{
    void Save(A2ATaskResult task);
    A2ATaskResult? Get(string taskId);
}

public sealed class A2ATaskStore : IA2ATaskStore
{
    private readonly ConcurrentDictionary<string, A2ATaskResult> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public void Save(A2ATaskResult task)
    {
        if (task is null || string.IsNullOrWhiteSpace(task.Id)) return;
        _tasks[task.Id] = task;

        if (_tasks.Count > 1000)
        {
            var oldest = _tasks.Keys.FirstOrDefault();
            if (oldest != null) _tasks.TryRemove(oldest, out _);
        }
    }

    public A2ATaskResult? Get(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return null;
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }
}
