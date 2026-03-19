namespace HealPlan.Models;

/// <summary>
/// PlanAnalyzer が集約した「推奨ヒールアクション」1件。
/// </summary>
public class RecommendedAction
{
    /// <summary>バケツ中央時刻（秒）</summary>
    public float Time { get; set; }

    /// <summary>FFXIV アクション ID</summary>
    public uint ActionId { get; set; }

    /// <summary>表示用アクション名</summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>このバケツで使用されたプル数</summary>
    public int Count { get; set; }

    /// <summary>集計対象の総プル数</summary>
    public int Total { get; set; }

    /// <summary>確度（Count / Total）</summary>
    public float Confidence => Total > 0 ? (float)Count / Total : 0f;
}
