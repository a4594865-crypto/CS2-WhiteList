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
            // 監聽器：當環境變數變動時同步給 Config.Enabled
            Convar_isPluginEnabled.ValueChanged += (_, value) => { Config.Enabled = value; };
            Convar_useAsBlacklist.ValueChanged += (_, value) => { Config.UseAsBlacklist = value; };
        }

        RegisterListener<OnClientAuthorized>(OnClientAuthorized);

        // 註冊指令
        AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
        AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
        
        // 註冊切換指令 (css_whitelist)，並支援控制台執行 (CLIENT_AND_SERVER)
        AddCommand("css_whitelist", "Toggle Whitelist", ToggleWhitelist);
        
        // 如果 config.json 設的指令不是 whitelist，則另外註冊一個
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
            // 呼叫下方唯一的 CheckFile 方法
            CheckFile();
        }
    }

    // --- 這裡只有一個 CheckFile，解決重複定義問題 ---
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            Logger.LogWarning("[WhiteList] 找不到 whitelist.txt，已自動建立新檔案。");
            return;
        }

        // 徹底清洗資料：移除前後空格、\r 換行符，並過濾掉空行
        WhiteListValues = File.ReadAllLines(filePath)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        Logger.LogInformation($"[WhiteList] 檔案讀取成功！目前名單內共有 {WhiteListValues.Length} 個 ID。");
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
    {
        // 如果是玩家執行，檢查權限
        if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
        {
            command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
            return;
        }

        // 修改 ConVar 值，讓 Load 裡的 ValueChanged 監聽器去改 Config.Enabled
        Convar_isPluginEnabled.Value = !Convar_isPluginEnabled.Value;

        bool finalStatus = Convar_isPluginEnabled.Value;
        string statusText = finalStatus ? "開啟" : "關閉";
        string colorStatus = finalStatus ? "\x06開啟" : "\x02關閉";
        
        command.ReplyToCommand($" [WhiteList] 功能目前已切換為：{statusText}");

        // 全服廣播
        string executor = player == null ? "伺服器控制台" : player.PlayerName;
        Server.PrintToChatAll($"\x01[\x0B 管理員 \x01] \x03{executor}\x01 將白名單設定：{colorStatus}");
        Logger.LogInformation($"{executor} toggled Whitelist to: {finalStatus}");
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
