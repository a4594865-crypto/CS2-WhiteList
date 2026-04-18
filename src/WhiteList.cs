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
    public override string ModuleVersion => "1.0.4"; // 建議微調版本號
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

        // 註冊監聽器
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

    // 核心修正：強健的檔案讀取邏輯
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
            Logger.LogInformation("[WhiteList] 找不到 whitelist.txt，已自動建立空白檔案。");
            return;
        }

        try
        {
            // 修正點：Trim() 移除 \r 換行符，Where 過濾掉空白行與非數字內容
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.All(char.IsDigit))
                .ToArray();

            Logger.LogInformation($"[WhiteList] 成功從檔案載入 {WhiteListValues.Length} 個 ID。");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[WhiteList] 讀取檔案時發生錯誤: {ex.Message}");
        }
    }

    // 玩家連線時的驗證邏輯
    public void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        if (!Config.Enabled) return;

        string playerSteamId64 = steamId.SteamId64.ToString();
        
        // 檢查該 ID 是否在清單中
        bool isInList = WhiteListValues.Contains(playerSteamId64);

        // 邏輯：白名單模式且不在名單內 -> 踢出
        // 或是：黑名單模式且在名單內 -> 踢出
        if ((!Config.UseAsBlacklist && !isInList) || (Config.UseAsBlacklist && isInList))
        {
            Logger.LogInformation($"[WhiteList] 拒絕玩家連線: {playerSteamId64} (原因: 不在白名單或在黑名單中)");
            Server.ExecuteCommand($"kickid {playerSlot} \"{Localizer["KickReason"]}\"");
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
        Logger.LogInformation($"Admin {player.PlayerName} toggled Whitelist to: {Config.Enabled}");
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
