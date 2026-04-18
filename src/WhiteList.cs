using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace WhiteList;

public partial class WhiteList : BasePlugin, IPluginConfig<Config>
{
    // ... 模組定義 (ModuleName, Version 等) 保持不變 ...

    public override void Load(bool hotReload)
    {
        // ... Load 邏輯保持不變 ...

        // 這裡註冊監聽器，具體實作請保留在 Events.cs 中
        RegisterListener<OnClientAuthorized>(OnClientAuthorized);

        // ... 指令註冊 ...

        if (Config.UseDatabase)
        {
            BuildDatabaseConnectionString();
            CheckDatabaseTables();
        }
        else
        {
            CheckFile();
        }
    }

    // 唯一的 CheckFile 定義，請確保專案中沒有其他重複的 CheckFile
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
            return;
        }

        try
        {
            // 使用 ReadAllLines 並透過 Select(line => line.Trim()) 清除 \r 字元
            // 這能解決 whitelist.txt 第二行玩家無法進入的問題
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            Logger.LogInformation($"[WhiteList] 成功載入 {WhiteListValues.Length} 個 ID");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[WhiteList] 讀取失敗: {ex.Message}");
        }
    }
}
