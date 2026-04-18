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
    public override string ModuleVersion => "1.0.3";
    public static int ConfigVersion => 3;
    public string[] WhiteListValues = [];

    public override void Load(bool hotReload)
    {
        if (Config.UsePrivateFeature)
        {
            if (hotReload)
            {
                Config.Enabled = Convar_isPluginEnabled.Value;
                Config.UseAsBlacklist = Convar_useAsBlacklist.Value;
            }
            Convar_isPluginEnabled.ValueChanged += (_, value) => { Config.Enabled = value; };
            Convar_useAsBlacklist.ValueChanged += (_, value) => { Config.UseAsBlacklist = value; };
        }

        RegisterListener<OnClientAuthorized>(OnClientAuthorized);

        // 註冊指令
        AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
        AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
        
        // 額外註冊一個給控制台用的指令
        AddCommand("css_whitelist", "Toggle Whitelist", ToggleWhitelist);
        
        if (Config.Commands.Toggle != "whitelist")
        {
            AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist Custom", ToggleWhitelist);
        }

        CheckVersion();

        if (Config.UseDatabase)
        {
            BuildDatabaseConnectionString();
            CheckDatabaseTables();
        }
        else
        {
            // 執行下方唯一的 CheckFile 方法
            CheckFile();
        }
    }

    // 解決空格問題的關鍵：徹底清洗 ID 數據
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            Logger.LogWarning("[WhiteList] 找不到 whitelist.txt，已自動建立新檔案。");
            return;
        }

        // 1. ReadAllLines 讀取所有行
        // 2. Select(x => x.Trim()) 徹底洗掉每行前後的隱形空格與 \r 換行符號
        // 3. Where 濾掉沒內容的空行
        WhiteListValues = File.ReadAllLines(filePath)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        Logger.LogInformation($"[WhiteList] 讀取成功！名單內共有 {WhiteListValues.Length} 個有效的 ID。");
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
        {
            command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
            return;
        }

        [cite_start]// 修改 ConVar 值，讓 Load 裡的監聽器去改 Config.Enabled，避免死循環 
        Convar_isPluginEnabled.Value = !Convar_isPluginEnabled.Value;

        bool finalStatus = Convar_isPluginEnabled.Value;
        string status = finalStatus ? "\x06開啟" : "\x02關閉";
        string textStatus = finalStatus ? "開啟" : "關閉";
        
        command.ReplyToCommand($" [WhiteList] 功能已設定為：{textStatus}");

        string executor = player == null ? "伺服器控制台" : player.PlayerName;
        Server.PrintToChatAll($"\x01[\x0B 管理員 \x01] \x03{executor}\x01 將白名單設定：{status}");
        Logger.LogInformation($"{executor} toggled Whitelist to: {finalStatus}");
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
