using System;
using KnightOnline.Client.Data.Models;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.Services
{
    // Temporary alpha adapter. Production builds must replace this adapter
    // with Keychain/Keystore/Credential Manager without changing auth flow.
    public sealed class DevelopmentAccountSessionStore
        : ILocalAccountSessionStore
    {
        private const string SessionKey =
            "KnightOnline.Development.AccountSession";
        private const string DeviceKey =
            "KnightOnline.InstallationId";

        public StoredAccountSession Load()
        {
            string json = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<StoredAccountSession>(json);
            }
            catch (Exception)
            {
                Clear();
                return null;
            }
        }

        public string GetOrCreateDeviceId()
        {
            string id = PlayerPrefs.GetString(DeviceKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id))
                return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceKey, id);
            PlayerPrefs.Save();
            return id;
        }

        public void Save(StoredAccountSession session)
        {
            // Save replaces the previous account. No earlier account token is
            // retained on this device.
            PlayerPrefs.SetString(SessionKey, JsonUtility.ToJson(session));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.Save();
        }
    }
}
