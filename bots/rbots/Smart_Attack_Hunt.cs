using System;
using RBot;
using System.Linq;

public class SmartAttackHunt {
	
	/// This bot has two modes: 
	/// - If you target a monster in-game before activating the bot, it will hunt that monster all across the map (private room is advised).
	/// - If you dont target a monster in-game before hand, it will attack any monster on screen and will stay on said screen.
	/// Note that the bot will automatically turn in any quests that are completed while the bot is active.
	/// ConsiderBossHPTreshhold is the amount of HP an enemy needs to have in order to be considered a boss, this is used for hunting delay
	/// Its recommanded to use attack mode if you are farming a boss for a quest
	
	//-----------EDIT BELOW-------------//
	public int TurnInAttempts = 10;
	public int ConsiderBossHPTreshhold = 100000;
	public readonly int[] SkillOrder = { 2, 4, 3, 1 };

	public bool AutoQuestComplete = true;
	public bool DropGrabber = false;
	public string[] GrabTheseDrops = {
		"Item Name Here",
		"Item Name Here",
		"Item Name Here",
		"Item Name Here",
		"Item Name Here"
		//If you need more items than 5, just add more items to this list
	 };
	//-----------EDIT ABOVE-------------//

	public ScriptInterface bot => ScriptInterface.Instance;
	public string Target;
	public int MonsterMaxHP;
	public void ScriptMain(ScriptInterface bot){
		bot.Options.SafeTimings = true;
		bot.Options.RestPackets = true;
		bot.Options.InfiniteRange = true;
		bot.Options.ExitCombatBeforeQuest = true;
		

		if (DropGrabber)
			GetDropList(GrabTheseDrops);
		SkillList(SkillOrder);
		DeathHandler();
		
		FormatLog(Text: "Script Started", Title: true);
		if (bot.Player.HasTarget) {
			Target = bot.Player.Target.Name;
			MonsterMaxHP = bot.Player.Target.HP;
		}
		
	// Hunting
		if (Target != null) {
			FormatLog("Hunting", $"[{Target}]");
			while(!bot.ShouldExit()) {
				bot.Player.Hunt(Target);
			
			// Auto Quest Complete
				if (AutoQuestComplete) {
					if (bot.Quests.ActiveQuests.Count >= 1) {
					// Sleep 750 if the enemy is a boss
						if (BossCheck())
							bot.Sleep(750);
						foreach (var Quest in bot.Quests.ActiveQuests) {
							int QuestID = Quest.ID;
							if (bot.Quests.CanComplete(QuestID)) {
								ModSafeQuestComplete(QuestID);
							}
						}
					}
				}
			}
		}
		
	// Attacking
		else {
			FormatLog("Attacking", "[Everything]", Tabs: 1);
			while(!bot.ShouldExit()) {
				bot.Player.SetSpawnPoint();
				bot.Player.Attack("*");
			// Auto Quest Complete
				if (AutoQuestComplete) {
					if (bot.Quests.ActiveQuests.Count >= 1) {
						foreach (var Quest in bot.Quests.ActiveQuests) {
							int QuestID = Quest.ID;
							if (bot.Quests.CanComplete(QuestID)) {
								ModSafeQuestComplete(QuestID);
							}
						}
					}
				}
			}
		}	
	}
	
	/*------------------------------------------------------------------------------------------------------------
													 Required Functions
	------------------------------------------------------------------------------------------------------------*/
	//These functions are required for this bot to function.

	public bool BossCheck() {
		if (bot.Monsters.CurrentMonsters.Count() == 1)
			return true;
		if (MonsterMaxHP >= ConsiderBossHPTreshhold)
			return true;
		return false;
	}

	/// <summary>
	/// Spams Skills when in combat. You can get in combat by going to a cell with monsters in it with bot.Options.AggroMonsters enabled or using an attack command against one.
	/// </summary>
	public void SkillList(params int[] Skillset)
	{
		if(bot.Handlers.Any(h => h.Name == "Skill Handler"))
			bot.Handlers.RemoveAll(h => h.Name == "Skill Handler");
		bot.RegisterHandler(1, b => {
			if (bot.Player.InCombat)
			{
				foreach (var Skill in Skillset)
				{
					if (bot.Player.CanUseSkill(Skill))
						bot.Player.UseSkill(Skill);
				}
			}
		}, "Skill Handler");
	}
	
	/// <summary>
	/// Checks if items in an array have dropped every second and picks them up if so. GetDropList is recommended.
	/// </summary>
	public void GetDropList(params string[] GetDropList)
	{
		if(bot.Handlers.Any(h => h.Name == "Drop Handler"))
			bot.Handlers.RemoveAll(h => h.Name == "Drop Handler");
		bot.RegisterHandler(4, b => {
			foreach (string Item in GetDropList)
			{
				if (bot.Player.DropExists(Item)) bot.Player.Pickup(Item);
			}
			bot.Player.RejectExcept(GetDropList);
		}, "Drop Handler");
	}
	
	/// <summary>
	/// Attempts to complete the quest with the set amount of {TurnInAttempts}. If it fails to complete, logs out. If it successfully completes, re-accepts the quest and checks if it can be completed again.
	/// </summary>
	public void ModSafeQuestComplete(int QuestID)
	{
		//Must have the following functions in your script:
		//ExitCombat

		string Cell = bot.Player.Cell;
		string Pad = bot.Player.Pad;
		int i = 0;
		while (bot.Quests.CanComplete(QuestID)) {
			if (bot.Player.Cell != "Wait" || bot.Player.InCombat)
				ExitCombat();
			bot.Quests.EnsureComplete(QuestID);
			i++;
			if (i > TurnInAttempts) {
				FormatLog("Quest", $"Turning in Quest {QuestID} failed. Logging out");
				bot.Player.Logout();
			}
		}
		FormatLog("Quest", $"Turning in Quest {QuestID} successful.");
		while (!bot.Quests.IsInProgress(QuestID)) bot.Quests.EnsureAccept(QuestID);
		bot.Player.Jump(Cell, Pad);
	}

	/// <summary>
	/// Logs following a specific format. No more than 3 tabs allowed.
	/// </summary>
	public void FormatLog(string Topic = "FormatLog", string Text = "Missing Input", int Tabs = 2, bool Title = false, bool Followup = false)
	{
		if (Title)
			bot.Log($"[{DateTime.Now:HH:mm:ss}] -----{Text}-----");
		else 
		{
			Tabs = Tabs > 3 ? 3 : Tabs;
			string TabPlace = "";
			for (int i = 0; i < Tabs; i++) 
				TabPlace += "\t";
			if (Followup) 
				bot.Log($"[{DateTime.Now:HH:mm:ss}] ↑ {TabPlace}{Text}");
			else 
				bot.Log($"[{DateTime.Now:HH:mm:ss}] {Topic} {TabPlace}{Text}");
		}
	}
	
	/// <summary>
	/// Exits Combat by jumping cells.
	/// </summary>
	public void ExitCombat()
	{
		bot.Options.AggroMonsters = false;
		bot.Player.Jump("Wait", "Spawn");
		bot.Wait.ForCellChange("Wait");
		bot.Wait.ForCombatExit();
	}

	public void DeathHandler() {
      bot.RegisterHandler(2, b => {
         if (bot.Player.State==0) {
            bot.Player.SetSpawnPoint();
            ExitCombat();
            bot.Sleep(12000);
         }
      });
	}
}