using System;

namespace HealPlan.Models;

/// <summary>
/// 1回のアクション使用記録。
/// </summary>
[Serializable]
public class ActionEntry
{
    /// <summary>プル開始からの経過秒数</summary>
    public float Time { get; set; }

    /// <summary>FFXIV アクション ID</summary>
    public uint ActionId { get; set; }

    /// <summary>使用者 ID。ローカルプレイヤーの場合は "player"</summary>
    public string ActorId { get; set; } = "player";
}
