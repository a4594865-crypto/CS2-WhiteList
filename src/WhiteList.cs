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

    // 註冊監聽器，實作位於 Events.cs
    RegisterListener<OnClientAuthorized>(OnClientAuthorized);

    // 註冊指令
    AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
    AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
    
    // 註冊切換開關指令
    AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist);

    CheckVersion();

    if (Config.UseDatabase)
    {
      BuildDatabaseConnectionString();
      CheckDatabaseTables();
    }
    else
    {
      // 呼叫位於 Utility.cs 中的 CheckFile 方法
      CheckFile();
    }
  }

  // 處理白名單開關切換的方法
  [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
  public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
  {
    if (player == null) return;

    // 檢查執行者是否有權限 (預設為 @css/root)
    if (!AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
      command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
      return;
    }

    // 執行切換
    Config.Enabled = !Config.Enabled;

    // 設定顯示顏色與文字
    string status = Config.Enabled ? "\x06開啟" : "\x02關閉";
    
    // 全服廣播通知
    Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{player.PlayerName}\x01 將白名單設定：{status}");
    Logger.LogInformation($"Admin {player.PlayerName} toggled Whitelist to: {Config.Enabled}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
