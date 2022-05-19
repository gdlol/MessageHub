namespace MessageHub.HomeServer.P2p.Libp2p.Native;

[Serializable]
public class LibP2pException : Exception
{
    private LibP2pException(string message) : base(message) { }

    internal static void Check(StringHandle errorHandle)
    {
        if (errorHandle.IsInvalid)
        {
            return;
        }
        throw new LibP2pException(errorHandle.ToString() ?? string.Empty);
    }
}
