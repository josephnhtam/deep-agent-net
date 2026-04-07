using DeepAgentNet.Agents.Internal.Contracts;
using Microsoft.Extensions.AI;

namespace DeepAgentNet.Agents.Internal
{
    internal class FunctionCallPreValidValidator : IFunctionCallPreValidValidator, IFunctionCallPreValidationRegistry
    {
        private readonly Dictionary<string, FunctionCallPreValidationDelegate> _validators = new();

        public void Register(string toolName, FunctionCallPreValidationDelegate validationFunc)
        {
            _validators[toolName] = validationFunc;
        }

        public async ValueTask<string?> PreValidateAsync(
            FunctionCallContent call, CancellationToken cancellationToken)
        {
            if (_validators.TryGetValue(call.Name, out var validator))
            {
                return await validator(call, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }
    }

    internal delegate ValueTask<string?> FunctionCallPreValidationDelegate(
        FunctionCallContent call, CancellationToken cancellationToken);
}
