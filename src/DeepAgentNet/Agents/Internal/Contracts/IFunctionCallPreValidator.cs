using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal.Contracts
{
    internal interface IFunctionCallPreValidValidator
    {
        ValueTask<string?> PreValidateAsync(FunctionCallContent call, CancellationToken cancellationToken);
    }

    internal interface IFunctionCallPreValidationRegistry
    {
        void Register(string toolName, FunctionCallPreValidationDelegate validationFunc);
    }
}
