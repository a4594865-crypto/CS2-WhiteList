using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities; // 修正：新增此行以支援 SteamID
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace WhiteList;

public partial class WhiteList : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "WhiteList";
    public override string ModuleDescription => "Allow or block players from a list on database or file";
    public override string ModuleAuthor => "1MaaaaaacK";
    public override string ModuleVersion => "1.0.5";
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

        // 註冊玩家授權監聽器
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

    // 處理白名單切換
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

    // 修正：確保 CheckFile 只有這一個定義，避免重複定義錯誤
    public void CheckFile()
    {
        string filePath = Path.Combine(ModuleDirectory, "whitelist.txt");

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
            Logger.LogInformation("[WhiteList] 建立新的 whitelist.txt 檔案。");
            return;
        }

        try
        {
            // 讀取並清理每一行，確保沒有 \r 或多餘空格，且過濾非數字行
            WhiteListValues = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.All(char.IsDigit))
                .ToArray();

            Logger.LogInformation($"[WhiteList] 成功載入 {WhiteListValues.Length} 個 SteamID。");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[WhiteList] 讀取檔案失敗: {ex.Message}");
        }
    }

    // 玩家驗證邏輯
    public void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        if (!Config.Enabled) return;

        string playerSteamId64 = steamId.SteamId64.ToString();
        bool isInList = WhiteListValues.Contains(playerSteamId64);

        // 判斷是否需要踢出 (白名單模式下不在名單中，或黑名單模式下在名單中)
        if ((!Config.UseAsBlacklist && !isInList) || (Config.UseAsBlacklist && isInList))
        {
            Logger.LogInformation($"[WhiteList] 踢出玩家: {playerSteamId64} (Slot: {playerSlot})");
            // 注意：請確保翻譯檔中有 KickReason，或直接改為字串
            Server.ExecuteCommand($"kickid {playerSlot} \"{Localizer["KickReason"]}\"");
        }
    }

    public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
    public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
