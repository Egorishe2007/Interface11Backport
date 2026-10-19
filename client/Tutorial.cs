using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using EFT.InventoryLogic;
using UnityEngine;

namespace Interface10
{
    // The tutorial mod (Tutorial Backport, its code still in the MenuTweaks namespace), when it is installed: its notes
    // and tapes for the Handbook. Every call goes through Bridge, which reaches it by reflection, so Interface10 also
    // builds and runs without it. The tutorial mod depends on this one, so this one must not depend on it (no
    // BepInDependency): it is looked for only once the game runs, when every plugin is loaded.
    internal static class Tutorial
    {
        internal const string Recorder = "6526909b539ee574cb624991";
        private static bool? _present;

        // Tutorial Backport's GUID; com.egor.menutweaks is the same mod before its Forge name (development builds)
        internal static bool Present => _present ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.grifm0le.tutorialbackport")
                                                     || BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.egor.menutweaks");

        internal static string NotesCategory => Present ? Bridge.NotesCategory() : null;
        internal static string TapesCategory => Present ? Bridge.TapesCategory() : null;
        internal static int FoundVersion => Present ? Bridge.FoundVersion() : 0;
        internal static bool IsNote(string template) => Present && Bridge.IsNote(template);
        internal static bool IsTape(string template) => Present && Bridge.IsTape(template);
        internal static bool Found(string profileId, string template) => Present && Bridge.Found(profileId, template);

        private static bool _noFoundBy;

        // The notes and tapes the character has found (none with a tutorial mod older than 1.20.1, which can't tell).
        internal static List<string> FoundBy(string profileId)
        {
            if (Present && !_noFoundBy)
            {
                try
                {
                    return Bridge.FoundBy(profileId);
                }
                catch (MissingMethodException)
                {
                    _noFoundBy = true;
                    Interface10Plugin.Log.LogInfo("The tutorial mod is older than 1.20.1: no marks of new notes and tapes");
                }
            }

            return new List<string>();
        }
        internal static AudioClip Recording(string template) => Present ? Bridge.Recording(template) : null;
        internal static List<KeyValuePair<string, string>> Transcript(string template) =>
            Present ? Bridge.Transcript(template) : new List<KeyValuePair<string, string>>();
        internal static void Read(Item note)
        {
            if (Present)
            {
                Bridge.Read(note);
            }
        }

        // By reflection, so this mod builds and runs without the tutorial mod (which is published separately).
        private static class Bridge
        {
            private static Type _content;
            private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();

            private static Type Content => _content ??= AccessTools.TypeByName("MenuTweaks.TutorialContent");

            private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // plain reflection: AccessTools.Property logs a HarmonyX warning for each member that is a field instead
            private static object Get(string name)
            {
                var property = Content.GetProperty(name, Static);
                return property != null ? property.GetValue(null) : Content.GetField(name, Static)?.GetValue(null);
            }

            private static object Call(string name, params object[] args)
            {
                if (!Methods.TryGetValue(name, out var method))
                {
                    Methods[name] = method = AccessTools.Method(Content, name);
                }

                if (method == null)
                {
                    throw new MissingMethodException("MenuTweaks.TutorialContent", name);
                }

                try
                {
                    return method.Invoke(null, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }
            }

            internal static string NotesCategory() => (string)Get("NotesCategory");

            internal static string TapesCategory() => (string)Get("TapesCategory");

            internal static int FoundVersion() => (int)Get("FoundVersion");

            internal static bool IsNote(string template) => (bool)Call("IsNote", template);

            internal static bool IsTape(string template) => (bool)Call("IsTape", template);

            internal static bool Found(string profileId, string template) => (bool)Call("Found", profileId, template);

            internal static List<string> FoundBy(string profileId) => (List<string>)Call("FoundBy", profileId);

            internal static AudioClip Recording(string template) => (AudioClip)Call("Recording", template);

            internal static List<KeyValuePair<string, string>> Transcript(string template) => (List<KeyValuePair<string, string>>)Call("Transcript", template);

            internal static void Read(Item note) => Call("Read", note);
        }
    }
}
