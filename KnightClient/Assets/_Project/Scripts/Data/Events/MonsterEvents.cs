using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Data.Events
{
    public readonly struct MonsterListReceivedEvent : IStickyGameEvent
    {
        public readonly IReadOnlyList<MonsterData> Monsters;

        public MonsterListReceivedEvent(IReadOnlyList<MonsterData> monsters)
        {
            Monsters = monsters;
        }
    }
}
