using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using HealPlan.Models;
using HealPlan.Storage;

namespace HealPlan;

/// <summary>
/// 過去プルのデータを時間軸バケツで集約し、推奨ヒールタイムラインを生成する。
///
/// アルゴリズム:
///   1. 同ゾーンの全プルを読み込む。
///   2. 各アクションを floor(time / bucketSize) でバケツ化する。
///   3. (バケツ, actionId) の組み合わせを「そのプルで使ったか否か」で重複排除してカウント。
///   4. 各バケツで最も使用率の高い actionId を推奨アクションとして抽出する。
/// </summary>
public class PlanAnalyzer
{
    private readonly ZoneStorage  _storage;
    private readonly IDataManager _dataManager;
    private readonly IPluginLog   _log;

    public PlanAnalyzer(ZoneStorage storage, IDataManager dataManager, IPluginLog log)
    {
        _storage     = storage;
        _dataManager = dataManager;
        _log         = log;
    }

    /// <summary>
    /// 指定ゾーンの過去プルを集約して推奨アクションリストを返す。
    /// プル数が <paramref name="minPulls"/> 未満の場合は空リストを返す。
    /// </summary>
    public List<RecommendedAction> Analyze(uint zoneId, float bucketSize, int minPulls)
    {
        var records    = _storage.LoadZone(zoneId);
        var totalPulls = records.Count;

        if (totalPulls < minPulls)
        {
            _log.Debug($"[HealPlan] プル数不足: {totalPulls}/{minPulls} (ゾーン {zoneId})");
            return new List<RecommendedAction>();
        }

        // (bucket, actionId) → プル出現回数
        var bucketCounts = new Dictionary<(int bucket, uint actionId), int>();

        foreach (var pull in records)
        {
            // 同一プル内では (bucket, actionId) を 1 回のみカウント
            var seen = new HashSet<(int, uint)>();
            foreach (var action in pull.Actions)
            {
                var bucket = (int)Math.Floor(action.Time / bucketSize);
                var key    = (bucket, action.ActionId);
                if (seen.Add(key))
                {
                    bucketCounts.TryGetValue(key, out var cnt);
                    bucketCounts[key] = cnt + 1;
                }
            }
        }

        // 各バケツで最多アクションを抽出
        var recommendations = bucketCounts
            .GroupBy(kv => kv.Key.bucket)
            .Select(g =>
            {
                var best         = g.OrderByDescending(kv => kv.Value).First();
                var bucketCenter = (best.Key.bucket + 0.5f) * bucketSize;
                return new RecommendedAction
                {
                    Time       = bucketCenter,
                    ActionId   = best.Key.actionId,
                    ActionName = GetActionName(best.Key.actionId),
                    IconId     = GetIconId(best.Key.actionId),
                    Count      = best.Value,
                    Total      = totalPulls,
                };
            })
            .OrderBy(r => r.Time)
            .ToList();

        _log.Debug($"[HealPlan] 集約完了: {recommendations.Count} 件の推奨 (ゾーン {zoneId}, {totalPulls} プル)");
        return recommendations;
    }

    private string GetActionName(uint actionId)
    {
        try
        {
            var row  = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(actionId);
            var name = row?.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"[HealPlan] アクション名取得失敗: {actionId}");
        }
        return $"Action#{actionId}";
    }

    private uint GetIconId(uint actionId)
    {
        try
        {
            var row = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(actionId);
            if (row.HasValue)
                return row.Value.Icon;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"[HealPlan] アイコンID取得失敗: {actionId}");
        }
        return 0;
    }
}
