﻿using AutoHook.Data;
using AutoHook.Resources.Localization;
using AutoHook.Utils;
using System;

namespace AutoHook.Classes.AutoCasts;

public class AutoChum : BaseActionCast
{
    public bool _onlyUseWithIntuition;
    public int _useWhenIntuitionExceeds = 0;

    public override bool DoesCancelMooch() => true;

    public AutoChum() : base(UIStrings.Chum, IDs.Actions.Chum)
    {
        HelpText = UIStrings.CancelsCurrentMooch;
    }

    public override string GetName()
        => Name = UIStrings.Chum;

    public override bool CastCondition()
    {
        var hasIntuition = PlayerRes.HasStatus(IDs.Status.FishersIntuition);
        if (!hasIntuition && _onlyUseWithIntuition)
            return false;

        if (hasIntuition && _onlyUseWithIntuition && PlayerRes.GetStatusTime(IDs.Status.FishersIntuition) <= _useWhenIntuitionExceeds)
            return false;

        return true;
    }

    protected override DrawOptionsDelegate DrawOptions => () =>
    {
        if (DrawUtil.Checkbox(UIStrings.OnlyUseWhenFisherSIntutionIsActive, ref _onlyUseWithIntuition))
        {
            Service.Save();
        }

        if (_onlyUseWithIntuition)
        {
            var time = _useWhenIntuitionExceeds;
            if (DrawUtil.EditNumberField(UIStrings.UseWhenIntuitionTimeIsEqualOrGreaterThan, ref time))
            {
                _useWhenIntuitionExceeds = Math.Max(0, Math.Min(time, 999));
                Service.Save();
            }
        }
    };

    public override int Priority { get; set; } = 1;
    public override bool IsExcludedPriority { get; set; } = false;
}