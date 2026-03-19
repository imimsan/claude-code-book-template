using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using HealPlan.Models;
using HealPlan.Storage;
using HealPlan.Windows;

namespace HealPlan;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] private IDalamudPluginInterface PluginInterface { get; init; } = null!;
    [PluginService] private ICommandManager         CommandManager  { get; init; } = null!;
    [PluginService] private IDutyState              DutyState       { get; init; } = null!;
    [PluginService] private IClientState            ClientState     { get; init; } = null!;
    [PluginService] private IGameInteropProvider    GameInterop     { get; init; } = null!;
    [PluginService] private IDataManager            DataManager     { get; init; } = null!;
    [PluginService] private IPluginLog              Log             { get; init; } = null!;
    [PluginService] private IChatGui               ChatGui         { get; init; } = null!;

    private const string CommandName = "/healplan";

    private readonly Configuration  _config;
    private readonly ZoneStorage    _storage;
    private readonly CombatRecorder _recorder;
    private readonly PlanAnalyzer   _analyzer;

    private readonly WindowSystem    _windowSystem = new("HealPlan");
    private readonly MainWindow      _mainWindow;
    private readonly TimelineWindow  _timelineWindow;

    // 推奨リストのキャッシュ（プル完了またはゾーン変化時に更新）
    private List<RecommendedAction> _cachedRecommendations = new();
    private uint _cachedZoneId;

    public Plugin()
    {
        _config   = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _storage  = new ZoneStorage(PluginInterface.GetPluginConfigDirectory(), Log);
        _recorder = new CombatRecorder(DutyState, ClientState, GameInterop, Log, ChatGui, _storage);
        _analyzer = new PlanAnalyzer(_storage, DataManager, Log);

        // プル完了時に推奨リストを再生成する
        _recorder.PullCompleted += OnPullCompleted;

        _mainWindow     = new MainWindow(_config, _storage, PluginInterface, Log);
        _timelineWindow = new TimelineWindow();
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_timelineWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "HealPlan の設定ウィンドウを開きます。debug: ダミープルを注入します"
        });

        PluginInterface.UiBuilder.Draw      += OnDraw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    // -----------------------------------------------------------------------
    // コマンド・UI コールバック
    // -----------------------------------------------------------------------
    private void OnCommand(string command, string args)
    {
        if (args.Trim() == "debug")
        {
            InjectDebugPulls();
            return;
        }
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
    }

    private void InjectDebugPulls()
    {
        var zoneId  = _recorder.CurrentZoneId;
        var records = _storage.LoadZone(zoneId);
        var now     = DateTime.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            var pull = new PullRecord
            {
                ZoneId        = zoneId,
                PullStartedAt = now.AddMinutes(-10 + i * 3),
                Actions       = new List<ActionEntry>
                {
                    new() { Time = 5f,  ActionId = 16536, ActorId = "player" },
                    new() { Time = 20f, ActionId = 16537, ActorId = "player" },
                    new() { Time = 45f, ActionId = 16538, ActorId = "player" },
                },
            };
            records.Add(pull);
        }
        _storage.SaveZone(zoneId, records);
        RefreshRecommendations(zoneId);
        ChatGui.Print($"[HealPlan] デバッグ: ゾーン {zoneId} に 3 件のダミープルを注入しました");
    }

    private void OpenMainUi() =>
        _mainWindow.IsOpen = true;

    // -----------------------------------------------------------------------
    // 毎フレーム描画
    // -----------------------------------------------------------------------
    private void OnDraw()
    {
        if (_recorder.IsInPull && _config.IsOverlayEnabled)
        {
            var zoneId = _recorder.CurrentZoneId;

            // ゾーンが変わった場合はキャッシュを更新する
            if (zoneId != _cachedZoneId)
                RefreshRecommendations(zoneId);

            _timelineWindow.UpdateTimeline(_cachedRecommendations, _recorder.CurrentPullTime);
            _timelineWindow.IsOpen = true;
        }
        else
        {
            _timelineWindow.IsOpen = false;
        }

        _windowSystem.Draw();
    }

    // -----------------------------------------------------------------------
    // 推奨リストのキャッシュ更新
    // -----------------------------------------------------------------------
    private void OnPullCompleted(uint zoneId) =>
        RefreshRecommendations(zoneId);

    private void RefreshRecommendations(uint zoneId)
    {
        _cachedZoneId          = zoneId;
        _cachedRecommendations = _analyzer.Analyze(zoneId, _config.BucketSize, _config.MinPullsRequired);
        Log.Debug($"[HealPlan] 推奨リスト更新: {_cachedRecommendations.Count} 件");
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------
    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw      -= OnDraw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;

        _recorder.PullCompleted -= OnPullCompleted;
        _recorder.Dispose();

        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _timelineWindow.Dispose();
    }
}
