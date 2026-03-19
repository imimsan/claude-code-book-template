using System;
using Dalamud.Configuration;

namespace HealPlan;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // タイムライン表示に必要な最小プル数
    public int MinPullsRequired { get; set; } = 3;

    // オーバーレイ表示の有効/無効
    public bool IsOverlayEnabled { get; set; } = true;

    // バケツ幅（秒）: ±この秒数でアクションをグルーピングする
    public float BucketSize { get; set; } = 3.0f;
}
