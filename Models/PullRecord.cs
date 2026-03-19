using System;
using System.Collections.Generic;

namespace HealPlan.Models;

/// <summary>
/// 1プル分の記録。ゾーン ID・開始時刻・使用アクション一覧を保持する。
/// </summary>
[Serializable]
public class PullRecord
{
    public uint ZoneId { get; set; }
    public DateTime PullStartedAt { get; set; }
    public List<ActionEntry> Actions { get; set; } = new();
}
