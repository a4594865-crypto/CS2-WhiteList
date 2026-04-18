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
    public override string ModuleName => "WhiteList";
    public override string ModuleDescription => "Allow or block players from a list on database or file";
    public override string ModuleAuthor => "1MaaaaaacK";
    public override string ModuleVersion => "1.0.4"; // 更新版本號 
    [cite_start]public static int ConfigVersion => 3; [cite: 1]
    [cite_start]public string[] WhiteListValues = []; [cite: 1]

    public override void Load(bool hotReload)
    {
        [cite_start]if (Config.UsePrivateFeature) [cite: 1]
        {
            [cite_start]if (hotReload) [cite: 1]
            {
                [cite_start]Config.Enabled = Convar_isPluginEnabled.Value; [cite: 1]
                [cite_start]Config.UseAsBlacklist = Convar_useAsBlacklist.Value; [cite: 1]
            }
            Convar_isPluginEnabled.ValueChanged += (_, value) => { Config.Enabled = value; }; [cite: 1]
            Convar_useAsBlacklist.ValueChanged += (_, value) => { Config.UseAsBlacklist = value; }; [cite: 1]
        }

        [cite_start]// 註冊監聽器，具體實作程式碼位於 Events.cs 
        RegisterListener<OnClientAuthorized>(OnClientAuthorized);

        [cite_start]AddCommand($"css_{Config.Commands.Add}", "Add to list", Add); [cite: 1]
        [cite_start]AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove); [cite: 1]
        [cite_start]AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist); [cite: 1]

        [cite_start]CheckVersion(); [cite: 1]

        [cite_start]if (Config.UseDatabase) [cite: 1]
        {
            [cite_start]BuildDatabaseConnectionString(); [cite: 1]
            [cite_start]CheckDatabaseTables(); [cite: 1]
        }
        else
        {
            [cite_start]CheckFile(); [cite: 1]
        }
    }

    // 核心修正：處理多行讀取與不可見換行符號問題
    public void CheckFile()
    {
        [cite_start]string filePath = Path.Combine(ModuleDirectory, "whitelist.txt"); [cite: 1]

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
            return;
        }

        try
        {
            // 使用 ReadAllLines 並透過 Trim() 移除 \r 換行符（解決第二行失敗的主因）
            // 同時過濾掉空白行與標註文字（例如 ）
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim()) 
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => {
                    // 如果整行包含空格，嘗試取最後一部份（處理 7656... 這種格式）
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Last();
                })
                .Where(id => id.All(char.IsDigit)) // 確保最後留下的是純數字 ID
                .ToArray();

            [cite_start]Logger.LogInformation($"[WhiteList] 成功載入 {WhiteListValues.Length} 個 ID。"); [cite: 1]
        }
        catch (Exception ex)
        {
            [cite_start]Logger.LogError($"[WhiteList] 讀取失敗: {ex.Message}"); [cite: 1]
        }
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
    {
        [cite_start]if (player == null) return; [cite: 1]
        [cite_start]if (!AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission)) [cite: 1]
        {
            [cite_start]command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}"); [cite: 1]
            return;
        }

        [cite_start]Config.Enabled = !Config.Enabled; [cite: 1]
        string status = Config.Enabled ? "\x06開啟" : "\x02關閉"; [cite: 1]
        [cite_start]Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{player.PlayerName}\x01 將白名單設定：{status}"); [cite: 1]
    }

    [cite_start]public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true); [cite: 1]
    [cite_start]public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false); [cite: 1]
}
