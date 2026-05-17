#nullable enable
using System;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Skills;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Skill digits + Q/W/E potion overlays aligned with <see cref="MainControl"/> main_IE layout (virtual 1280×720).
    /// </summary>
    public sealed class HudHotbarStripControl : UIControl
    {
        private readonly CharacterState _state;
        private readonly SkillSelectionPanel? _skillPanel;
        private readonly SkillSlotControl[] _skillSlots = new SkillSlotControl[10];
        private readonly LabelControl[] _digitHints = new LabelControl[10];
        private readonly LabelControl[] _potionLabels = new LabelControl[3];

        /// <summary>Keyboard slot indices in left-to-right bar order: 1…9 then 0.</summary>
        private static readonly int[] BarDigitSlotOrder = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };

        private const int SkillPitch = 44;
        private const int SkillOriginX = 606;
        private const int SkillRowOffsetY = 34;
        private const float SkillSlotScale = 0.82f;

        private const int PotionOriginX = 392;
        private const int PotionPitch = 52;
        private const int PotionY = 6;

        public HudHotbarStripControl(CharacterState state, SkillSelectionPanel? skillPanel)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _skillPanel = skillPanel;

            Align = ControlAlign.Bottom | ControlAlign.Left;
            Margin = new Margin { Bottom = 7 };
            Interactive = true;
            BackgroundColor = Color.Transparent;
            BorderColor = Color.Transparent;

            ViewSize = new Point(UiScaler.VirtualSize.X, 100);

            for (int i = 0; i < 10; i++)
            {
                int slotIdx = BarDigitSlotOrder[i];

                var slot = new SkillSlotControl
                {
                    X = SkillOriginX + i * SkillPitch,
                    Y = SkillRowOffsetY,
                    IsTooltipEnabled = false,
                    Interactive = true
                };
                slot.Scale = SkillSlotScale;
                int capturedSlot = slotIdx;
                slot.Click += (_, _) =>
                {
                    _state.SetArmedHudHotkeySlotIndex(capturedSlot);
                    RefreshFromState();
                };

                var hint = new LabelControl
                {
                    Text = slotIdx == 0 ? "0" : slotIdx.ToString(),
                    TextColor = Color.LightGray,
                    X = SkillOriginX + i * SkillPitch + 10,
                    Y = SkillRowOffsetY + 46,
                    ViewSize = new Point(22, 14),
                    Scale = 0.55f
                };

                _skillSlots[i] = slot;
                _digitHints[i] = hint;
                Controls.Add(slot);
                Controls.Add(hint);
            }

            string[] pk = { "Q", "W", "E" };
            for (int p = 0; p < 3; p++)
            {
                var lab = new LabelControl
                {
                    Text = "",
                    TextColor = Color.LightGoldenrodYellow,
                    X = PotionOriginX + p * PotionPitch,
                    Y = PotionY + 22,
                    ViewSize = new Point(46, 14),
                    Scale = 0.55f
                };
                var keyHint = new LabelControl
                {
                    Text = pk[p],
                    TextColor = Color.Gray,
                    X = PotionOriginX + p * PotionPitch + 14,
                    Y = PotionY - 2,
                    ViewSize = new Point(18, 12),
                    Scale = 0.65f
                };
                _potionLabels[p] = lab;
                Controls.Add(keyHint);
                Controls.Add(lab);
            }

            _state.HudHotkeysChanged += OnHotkeysOrInventoryChanged;
            _state.InventoryChanged += OnHotkeysOrInventoryChanged;

            RefreshFromState();
        }

        private void OnHotkeysOrInventoryChanged()
        {
            RefreshFromState();
        }

        public override void Dispose()
        {
            _state.HudHotkeysChanged -= OnHotkeysOrInventoryChanged;
            _state.InventoryChanged -= OnHotkeysOrInventoryChanged;
            base.Dispose();
        }

        /// <summary>
        /// Opens skill picker (same panel as legacy quick-slot).
        /// </summary>
        public void ToggleSkillSelectionPanel()
        {
            if (_skillPanel == null)
            {
                return;
            }

            if (_skillPanel.Visible)
            {
                _skillPanel.Close();
            }
            else
            {
                _skillPanel.Open(_state);
            }
        }

        public void RefreshFromState()
        {
            for (int i = 0; i < 10; i++)
            {
                int slotIdx = BarDigitSlotOrder[i];
                ushort? id = _state.GetHudSkillId(slotIdx);
                SkillEntryState? entry = id.HasValue ? _state.TryGetSkillById(id.Value) : null;
                _skillSlots[i].Skill = entry;
                _skillSlots[i].IsSelected = _state.ArmedHudHotkeySlotIndex == slotIdx;
            }

            var inv = _state.GetInventoryItems();
            for (int p = 0; p < 3; p++)
            {
                byte? bind = _state.GetHudPotionInventorySlot(p);
                if (!bind.HasValue || !inv.TryGetValue(bind.Value, out byte[] raw) || raw.Length < 4)
                {
                    _potionLabels[p].Text = "";
                    continue;
                }

                string name = ItemDatabase.GetItemName(raw) ?? "?";
                if (name.Length > 6)
                {
                    name = name.Substring(0, 5) + "…";
                }

                _potionLabels[p].Text = name;
            }
        }
    }
}
