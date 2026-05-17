using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Client.Main.Core.Client
{
    /// <summary>
    /// Saves HUD skill digit bindings + Q/W/E potion inventory slots locally (per character name).
    /// OpenMU Season 6 packets in this client do not expose MuMain-style Option/HotKey sync; persistence keeps layout across sessions.
    /// </summary>
    public static class HudHotkeysPersistence
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private sealed class StoreFile
        {
            public Dictionary<string, CharacterHudEntry>? Characters { get; set; }
        }

        private sealed class CharacterHudEntry
        {
            public int ArmedHudHotkeySlotIndex { get; set; }
            public ushort?[]? Skills { get; set; }
            public byte?[]? Potions { get; set; }
        }

        private static string StorePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HudHotkeys.user.json");

        /// <summary>
        /// Loads bindings for <paramref name="state"/>.<see cref="CharacterState.Name"/> if present.
        /// </summary>
        public static void TryApply(CharacterState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.Name) || state.Name == "???")
            {
                return;
            }

            try
            {
                string path = StorePath;
                if (!File.Exists(path))
                {
                    return;
                }

                string json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<StoreFile>(json, JsonOptions);
                if (root?.Characters == null ||
                    !root.Characters.TryGetValue(state.Name, out CharacterHudEntry? entry) ||
                    entry == null)
                {
                    return;
                }

                state.ApplyHudHotkeysFromPersisted(entry.Skills, entry.Potions, entry.ArmedHudHotkeySlotIndex);
            }
            catch (Exception)
            {
                // Ignore corrupted file; defaults apply later.
            }
        }

        /// <summary>
        /// Writes current HUD bindings for the active character.
        /// </summary>
        public static void Save(CharacterState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.Name) || state.Name == "???")
            {
                return;
            }

            try
            {
                StoreFile root;
                string path = StorePath;
                if (File.Exists(path))
                {
                    try
                    {
                        root = JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(path), JsonOptions)
                               ?? new StoreFile { Characters = new Dictionary<string, CharacterHudEntry>() };
                    }
                    catch
                    {
                        root = new StoreFile { Characters = new Dictionary<string, CharacterHudEntry>() };
                    }
                }
                else
                {
                    root = new StoreFile { Characters = new Dictionary<string, CharacterHudEntry>() };
                }

                root.Characters ??= new Dictionary<string, CharacterHudEntry>();

                var skills = new ushort?[10];
                var potions = new byte?[3];
                for (int i = 0; i < 10; i++)
                {
                    skills[i] = state.GetHudSkillId(i);
                }

                for (int i = 0; i < 3; i++)
                {
                    potions[i] = state.GetHudPotionInventorySlot(i);
                }

                root.Characters[state.Name] = new CharacterHudEntry
                {
                    ArmedHudHotkeySlotIndex = state.ArmedHudHotkeySlotIndex,
                    Skills = skills,
                    Potions = potions,
                };

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, JsonSerializer.Serialize(root, JsonOptions));
            }
            catch (Exception)
            {
                // Non-fatal (read-only dir, etc.).
            }
        }
    }
}
