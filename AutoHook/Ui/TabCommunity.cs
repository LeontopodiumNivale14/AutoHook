﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoHook.Classes;
using AutoHook.Configurations;
using AutoHook.Enums;
using AutoHook.Fishing;
using AutoHook.Resources.Localization;
using AutoHook.Spearfishing;
using AutoHook.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using ECommons.Throttlers;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace AutoHook.Ui;

public class TabCommunity : BaseTab
{
    public override string TabName { get; } = UIStrings.CommunityPresets;
    public override bool Enabled { get; } = true;
    public override OpenWindow Type { get; } = OpenWindow.Community;

    private static SpearFishingPresets _gigPreset = Service.Configuration.AutoGigConfig;
    private static FishingPresets _fishingPreset = Service.Configuration.HookPresets;

    // Keep per-category folder names while popups are open
    private readonly Dictionary<string, string> _importAllFolderNames = new();

    public override void DrawHeader()
    {
    }

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow,
            UIStrings.CommunityDescription);
        using (ImRaii.Group())
        {
            using (var disabled = ImRaii.Disabled(EzThrottler.GetRemainingTime("WikiUpdate") > 0))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.CloudDownloadAlt, UIStrings.GetWikiPresets))
                    _ = WikiPresets.ListWikiPages();
            }

            if (ImGui.Selectable(UIStrings.ClickOpenWiki))
                OpenWiki();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(UIStrings.NewAccountWarning);

            if (ImGui.CollapsingHeader(UIStrings.Fishing, ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var (key, value) in WikiPresets.Presets.Where(preset => preset.Value.Count != 0))
                {
                    ImGui.Indent();
                    DrawHeaderList(key, value.Cast<BasePresetConfig>().ToList());
                    ImGui.Unindent();
                }
            }

            ImGui.Separator();

            if (ImGui.CollapsingHeader(UIStrings.Spearfishing, ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var (key, value) in WikiPresets.PresetsSf.Where(preset => preset.Value.Count != 0))
                {
                    ImGui.Indent();
                    DrawHeaderList(key, value.Cast<BasePresetConfig>().ToList());
                    ImGui.Unindent();
                }
            }
        }
    }

    private void DrawHeaderList(string tab, List<BasePresetConfig> list)
    {
        if (ImGui.CollapsingHeader($"{tab}, Total: {list.Count}"))
        {
            ImGui.Indent();

            // Import-all with confirmation (and folder creation for fishing presets)
            if (ImGui.Button($"Import all###{tab}"))
            {
                if (!_importAllFolderNames.ContainsKey(tab))
                    _importAllFolderNames[tab] = tab;
                ImGui.OpenPopup($"ImportAll###{tab}");
            }

            // Popup content
            if (ImGui.BeginPopup($"ImportAll###{tab}"))
            {
                var isFishing = list.Count > 0 && list[0] is CustomPresetConfig;

                ImGui.TextWrapped($"Import {list.Count} preset(s) from '{tab}'?");

                if (isFishing)
                {
                    var name = _importAllFolderNames[tab];
                    if (ImGui.InputText(UIStrings.FolderName, ref name, 64, ImGuiInputTextFlags.AutoSelectAll))
                        _importAllFolderNames[tab] = name;
                }

                // Import / Cancel buttons
                if (ImGui.Button(UIStrings.Import))
                {
                    if (isFishing)
                    {
                        var folderName = _importAllFolderNames.TryGetValue(tab, out var n) && !string.IsNullOrWhiteSpace(n)
                            ? n
                            : tab;

                        var importedGuids = new List<System.Guid>();
                        var imported = 0;
                        var skipped = 0;

                        foreach (var preset in list)
                        {
                            if (preset is CustomPresetConfig custom)
                            {
                                // Skip duplicates by name
                                if (_fishingPreset.PresetList.Any(p => p.PresetName == custom.PresetName))
                                {
                                    skipped++;
                                    continue;
                                }

                                // Clone to new preset and add to list
                                var json = JsonConvert.SerializeObject(custom);
                                var copy = JsonConvert.DeserializeObject<CustomPresetConfig>(json);
                                copy!.UniqueId = System.Guid.NewGuid();
                                _fishingPreset.CustomPresets.Add(copy);
                                importedGuids.Add(copy.UniqueId);
                                imported++;
                            }
                        }

                        if (imported > 0)
                        {
                            // Create folder and add imported presets to it
                            var newFolder = new PresetFolder(folderName);
                            foreach (var id in importedGuids)
                                newFolder.AddPreset(id);

                            _fishingPreset.Folders.Add(newFolder);
                            Service.Save();
                            Notify.Success($"Imported {imported} preset(s) into folder '{folderName}'{(skipped > 0 ? $", skipped {skipped} duplicate(s)" : string.Empty)}.");
                        }
                        else
                        {
                            Notify.Info("No new presets to import.");
                        }

                        ImGui.CloseCurrentPopup();
                    }
                    else
                    {
                        // Spearfishing: no folders, just import with duplicate check
                        var imported = 0;
                        var skipped = 0;

                        foreach (var preset in list)
                        {
                            if (preset is AutoGigConfig gig)
                            {
                                if (_gigPreset.Presets.Any(p => p.PresetName == gig.PresetName))
                                {
                                    skipped++;
                                    continue;
                                }
                                _gigPreset.AddNewPreset(gig);
                                imported++;
                            }
                        }

                        if (imported > 0)
                            Notify.Success($"Imported {imported} preset(s){(skipped > 0 ? $", skipped {skipped} duplicate(s)" : string.Empty)}.");
                        else
                            Notify.Info("No new presets to import.");

                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button(UIStrings.DrawImportExport_Cancel))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            foreach (var item in list)
            {
                var color = ImGuiColors.DalamudWhite;
                // check if the preset is fishing or autogig and if already in the list
                if (item is CustomPresetConfig customPreset)
                {
                    if (_fishingPreset.PresetList.Any(p => p.PresetName == customPreset.PresetName))
                        color = ImGuiColors.ParsedGreen;
                }
                else if (item is AutoGigConfig gigPreset)
                {
                    if (_gigPreset.Presets.Any(p => p.PresetName == gigPreset.PresetName))
                        color = ImGuiColors.ParsedGreen;
                }

                using (var a = ImRaii.PushColor(ImGuiCol.Text, color))
                {
                    ImGui.Selectable($"- {item.PresetName}");

                    // Also open the import menu on left-click
                    var popupId = $"PresetOptions###{item.PresetName}";
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        ImGui.OpenPopup(popupId);
                }

                ImportPreset(item);
            }

            ImGui.Unindent();
        }
    }

    private static void ImportAllPresets(List<BasePresetConfig> list)
    {
        var imported = 0;
        var skipped = 0;

        foreach (var preset in list)
        {
            if (preset is CustomPresetConfig custom)
            {
                if (_fishingPreset.PresetList.Any(p => p.PresetName == custom.PresetName))
                {
                    skipped++;
                    continue;
                }
                _fishingPreset.AddNewPreset(custom);
                imported++;
            }
            else if (preset is AutoGigConfig gig)
            {
                if (_gigPreset.Presets.Any(p => p.PresetName == gig.PresetName))
                {
                    skipped++;
                    continue;
                }
                _gigPreset.AddNewPreset(gig);
                imported++;
            }
        }

        if (imported > 0)
            Notify.Success($"Imported {imported} preset(s){(skipped > 0 ? $", skipped {skipped} duplicate(s)" : string.Empty)}.");
        else
            Notify.Info("No new presets to import.");
    }

    public static void ImportPreset(BasePresetConfig preset)
    {
        if (!ImGui.BeginPopupContextItem(@$"PresetOptions###{preset.PresetName}"))
            return;

        var name = preset.PresetName;
        if (preset.PresetName.StartsWith(@"[Old Version]"))
            ImGui.TextColored(ImGuiColors.ParsedOrange, UIStrings.Old_Preset_Warning);
        else
            ImGui.TextWrapped(UIStrings.ImportThisPreset);

        if (ImGui.InputText(UIStrings.PresetName, ref name, 64, ImGuiInputTextFlags.AutoSelectAll))
            preset.RenamePreset(name);

        if (ImGui.Button(UIStrings.Import))
        {
            if (preset is CustomPresetConfig customPreset)
                _fishingPreset.AddNewPreset(customPreset);
            else if (preset is AutoGigConfig gigPreset)
                _gigPreset.AddNewPreset(gigPreset);

            Notify.Success(UIStrings.PresetImported);
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button(UIStrings.DrawImportExport_Cancel))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private static void OpenWiki()
    {
        var url = "https://github.com/PunishXIV/AutoHook/wiki";
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}