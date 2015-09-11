using UnityEngine;
using System.Collections;

public class QuestionMarkBehaviour : MonoBehaviour 
{
	public GameEngine gameEngine;

	public string infoText;

	void OnMouseDown()
	{
		ShowInfoText();
		HideQuestionMark();
	}

	public void HideQuestionMark()
	{
		this.gameObject.SetActive (false);
	}

	public void ShowInfoText()
	{
		gameEngine.ShowPanel (infoText);
	}
}
