using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Gameplay.Services
{
    public interface ILocalAccountSessionStore
    {
        StoredAccountSession Load();
        string GetOrCreateDeviceId();
        void Save(StoredAccountSession session);
        void Clear();
    }
}
