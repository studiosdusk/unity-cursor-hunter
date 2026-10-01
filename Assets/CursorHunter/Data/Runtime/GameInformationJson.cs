using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    public static class GameInformationJson
    {
        public static string Serialize(GameInformation information, bool pretty = true)
        {
            if (information == null || !information.TryValidate(out _))
                throw new ArgumentException("Invalid game information.");
            return JsonUtility.ToJson(information, pretty);
        }

        public static bool TryDeserialize(string json, out GameInformation information, out string error)
        {
            information = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024)
            {
                error = "Game information JSON is empty or exceeds 1 MiB.";
                return false;
            }
            try
            {
                var candidate = JsonUtility.FromJson<GameInformation>(json);
                if (candidate == null) { error = "Missing game information."; return false; }
                if (!candidate.TryValidate(out error)) return false;
                information = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
