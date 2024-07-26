﻿using System;

namespace AutoHook.Classes;

public abstract class BasePresetConfig()
{
    public string PresetName { get; set; } = "";

    public Guid UniqueId { get; set; } = Guid.NewGuid();
    
    public abstract void DrawOptions();

    public virtual void RenamePreset(string newName)
    {
        PresetName = newName;
        Service.Save();
    }
    
    public abstract void AddItem(BaseOption item);
    
    public abstract void RemoveItem(Guid value);
}