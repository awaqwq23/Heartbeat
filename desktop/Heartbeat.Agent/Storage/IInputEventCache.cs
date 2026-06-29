using Heartbeat.Core.DTOs.Input;

namespace Heartbeat.Agent.Storage
{
    public interface IInputEventCache
    {
        void Add(List<InputEventItem> items);
        List<InputEventItem> Load();
        void Clear();
    }
}
