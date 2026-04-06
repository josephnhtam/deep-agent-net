using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DeepAgentNet.TodoLists
{
    [Description("Status of the todo")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TodoStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled,
    }
}
