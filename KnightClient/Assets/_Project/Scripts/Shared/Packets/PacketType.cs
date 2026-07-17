namespace KnightOnline.Client.Shared.Packets
{
    public enum PacketType : byte
    {
        Unknown = 0,
        ConnectRequest = 1,
        ConnectResponse = 2,
        CreateCharacterRequest = 3,
        CreateCharacterResponse = 4,
        ListCharactersRequest = 5,
        ListCharactersResponse = 6,
    }
}
