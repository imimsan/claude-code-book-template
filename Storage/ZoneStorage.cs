using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;
using HealPlan.Models;

namespace HealPlan.Storage;

/// <summary>
/// ゾーンごとのプル記録を JSON ファイルで管理する。
/// ファイルパス: {configDir}/zones/{zoneId}.json
/// </summary>
public class ZoneStorage
{
    private readonly string _zonesDir;
    private readonly IPluginLog _log;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public ZoneStorage(string configDir, IPluginLog log)
    {
        _zonesDir = Path.Combine(configDir, "zones");
        _log = log;
        Directory.CreateDirectory(_zonesDir);
    }

    private string GetFilePath(uint zoneId) =>
        Path.Combine(_zonesDir, $"{zoneId}.json");

    public List<PullRecord> LoadZone(uint zoneId)
    {
        var path = GetFilePath(zoneId);
        if (!File.Exists(path))
            return new List<PullRecord>();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<PullRecord>>(json, JsonOptions)
                   ?? new List<PullRecord>();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"[HealPlan] ゾーン {zoneId} のデータ読み込みに失敗");
            return new List<PullRecord>();
        }
    }

    public void SaveZone(uint zoneId, List<PullRecord> records)
    {
        var path = GetFilePath(zoneId);
        try
        {
            var json = JsonSerializer.Serialize(records, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[HealPlan] ゾーン {zoneId} のデータ保存に失敗");
        }
    }

    public void DeleteZone(uint zoneId)
    {
        var path = GetFilePath(zoneId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public List<uint> GetSavedZoneIds()
    {
        var result = new List<uint>();
        foreach (var file in Directory.GetFiles(_zonesDir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (uint.TryParse(name, out var id))
                result.Add(id);
        }
        return result;
    }
}
