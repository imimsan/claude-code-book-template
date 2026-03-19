using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using HealPlan.Models;
using HealPlan.Storage;

namespace HealPlan;

/// <summary>
/// 戦闘中のアクション使用を記録するクラス。
/// <list type="bullet">
///   <item>DutyState イベントでプル開始・終了（ワイプ／クリア）を追跡する。</item>
///   <item>UseAction 関数フックでローカルプレイヤーのアクションをキャプチャする。</item>
/// </list>
/// </summary>
public class CombatRecorder : IDisposable
{
    // -----------------------------------------------------------------------
    // UseAction フック
    // -----------------------------------------------------------------------

    /// <summary>
    /// ActionManager::UseAction のデリゲート。
    /// 引数は FFXIVClientStructs の定義に準拠。
    /// </summary>
    private delegate bool UseActionDelegate(
        nint  actionManager,      // ActionManager*
        uint  actionType,         // ActionType enum (1 = Action)
        uint  actionId,           // アクション行 ID
        ulong targetId,           // ターゲットの GameObjectID
        uint  extraParam,
        uint  useActionMode,
        uint  comboRouteId,
        nint  outOptAreaTargeted); // bool* — nint で受け取り unsafe 不要にする

    /// <summary>
    /// UseAction のシグネチャ。
    /// ゲームパッチで変わる場合があります。最新値は
    /// https://github.com/aers/FFXIVClientStructs を参照してください。
    /// </summary>
    private const string UseActionSignature = "E8 ?? ?? ?? ?? 88 85 ?? ?? ?? ?? 83 EB 01";

    private readonly Hook<UseActionDelegate>? _useActionHook;

    // -----------------------------------------------------------------------
    // 依存サービス
    // -----------------------------------------------------------------------
    private readonly IDutyState   _dutyState;
    private readonly IClientState _clientState;
    private readonly IPluginLog   _log;
    private readonly ZoneStorage  _storage;
    private readonly IChatGui     _chatGui;

    // -----------------------------------------------------------------------
    // 状態
    // -----------------------------------------------------------------------
    private bool        _inPull;
    private DateTime    _pullStartTime;
    private PullRecord? _currentPull;

    // -----------------------------------------------------------------------
    // イベント
    // -----------------------------------------------------------------------

    /// <summary>プルが保存されたときに発火する。引数はゾーン ID。</summary>
    public event Action<uint>? PullCompleted;

    // -----------------------------------------------------------------------
    // コンストラクタ
    // -----------------------------------------------------------------------
    public CombatRecorder(
        IDutyState            dutyState,
        IClientState          clientState,
        IGameInteropProvider  gameInterop,
        IPluginLog            log,
        IChatGui              chatGui,
        ZoneStorage           storage)
    {
        _dutyState   = dutyState;
        _clientState = clientState;
        _log         = log;
        _chatGui     = chatGui;
        _storage     = storage;

        // UseAction フックの設定
        try
        {
            _useActionHook = gameInterop.HookFromSignature<UseActionDelegate>(
                UseActionSignature, UseActionDetour);
            _useActionHook.Enable();
            _log.Information("[HealPlan] UseAction フックを設定しました");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[HealPlan] UseAction フックの設定に失敗。シグネチャを確認してください。");
            _chatGui.PrintError("[HealPlan] フック失敗 - アクション記録無効");
        }

        _dutyState.DutyStarted   += OnDutyStarted;
        _dutyState.DutyCompleted += OnDutyCompleted;
        _dutyState.DutyWiped     += OnDutyWiped;
    }

    // -----------------------------------------------------------------------
    // DutyState ハンドラ
    // -----------------------------------------------------------------------
    private void OnDutyStarted(object? sender, ushort territoryId)   => StartPull(territoryId);
    private void OnDutyCompleted(object? sender, ushort territoryId) => EndPull(save: true);
    private void OnDutyWiped(object? sender, ushort territoryId)     => EndPull(save: true);

    private void StartPull(ushort zoneId)
    {
        _inPull        = true;
        _pullStartTime = DateTime.UtcNow;
        _currentPull   = new PullRecord
        {
            ZoneId        = zoneId,
            PullStartedAt = _pullStartTime,
        };
        _log.Information($"[HealPlan] プル開始 ゾーン:{zoneId}");
        _chatGui.Print("[HealPlan] プル開始");
    }

    private void EndPull(bool save)
    {
        if (!_inPull || _currentPull == null)
            return;

        _inPull = false;

        var pullDuration = (DateTime.UtcNow - _currentPull.PullStartedAt).TotalSeconds;
        if (save && pullDuration >= 5.0)
        {
            var zoneId  = _currentPull.ZoneId;
            var records = _storage.LoadZone(zoneId);
            records.Add(_currentPull);
            _storage.SaveZone(zoneId, records);
            _log.Information($"[HealPlan] プル保存 ({_currentPull.Actions.Count} アクション)");
            _chatGui.Print($"[HealPlan] {_currentPull.Actions.Count} アクション保存");
            PullCompleted?.Invoke(zoneId);
        }

        _currentPull = null;
    }

    // -----------------------------------------------------------------------
    // UseAction デトゥール
    // -----------------------------------------------------------------------
    private bool UseActionDetour(
        nint  actionManager,
        uint  actionType,
        uint  actionId,
        ulong targetId,
        uint  extraParam,
        uint  useActionMode,
        uint  comboRouteId,
        nint  outOptAreaTargeted)
    {
        // オリジナルを先に呼び出す
        var result = _useActionHook!.Original(
            actionManager, actionType, actionId, targetId,
            extraParam, useActionMode, comboRouteId, outOptAreaTargeted);

        // プル中・アクション成功・通常アクション（actionType == 1）のみ記録
        if (result && _inPull && _currentPull != null && actionType == 1)
        {
            var elapsed = (float)(DateTime.UtcNow - _pullStartTime).TotalSeconds;
            _currentPull.Actions.Add(new ActionEntry
            {
                Time     = elapsed,
                ActionId = actionId,
                ActorId  = "player",
            });
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // プロパティ（Plugin.cs から参照）
    // -----------------------------------------------------------------------
    public bool   IsInPull       => _inPull;
    public float  CurrentPullTime => _inPull
        ? (float)(DateTime.UtcNow - _pullStartTime).TotalSeconds
        : 0f;
    public ushort CurrentZoneId  => _clientState.TerritoryType;

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------
    public void Dispose()
    {
        _dutyState.DutyStarted   -= OnDutyStarted;
        _dutyState.DutyCompleted -= OnDutyCompleted;
        _dutyState.DutyWiped     -= OnDutyWiped;
        _useActionHook?.Disable();
        _useActionHook?.Dispose();
    }
}
