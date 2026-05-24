using UnityEngine;
using static RaceManager;

public class PauseMenu : MonoBehaviour
{
	public BotManager botManager;

	public void RestartRace()
	{
		Instance.RestartRace();
	}

	public void RestartTimer()
	{
		Instance.elapsedTime = 0f;
	}

	public void CompleteAllCheckpoints()
	{
		Instance.passedCheckpoints = Instance.totalCheckpoints;
	}

	public void AddBot()
	{
		botManager.HandleBots(BotOp.Add, 1);
	}

	public void AddAllBots()
	{
		botManager.HandleBots(BotOp.Add, botManager.MAX_BOTS);
	}

	public void RemoveBot()
	{
		botManager.HandleBots(BotOp.Remove, 1);
	}

	public void RemoveAllBots()
	{
		botManager.HandleBots(BotOp.Remove, botManager.MAX_BOTS);
	}

	public void SwitchViewToBot()
	{
		if (botManager != null)
			botManager.SwitchViewToBot();
	}

	public void ToggleBotAIGizmos()
	{
		if (botManager != null)
			botManager.ToggleBotAIGizmos();
	}
}
