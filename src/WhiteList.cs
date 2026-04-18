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
      // 這裡的監聽器會負責處理所有的開關邏輯
      Convar_isPluginEnabled.ValueChanged += (_, value) => { Config.Enabled = value; };
      Convar_useAsBlacklist.ValueChanged += (_, value) => { Config.UseAsBlacklist = value; };
    }

    RegisterListener<OnClientAuthorized>(OnClientAuthorized);

    // 建議：檢查 config.json 裡的 Toggle 是否也叫 whitelist，如果是，請註冊一個就好
    if (Config.Commands.Toggle != "whitelist")
    {
        AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist (Custom)", ToggleWhitelist);
    }
    AddCommand("css_whitelist", "Toggle Whitelist (Console)", ToggleWhitelist);
    
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

  [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
  public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
  {
    if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
      command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
      return;
    }

    // 關鍵修正：只改 ConVar 的值，讓 ValueChanged 監聽器去改 Config.Enabled
    Convar_isPluginEnabled.Value = !Convar_isPluginEnabled.Value;

    // 抓取修改後的最新狀態
    bool newState = Convar_isPluginEnabled.Value;
    string statusText = newState ? "開啟" : "關閉";
    string colorStatus = newState ? "\x06開啟" : "\x02關閉";

    command.ReplyToCommand($" [WhiteList] 功能已成功切換為：{statusText}");

    string executor = player == null ? "伺服器控制台" : player.PlayerName;
    Server.PrintToChatAll($" \x01[\x0B 管理員 \x01] \x03{executor}\x01 將白名單設定：{colorStatus}");
    
    Logger.LogInformation($"{executor} toggled Whitelist to: {newState}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
