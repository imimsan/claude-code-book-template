using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HealPlan.Storage;

namespace HealPlan.Windows;

/// <summary>
/// /healplan コマンドで開く設定・プル履歴ウィンドウ。
/// 「設定」タブと「プル履歴」タブの 2 タブ構成。
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly Configuration         _config;
    private readonly ZoneStorage           _storage;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog            _log;

    public MainWindow(
        Configuration          config,
        ZoneStorage            storage,
        IDalamudPluginInterface pluginInterface,
        IPluginLog             log)
        : base("HealPlan##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _config          = config;
        _storage         = storage;
        _pluginInterface = pluginInterface;
        _log             = log;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##healplan_tabs"))
        {
            if (ImGui.BeginTabItem("設定"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("プル履歴"))
            {
                DrawHistoryTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    // -----------------------------------------------------------------------
    // 設定タブ
    // -----------------------------------------------------------------------
    private void DrawSettingsTab()
    {
        ImGui.Spacing();

        // オーバーレイ有効/無効
        var overlayEnabled = _config.IsOverlayEnabled;
        if (ImGui.Checkbox("タイムラインオーバーレイを表示する", ref overlayEnabled))
        {
            _config.IsOverlayEnabled = overlayEnabled;
            _pluginInterface.SavePluginConfig(_config);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 最小プル数
        var minPulls = _config.MinPullsRequired;
        ImGui.Text("タイムライン表示に必要な最小プル数");
        ImGui.SetNextItemWidth(150);
        if (ImGui.SliderInt("##minPulls", ref minPulls, 1, 20))
        {
            _config.MinPullsRequired = minPulls;
            _pluginInterface.SavePluginConfig(_config);
        }
        ImGui.SameLine();
        ImGui.TextDisabled($"現在: {minPulls} 回");

        ImGui.Spacing();

        // バケツサイズ
        var bucketSize = _config.BucketSize;
        ImGui.Text("時間バケツ幅（秒）");
        ImGui.SetNextItemWidth(150);
        if (ImGui.SliderFloat("##bucketSize", ref bucketSize, 1f, 10f, "%.1f 秒"))
        {
            _config.BucketSize = bucketSize;
            _pluginInterface.SavePluginConfig(_config);
        }
        ImGui.SameLine();
        ImGui.TextDisabled("±この秒数でアクションをグルーピング");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("戦闘終了またはワイプ後、自動的にプルが保存されます。");
    }

    // -----------------------------------------------------------------------
    // プル履歴タブ
    // -----------------------------------------------------------------------
    private void DrawHistoryTab()
    {
        ImGui.Spacing();

        var zoneIds = _storage.GetSavedZoneIds();
        if (zoneIds.Count == 0)
        {
            ImGui.TextDisabled("記録されたプルはありません。");
            return;
        }

        foreach (var zoneId in zoneIds.OrderBy(z => z))
        {
            var records = _storage.LoadZone(zoneId);
            var header  = $"ゾーン {zoneId} — {records.Count} プル";

            if (!ImGui.CollapsingHeader(header))
                continue;

            ImGui.Indent();
            for (var i = 0; i < records.Count; i++)
            {
                var pull = records[i];
                ImGui.Text(
                    $"#{i + 1}  {pull.PullStartedAt.ToLocalTime():MM/dd HH:mm}  " +
                    $"({pull.Actions.Count} アクション)");
            }

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.7f));
            if (ImGui.Button($"ゾーン {zoneId} の記録を全て削除##del{zoneId}"))
            {
                _storage.DeleteZone(zoneId);
                _log.Information($"[HealPlan] ゾーン {zoneId} の記録を削除しました");
            }
            ImGui.PopStyleColor();
            ImGui.Unindent();
        }
    }

    public void Dispose() { }
}
