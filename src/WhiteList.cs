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

        AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
        AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
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
            // 這裡會呼叫下方唯一的一個 CheckFile 方法
            CheckFile();
        }
    }

    // --- 關鍵：全專案只能有這一個 CheckFile，刪除其他地方的同名方法 ---
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            return;
        }

        // 徹底消除空格、換行符 (\r) 與 空行的絕招
        WhiteListValues = File.ReadAllLines(filePath)
            .Select(x => x.Trim()) // 1. 把每一行前後的隱形空格和 \r 統統洗掉
            .Where(x => !string.IsNullOrWhiteSpace(x)) // 2. 只有內容不是空的才留下來
            .ToArray();

        Logger.LogInformation($"[WhiteList] 讀取完成，共有 {WhiteListValues.Length} 個 ID。");
    }

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
        {
            command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
            return;
        }

        Convar_isPluginEnabled.Value = !Convar_isPluginEnabled.Value;

        bool finalStatus = Convar_isPluginEnabled.Value;
        string statusText = finalStatus ? "開啟" : "關閉";
        string colorStatus = finalStatus ? "\x06開啟" : "\x02關閉";
        
        command.ReplyToCommand($" [WhiteList] 設定已改為：{statusText}");

        string executor = player == null ? "伺服器控制台" : player.PlayerName;
        Server.PrintToChatAll($"\x01[\x0B 管理員 \x01] \x03{executor}\x01 將白名單設定：{colorStatus}");
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
