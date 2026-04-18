using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Linq; // 必須引用，用於處理格式清理

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
        AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist);

        CheckVersion();

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

    // 核心修正：手動定義 CheckFile，徹底解決 whitelist.txt 的換行與隱藏字元問題
    private void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            return;
        }

        try
        {
            // 讀取所有行，並強制執行 Trim() 去掉所有 \r, \n 以及前後空白
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim()) 
                .Where(line => !string.IsNullOrWhiteSpace(line)) 
                .ToArray();

            Logger.LogInformation($"[WhiteList] 檔案載入成功！目前白名單內共有 {WhiteListValues.Length} 個 ID。");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[WhiteList] 讀取 whitelist.txt 時發生錯誤: {ex.Message}");
        }
    }

    // 處理白名單開關切換的方法
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (!AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
        {
            command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
            return;
        }

        Config.Enabled = !Config.Enabled;
        string status = Config.Enabled ? "\x06開啟" : "\x02關閉";
        
        Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{player.PlayerName}\x01 將白名單設定：{status}");
        Logger.LogInformation($"Admin {player.PlayerName} toggled Whitelist to: {Config.Enabled}");
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
