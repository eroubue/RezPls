using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using RezPls.Managers;

namespace RezPls.GUI;

public class Interface : IDisposable
{
    public const string PluginName = "RezPls";

    private readonly string _configHeader;
    private readonly RezPls _plugin;

    private          string          _statusFilter = string.Empty;
    private readonly HashSet<string> _seenNames;

    public bool Visible;

    public bool TestMode = false;

    private static void ChangeAndSave<T>(T value, T currentValue, Action<T> setter) where T : IEquatable<T>
    {
        if (value.Equals(currentValue))
            return;

        setter(value);
        RezPls.Config.Save();
    }

    public Interface(RezPls plugin)
    {
        _plugin       = plugin;
        _configHeader = RezPls.Version.Length > 0 ? $"{PluginName} v{RezPls.Version}###{PluginName}" : PluginName;
        _seenNames    = new HashSet<string>(_plugin.StatusSet.DisabledStatusSet.Count + _plugin.StatusSet.EnabledStatusSet.Count);

        Dalamud.PluginInterface.UiBuilder.Draw         += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi += Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   += Enable;
    }

    private static void DrawCheckbox(string name, string tooltip, bool value, Action<bool> setter)
    {
        var tmp = value;
        if (ImGui.Checkbox(name, ref tmp))
            ChangeAndSave(tmp, value, setter);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawEnabledCheckbox()
        => DrawCheckbox("启用", "启用或禁用插件。", RezPls.Config.Enabled, e =>
        {
            RezPls.Config.Enabled = e;
            if (e)
                _plugin.Enable();
            else
                _plugin.Disable();
        });

    private void DrawHideSymbolsOnSelfCheckbox()
        => DrawCheckbox("隐藏自身符号", "隐藏绘制在玩家角色身上的符号和/或文本。",
            RezPls.Config.HideSymbolsOnSelf,    e => RezPls.Config.HideSymbolsOnSelf = e);

    private void DrawShowCastProgressCheckbox()
        => DrawCheckbox("显示施法进度", "显示复活施法相对于总施法时间的进度。仅适用于填充施法框样式。",
            RezPls.Config.ShowCastProgress,   e => RezPls.Config.ShowCastProgress = e);

    private void DrawEnabledRaiseCheckbox()
        => DrawCheckbox("启用复活高亮",
            "高亮显示正在被复活的玩家。", RezPls.Config.EnabledRaise, e => RezPls.Config.EnabledRaise = e);

    private void DrawShowGroupCheckbox()
        => DrawCheckbox("小队框架高亮",
            "根据您的颜色和状态选择在小队框架中高亮显示玩家。",
            RezPls.Config.ShowGroupFrame,
            e => RezPls.Config.ShowGroupFrame = e);

    private void DrawShowAllianceCheckbox()
        => DrawCheckbox("团队框架高亮",
            "根据您的颜色和状态选择在团队框架中高亮显示玩家。",
            RezPls.Config.ShowAllianceFrame,
            e => RezPls.Config.ShowAllianceFrame = e);

    private void DrawShowCasterNamesCheckbox()
        => DrawCheckbox("显示施法者姓名",
            "高亮显示玩家时，同时在框架中显示正在复活或净化他们的施法者姓名。",
            RezPls.Config.ShowCasterNames,
            e => RezPls.Config.ShowCasterNames = e);

    private void DrawShowIconCheckbox()
        => DrawCheckbox("显示世界图标",
            "在已复活或正在被复活的尸体上绘制复活图标。", RezPls.Config.ShowIcon,
            e => RezPls.Config.ShowIcon = e);

    private void DrawShowIconDispelCheckbox()
        => DrawCheckbox("显示世界图标##净化",
            "在有可移除负面状态效果的玩家身上绘制减益图标。", RezPls.Config.ShowIconDispel,
            e => RezPls.Config.ShowIconDispel = e);

    private void DrawShowInWorldTextCheckbox()
        => DrawCheckbox("显示世界文本",
            "在正在被复活的尸体下方显示当前复活者，或显示已被复活。",
            RezPls.Config.ShowInWorldText,
            e => RezPls.Config.ShowInWorldText = e);

    private void DrawShowInWorldTextDispelCheckbox()
        => DrawCheckbox("显示世界文本##净化",
            "在正在被净化的受影响玩家下方显示当前施法者，或显示其有可移除的负面状态效果。",
            RezPls.Config.ShowInWorldTextDispel,
            e => RezPls.Config.ShowInWorldTextDispel = e);

    private void DrawRestrictJobsCheckbox()
        => DrawCheckbox("限制复活职业",
            "仅当您是具有固有复活能力的职业时才显示复活信息。\n"
          + "幻术师、白魔法师、秘术师、学者、召唤师、占星术士、青魔法师、赤魔法师（64级+）。"
          + "忽略遗失技能和理符技能。\n", RezPls.Config.RestrictedJobs,
            e => RezPls.Config.RestrictedJobs = e);

    private void DrawDispelHighlightingCheckbox()
        => DrawCheckbox("启用净化高亮",
            "高亮显示有可移除负面状态效果的玩家。",
            RezPls.Config.EnabledDispel, e => RezPls.Config.EnabledDispel = e);

    private void DrawRestrictJobsDispelCheckbox()
        => DrawCheckbox("限制净化职业",
            "仅当您是具有固有净化能力的职业时才显示净化信息。\n"
          + "幻术师、白魔法师、学者、占星术士、吸游诗人（35级+）、青魔法师",
            RezPls.Config.RestrictedJobsDispel, e => RezPls.Config.RestrictedJobsDispel = e);

    private void DrawTestModeCheckBox1()
        => DrawCheckbox("测试玩家已复活", "应在玩家角色和小队框架上显示活跃的\"已复活\"效果。",
            ActorWatcher.TestMode == 1,       e => ActorWatcher.TestMode = e ? 1 : 0);

    private void DrawTestModeCheckBox2()
        => DrawCheckbox("测试玩家被目标复活",
            "应在玩家角色和小队框架上显示活跃的\"正在被复活\"效果，就像施法者是其当前目标一样。",
            ActorWatcher.TestMode == 2, e => ActorWatcher.TestMode = e ? 2 : 0);

    private void DrawTestModeCheckBox3()
        => DrawCheckbox("测试玩家无用复活",
            "应在玩家角色上显示活跃的\"无用复活\"效果，就像玩家角色和其当前目标都在复活它一样。",
            ActorWatcher.TestMode == 3, e => ActorWatcher.TestMode = e ? 3 : 0);

    private void DrawTestModeCheckBox4()
        => DrawCheckbox("测试玩家负面状态效果",
            "应在玩家角色上显示活跃的\"有被监控状态效果\"效果，就像玩家角色有被监控的状态一样。",
            ActorWatcher.TestMode == 4, e => ActorWatcher.TestMode = e ? 4 : 0);

    private void DrawTestModeCheckBox5()
        => DrawCheckbox("测试玩家负面状态正在被净化",
            "应在玩家角色上显示活跃的\"正在被净化\"效果，就像它正在被其当前目标净化一样。",
            ActorWatcher.TestMode == 5, e => ActorWatcher.TestMode = e ? 5 : 0);

    private void DrawTestModeCheckBox6()
        => DrawCheckbox("测试玩家无用净化",
            "应在玩家角色上显示活跃的\"无用净化\"效果，就像使用双重净化或对没有被监控状态的目标使用净化一样。",
            ActorWatcher.TestMode == 6, e => ActorWatcher.TestMode = e ? 6 : 0);


    private void DrawSingleStatusEffectList(string header, bool which, float width)
    {
        using var group = ImRaii.Group();
        var       list  = which ? _plugin.StatusSet.DisabledStatusSet : _plugin.StatusSet.EnabledStatusSet;
        _seenNames.Clear();
        if (ImGui.BeginListBox($"##{header}box", width / 2 * Vector2.UnitX))
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var (status, name) = list[i];
                if (!name.Contains(_statusFilter) || _seenNames.Contains(name))
                    continue;

                _seenNames.Add(name);
                if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                {
                    _plugin.StatusSet.Swap((ushort)status.RowId);
                    --i;
                }
            }

            ImGui.EndListBox();
        }

        if (which)
        {
            if (ImGui.Button("禁用所有状态", width / 2 * Vector2.UnitX))
                _plugin.StatusSet.ClearEnabledList();
        }
        else if (ImGui.Button("启用所有状态", width / 2 * Vector2.UnitX))
        {
            _plugin.StatusSet.ClearDisabledList();
        }
    }

    private static void DrawStatusSelectorTitles(float width)
    {
        const string disabledHeader = "已禁用状态";
        const string enabledHeader  = "被监控状态";
        var          pos1           = width / 4 - ImGui.CalcTextSize(disabledHeader).X / 2;
        var          pos2           = 3 * width / 4 + ImGui.GetStyle().ItemSpacing.X - ImGui.CalcTextSize(enabledHeader).X / 2;
        ImGui.SetCursorPosX(pos1);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(disabledHeader);
        ImGui.SameLine(pos2);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(enabledHeader);
    }

    private void DrawStatusEffectList()
    {
        var width = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetStyle().ItemSpacing.X;
        DrawStatusSelectorTitles(width);
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##statusFilter", "过滤...", ref _statusFilter, 64);
        DrawSingleStatusEffectList("已禁用状态", true, width);
        ImGui.SameLine();
        DrawSingleStatusEffectList("被监控状态", false, width);
    }


    private void DrawColorPicker(string name, string tooltip, uint value, uint defaultValue, Action<uint> setter)
    {
        const ImGuiColorEditFlags flags = ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs;

        var tmp = ImGui.ColorConvertU32ToFloat4(value);
        if (ImGui.ColorEdit4($"##{name}", ref tmp, flags))
            ChangeAndSave(ImGui.ColorConvertFloat4ToU32(tmp), value, setter);
        ImGui.SameLine();
        if (ImGui.Button($"默认##{name}"))
            ChangeAndSave(defaultValue, value, setter);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                $"重置为默认值: #{defaultValue & 0xFF:X2}{(defaultValue >> 8) & 0xFF:X2}{(defaultValue >> 16) & 0xFF:X2}{defaultValue >> 24:X2}");
        ImGui.SameLine();
        ImGui.Text(name);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawCurrentRaiseColorPicker()
        => DrawColorPicker("正在被复活",
            "正在被其他玩家或仅被您复活的玩家的高亮颜色。",
            RezPls.Config.CurrentlyRaisingColor, RezPlsConfig.DefaultCurrentlyRaisingColor, c => RezPls.Config.CurrentlyRaisingColor = c);


    private void DrawAlreadyRaisedColorPicker()
        => DrawColorPicker("已复活",
            "已被复活且当前不被您复活的玩家的高亮颜色。",
            RezPls.Config.RaisedColor, RezPlsConfig.DefaultRaisedColor, c => RezPls.Config.RaisedColor = c);

    private void DrawDoubleRaiseColorPicker()
        => DrawColorPicker("冗余施法",
            "如果您正在复活的玩家已被复活或其他人也在复活他们，\n"
          + "如果您和其他玩家同时净化他们，或如果您净化没有被监控负面状态效果的目标时的高亮颜色。",
            RezPls.Config.DoubleRaiseColor, RezPlsConfig.DefaultDoubleRaiseColor, c => RezPls.Config.DoubleRaiseColor = c);

    private void DrawInWorldBackgroundColorPicker()
        => DrawColorPicker("世界背景",
            "绘制在尸体上的复活文本的背景颜色。",
            RezPls.Config.InWorldBackgroundColor, RezPlsConfig.DefaultInWorldBackgroundColorRaise,
            c => RezPls.Config.InWorldBackgroundColor = c);

    private void DrawInWorldBackgroundColorPickerDispel()
        => DrawColorPicker("世界背景（净化）",
            "绘制在受被监控负面状态效果影响的角色身上的文本的背景颜色。",
            RezPls.Config.InWorldBackgroundColorDispel, RezPlsConfig.DefaultInWorldBackgroundColorDispel,
            c => RezPls.Config.InWorldBackgroundColorDispel = c);

    private void DrawDispellableColorPicker()
        => DrawColorPicker("有被监控状态效果",
            "有任何被监控负面状态效果的玩家的高亮颜色。",
            RezPls.Config.DispellableColor, RezPlsConfig.DefaultDispellableColor, c => RezPls.Config.DispellableColor = c);

    private void DrawCurrentlyDispelledColorPicker()
        => DrawColorPicker("正在被净化",
            "正在被其他玩家或仅被您净化的玩家的高亮颜色。",
            RezPls.Config.CurrentlyDispelColor, RezPlsConfig.DefaultCurrentlyDispelColor, c => RezPls.Config.CurrentlyDispelColor = c);

    private void DrawScaleButton()
    {
        const float min  = 0.1f;
        const float max  = 3.0f;
        const float step = 0.005f;

        var tmp = RezPls.Config.IconScale;
        if (ImGui.DragFloat("世界图标缩放", ref tmp, step, min, max))
            ChangeAndSave(tmp, RezPls.Config.IconScale, f => RezPls.Config.IconScale = Math.Max(min, Math.Min(f, max)));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("设置绘制在已复活尸体上的复活图标的缩放比例。");
    }

    private static readonly string[] RectTypeStrings = new[]
    {
        "填充",
        "仅轮廓",
        "仅完全不透明轮廓",
        "填充和完全不透明轮廓",
    };

    private void DrawRectTypeSelector()
    {
        var type = (int)RezPls.Config.RectType;
        if (!ImGui.Combo("矩形类型", ref type, RectTypeStrings, RectTypeStrings.Length))
            return;

        ChangeAndSave(type, (int)RezPls.Config.RectType, t => RezPls.Config.RectType = (RectType)t);
    }

    public void Draw()
    {
        if (!Visible)
            return;

        var buttonHeight      = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
        var horizontalSpacing = new Vector2(0, ImGui.GetTextLineHeightWithSpacing());

        var height = 15 * buttonHeight
          + 6 * horizontalSpacing.Y
          + 27 * ImGui.GetStyle().ItemSpacing.Y;
        var width       = 450 * ImGui.GetIO().FontGlobalScale;
        var constraints = new Vector2(width, height);
        ImGui.SetNextWindowSizeConstraints(constraints, constraints);

        if (!ImGui.Begin(_configHeader, ref Visible, ImGuiWindowFlags.NoResize))
            return;

        try
        {
            DrawEnabledCheckbox();

            if (ImGui.CollapsingHeader("复活设置"))
            {
                DrawEnabledRaiseCheckbox();
                DrawRestrictJobsCheckbox();
                DrawShowIconCheckbox();
                DrawShowInWorldTextCheckbox();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("净化设置"))
            {
                DrawDispelHighlightingCheckbox();
                DrawRestrictJobsDispelCheckbox();
                DrawShowIconDispelCheckbox();
                DrawShowInWorldTextDispelCheckbox();
                ImGui.Dummy(horizontalSpacing);
                DrawStatusEffectList();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("通用设置"))
            {
                DrawShowCastProgressCheckbox();
                DrawHideSymbolsOnSelfCheckbox();
                DrawShowGroupCheckbox();
                DrawShowAllianceCheckbox();
                DrawShowCasterNamesCheckbox();
                DrawRectTypeSelector();
                DrawScaleButton();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("颜色"))
            {
                DrawCurrentRaiseColorPicker();
                DrawAlreadyRaisedColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDispellableColorPicker();
                DrawCurrentlyDispelledColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDoubleRaiseColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawInWorldBackgroundColorPicker();
                DrawInWorldBackgroundColorPickerDispel();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("测试"))
            {
                DrawTestModeCheckBox1();
                DrawTestModeCheckBox2();
                DrawTestModeCheckBox3();
                DrawTestModeCheckBox4();
                DrawTestModeCheckBox5();
                DrawTestModeCheckBox6();
            }

            DrawDebug();
        }
        finally
        {
            ImGui.End();
        }
    }

    [Conditional("DEBUG")]
    private void DrawDebug()
    {
        if (!ImGui.CollapsingHeader("调试"))
            return;

        ImGui.TextUnformatted($"在PVP中: {Dalamud.ClientState.IsPvP}");
        ImGui.TextUnformatted($"测试模式: {ActorWatcher.TestMode}");
        using (var tree = ImRaii.TreeNode("姓名"))
        {
            if (tree)
                foreach (var (id, name) in _plugin.ActorWatcher.ActorNames)
                    ImRaii.TreeNode($"{name} ({id})", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var tree = ImRaii.TreeNode("施法"))
        {
            if (tree)
                foreach (var (id, state) in _plugin.ActorWatcher.RezList)
                {
                    ImRaii.TreeNode($"{id}: {state.Type} 由 {state.Caster}, {(state.HasStatus ? "有状态" : string.Empty)}",
                        ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                }
        }
    }

    public void Enable()
        => Visible = true;

    public void Dispose()
    {
        Dalamud.PluginInterface.UiBuilder.Draw         -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   -= Enable;
    }
}
