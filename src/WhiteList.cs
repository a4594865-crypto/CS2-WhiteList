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
    
    // 1. 遊戲內切換指令 (例如 css_toggle)
    AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist);
    
    // 2. 新增：專供控制台使用的指令 css_whitelist
    AddCommand("css_whitelist", "Console command to toggle whitelist", ToggleWhitelistConsole);

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

  // 遊戲內切換邏輯
  [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
  public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
  {
    if (player == null) return;

    if (!AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
      command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
      return;
    }

    ExecuteToggle("管理員 " + player.PlayerName);
  }

  // 控制台切換邏輯
  [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
  public void ToggleWhitelistConsole(CCSPlayerController? player, CommandInfo command)
  {
    ExecuteToggle("控制台");
  }

  // 統一的切換執行邏輯
  private void ExecuteToggle(string setterName)
  {
    Config.Enabled = !Config.Enabled;
    string status = Config.Enabled ? "\x06開啟" : "\x02關閉";
    string consoleStatus = Config.Enabled ? "開啟" : "關閉";
    
    // 遊戲內通知
    Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{setterName}\x01 將白名單設定：{status}");
    
    // 控制台通知
    Logger.LogInformation($"[WhiteList] {setterName} 已將白名單設定為: {consoleStatus}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
