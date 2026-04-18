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
    public override string ModuleVersion => "1.0.4";
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

        // 這裡只需要註冊，邏輯寫在 Events.cs
        RegisterListener<OnClientAuthorized>(OnClientAuthorized);

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

    // 核心修正：處理多行讀取與換行符問題
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
            // 1. 使用 ReadAllLines 拆分每一行
            // 2. Trim() 移除 \r 換行符（這是第二行失敗的主因）
            // 3. Where 過濾空白與非數字行
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.All(c => char.IsDigit(c) || c == 'S' || c == 'T' || c == 'E' || c == 'A' || c == 'M' || c == '_' || c == ':'))
                .ToArray();

            Logger.LogInformation($"[WhiteList] 成功載入 {WhiteListValues.Length} 個白名單項目。");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[WhiteList] 檔案讀取失敗: {ex.Message}");
        }
    }

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
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
