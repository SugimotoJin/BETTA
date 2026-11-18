using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Betta3D.EditorTools
{
    /// <summary>
    /// Before every build, makes sure the betta shaders are in "Always Included Shaders",
    /// so fish created from code (BettaFish.Create / BettaTank.Spawn) also work in a built player.
    /// </summary>
    class BettaBuildHook : IPreprocessBuildWithReport
    {
        static readonly string[] Names = { "Betta/Body", "Betta/Fin", "Betta/Eye", "Betta/Food" };

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report) { IncludeShaders(); }

        public static void IncludeShaders()
        {
            Object gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            SerializedObject so = new SerializedObject(gs);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            foreach (string n in Names)
            {
                Shader sh = Shader.Find(n);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) { found = true; break; }
                if (found) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("Betta: ベタのシェーダーを Always Included Shaders に追加しました");
            }
        }
    }
}
