namespace ClashOfClansProxy
{
    public class ClientState : State
    {
        public ServerState serverState;
        public byte[] serverKey, nonce;
    }
}
