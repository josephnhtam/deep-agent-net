using Microsoft.Extensions.AI;

namespace CodingAgentSample
{
    public static class Extensions
    {
        public static bool IsRejectedFunctionResult(this FunctionResultContent callResult)
        {
            if (callResult.AdditionalProperties?.ContainsKey("PreValidationRejected") == true)
                return true;

            if (callResult.Result is string resultString && resultString.StartsWith("Tool call invocation rejected."))
                return true;

            return false;
        }
    }
}
