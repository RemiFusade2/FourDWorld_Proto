using UnityEngine;
using System.Collections;

public class PlacardDoorBehaviour : MonoBehaviour {


	void OnMouseDown()
	{
		this.GetComponent<Animator> ().SetBool ("Open", !this.GetComponent<Animator> ().GetBool ("Open"));
	}
}
