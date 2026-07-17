using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Gameplay.Services
{
    public sealed class CharacterSelectionService
    {
        private readonly IEventBus _eventBus;

        public CharacterSelectionService(IEventBus eventBus) => _eventBus = eventBus;

        public void SelectCharacter(CharacterData character)
        {
            _eventBus.Publish(new CharacterSelectedEvent(character));
        }
    }
}
