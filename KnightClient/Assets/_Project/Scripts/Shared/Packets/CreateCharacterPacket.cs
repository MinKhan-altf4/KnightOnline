namespace KnightOnline.Client.Shared.Packets
{
    public enum CreateCharacterResult : byte
    {
        Success = 0,
        NameEmpty = 1,
        NameTooLong = 2,
        NameAlreadyTaken = 3,
        CharacterLimitReached = 4,
        Unauthorized = 5,
    }

    public sealed class CreateCharacterRequestPacket
    {
        public string CharacterName { get; }

        public CreateCharacterRequestPacket(string characterName)
        {
            CharacterName = characterName;
        }
    }

    public sealed class CreateCharacterResponsePacket
    {
        public CreateCharacterResult Result { get; }
        public string Message { get; }

        public CreateCharacterResponsePacket(CreateCharacterResult result, string message)
        {
            Result = result;
            Message = message;
        }
    }
}
