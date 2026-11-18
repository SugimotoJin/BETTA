using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Betta3D
{
    /// <summary>Settings of the desktop app, saved as JSON next to the other app data.</summary>
    [Serializable]
    public class BettaSave
    {
        public List<BettaParams> fish = new List<BettaParams>();
        public bool mouseReact = true;
        public float fishScale = 0.6f;      // fish size on screen (1 = 1/8 of the screen height)
        public int fps = 30;                // 30 is plenty for slow-moving fish and saves GPU
        public float surface = 0.1f;        // water surface, as a fraction of the screen height from the top
        public int version = 2;
        public List<string> monitors = new List<string>();   // device names of the monitors to swim on (empty = main)

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, "betta_settings.json"); } }

        public static BettaSave Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    BettaSave s = JsonUtility.FromJson<BettaSave>(File.ReadAllText(FilePath));
                    if (s != null && s.fish != null)
                    {
                        if (s.version < 2) { s.fps = 30; s.surface = 0.1f; s.version = 2; }   // new defaults
                        return s;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Betta: 設定を読めませんでした: " + e.Message); }
            return null;
        }

        public void Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("Betta: 設定を保存できませんでした: " + e.Message); }
        }
    }
}
