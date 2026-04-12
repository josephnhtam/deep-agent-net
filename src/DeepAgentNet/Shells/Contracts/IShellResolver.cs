namespace DeepAgentNet.Shells.Contracts
{
    public interface IShellResolver
    {
        List<IShellRunner> ResolveShells();
    }
}
