using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HealPlan.Models;

namespace HealPlan.Windows;

/// <summary>
/// 戦闘中に推奨ヒールタイムラインをオーバーレイ表示するウィンドウ。
/// 現在時刻の前 2 秒〜後 30 秒の範囲にあるアクションを表示し、
/// 直近のアクションをオレンジでハイライトする。
/// </summary>
public class TimelineWindow : Window, IDisposable
{
    private List<RecommendedAction> _recommendations = new();
    private float _currentTime;

    /// <summary>現在時刻より先の表示秒数</summary>
    private const float LookaheadSeconds = 30f;

    /// <summary>この秒数以内に迫っているアクションをハイライト</summary>
    private const float HighlightThreshold = 5f;

    public TimelineWindow()
        : base(
            "HealPlan タイムライン##timeline",
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav)
    {
        IsOpen = false;
    }

    /// <summary>推奨リストと現在プル時刻を更新する（毎フレーム呼び出される）。</summary>
    public void UpdateTimeline(List<RecommendedAction> recommendations, float currentTime)
    {
        _recommendations = recommendations;
        _currentTime     = currentTime;
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowBgAlpha(0.85f);
    }

    public override void Draw()
    {
        // ヘッダ
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.2f, 1f), "HealPlan");
        ImGui.SameLine();
        ImGui.TextDisabled($"  T+{_currentTime:F0}s");
        ImGui.Separator();

        // 表示範囲: 2 秒前〜30 秒後
        var windowStart = _currentTime - 2f;
        var windowEnd   = _currentTime + LookaheadSeconds;

        var anyVisible = false;
        foreach (var rec in _recommendations)
        {
            if (rec.Time < windowStart || rec.Time > windowEnd)
                continue;

            anyVisible = true;
            DrawRow(rec);
        }

        if (!anyVisible)
            ImGui.TextDisabled("（推奨アクションなし）");
    }

    private void DrawRow(RecommendedAction rec)
    {
        var timeLeft = rec.Time - _currentTime;
        var isNear   = timeLeft is >= -2f and <= HighlightThreshold;
        var isPast   = timeLeft < -2f;

        // --- 時刻ラベル ---
        var timeColor = isNear  ? new Vector4(1f,   0.5f, 0.2f, 1f)
                      : isPast  ? new Vector4(0.5f, 0.5f, 0.5f, 0.7f)
                      :           new Vector4(0.8f, 0.9f, 1f,   1f);
        ImGui.TextColored(timeColor, $" T+{rec.Time:F0}s");

        // --- アクション名 ---
        ImGui.SameLine();
        var nameColor = isNear  ? new Vector4(1f,   0.8f, 0.3f, 1f)
                      : isPast  ? new Vector4(0.5f, 0.5f, 0.5f, 0.7f)
                      :           new Vector4(1f,   1f,   1f,   1f);
        ImGui.TextColored(nameColor, $"[{rec.ActionName}]");

        // --- 確度プログレスバー ---
        ImGui.SameLine();
        var barColor = rec.Confidence >= 0.8f ? new Vector4(0.2f, 0.9f, 0.3f, 0.8f)
                     : rec.Confidence >= 0.5f ? new Vector4(0.9f, 0.8f, 0.2f, 0.8f)
                     :                          new Vector4(0.7f, 0.7f, 0.7f, 0.6f);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
        ImGui.ProgressBar(rec.Confidence, new Vector2(80, 0), $"{rec.Count}/{rec.Total}");
        ImGui.PopStyleColor();
    }

    public void Dispose() { }
}
