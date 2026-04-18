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

    // --- 註冊指令區塊 ---
    
    // 1. 保留原本從 config.json 讀取的自訂指令 (例如 !whitelist)
    AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist (Custom)", ToggleWhitelist);

    // 2. 額外新增一個專門給伺服器後台使用的固定指令
    AddCommand("css_whitelist", "Toggle Whitelist (Global/Console)", ToggleWhitelist);

    // -------------------

    AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
    AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);

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

  // 修改：將 whoCanExecute 改為 CLIENT_AND_SERVER，讓黑視窗也能用
  [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
  public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
  {
    // 如果是玩家執行 (player 不為 null)，則檢查權限
    // 如果是伺服器控制台執行 (player 為 null)，則直接通過
    if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
      command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
      return;
    }

    // 執行切換邏輯
    Config.Enabled = !Config.Enabled;

    // 設定顯示顏色與文字
    string statusText = Config.Enabled ? "開啟" : "關閉";
    string colorStatus = Config.Enabled ? "\x06開啟" : "\x02關閉";
    
    // 回應執行指令的人 (會顯示在控制台或玩家聊天室)
    command.ReplyToCommand($" [WhiteList] 功能已切換為：{statusText}");

    // 全服廣播通知
    string executor = player == null ? "伺服器控制台" : player.PlayerName;
    Server.PrintToChatAll($" \x01[\x0B 管理員 \x01] \x03{executor}\x01 將白名單設定：{colorStatus}");
    
    Logger.LogInformation($"{executor} toggled Whitelist to: {Config.Enabled}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
