// Writes compile errors, shader errors and console errors/warnings to Logs/BettaConsole.txt
// so they can be read without copying them out of the Console window.
// Lives in its own assembly so it still works when other scripts fail to compile.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace BettaTools
{
    [InitializeOnLoad]
    static class BettaConsoleLogger
    {
        static readonly object Lock = new object();
        static readonly string LogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BettaConsole.txt"));

        static BettaConsoleLogger()
        {
            CompilationPipeline.compilationStarted += _ => Write("==== compilation started " + DateTime.Now.ToString("HH:mm:ss"));
            CompilationPipeline.assemblyCompilationFinished += (asm, messages) =>
            {
                foreach (CompilerMessage m in messages)
                    Write("[compile " + m.type + "] " + m.file + "(" + m.line + "," + m.column + "): " + m.message);
            };
            Application.logMessageReceivedThreaded += (condition, stack, type) =>
            {
                if (type == LogType.Log) return;
                string s = "[" + type + "] " + condition;
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) s += "\n" + stack;
                Write(s);
            };
            EditorApplication.delayCall += CheckShaders;
            Write("==== editor ready " + DateTime.Now.ToString("HH:mm:ss"));
        }

        [MenuItem("Betta/エラーをログに書き出す")]
        static void CheckShaders()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // the betta shaders: in the package (Packages/com.kyabe.betta) or anywhere under Assets/Betta*
                if (!path.StartsWith("Packages/com.kyabe.betta/") && !path.StartsWith("Assets/Betta")) continue;
                Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) continue;
                ShaderMessage[] msgs = ShaderUtil.GetShaderMessages(sh);
                if (msgs.Length == 0) { Write("[shader ok] " + path); continue; }
                foreach (ShaderMessage m in msgs)
                    Write("[shader " + m.severity + "] " + path + " line " + m.line + " (" + m.platform + "): " + m.message);
            }
        }

        static void Write(string s)
        {
            lock (Lock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                    File.AppendAllText(LogPath, s + Environment.NewLine);
                }
                catch (Exception) { }
            }
        }
    }
}
