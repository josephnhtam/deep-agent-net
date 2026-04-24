using Microsoft.Extensions.AI;

namespace SampleUtilities.AgentConsoles
{
    public static class Extensions
    {
        public static bool IsRejectedFunctionResult(this FunctionResultContent callResult)
        {
            if (callResult.AdditionalProperties?.ContainsKey("PreValidationRejected") == true)
                return true;

            if (callResult.Result is string resultString && resultString.StartsWith("Error: Tool call invocation was rejected by user."))
                return true;

            return false;
        }
    }
}
