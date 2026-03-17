using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace SamplePlugin.Windows;

public class MainWindow : Window
{
    public MainWindow() : base("Sample Plugin##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(300, 150),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        ImGui.Text("Hello from Sample Plugin!");
        ImGui.Spacing();
        ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1.0f, 0.5f, 1.0f), "プラグインが動いています！");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text($"現在時刻: {DateTime.Now:HH:mm:ss}");
    }
}
