using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/**
 * Manage the 3D gizmo at the bottom of the screen.
 * 
 * Will show 3 arrows depending on which space we're moving in. X,Y,Z or X,Y,W or Z,Y,W (Y should always be the Up vector)
 * */
public class FourDCompassBehaviour : MonoBehaviour 
{
	public List<GameObject> arrows; // Must contain 4 arrows X,Y,Z,W (red, green, blue, and purple)
	
	public TextMesh redArrowText; // X
	public TextMesh greenArrowText; // Y
	public TextMesh blueArrowText; // Z
	public TextMesh purpleArrowText; // W

	public GameEngine gameEngine;

	// Store current values (in order to compare with new values)
	// These vectors always have 3 coordinates at a value of zero, and one at a value of +1 or -1.
	private Vector4 fourDRight; // by default: (1,0,0,0), X is the Right vector
	private Vector4 fourDUp; // by default: (0,1,0,0), Y is the Up vector
	private Vector4 fourDForward; // by default: (0,0,1,0), Z is the Forward vector
	private Vector4 fourDFixed; // by default: (0,0,0,1), W is fixed (it's the 4th dimension, we can't see it)

	// Use this for initialization
	void Start () 
	{
		// Initialize at current gameEngine status
		ResetAxes (gameEngine.fourDLevelRight, gameEngine.fourDLevelUp, gameEngine.fourDLevelForward, gameEngine.fourDLevelFixed);
	}
	
	// Update is called once per frame
	void Update () 
	{
		// Ensure the gizmo always has the same orientation relatively to global frame
		// even if it's a child of Player.
		this.transform.rotation = Quaternion.identity;

		// Texts always face the Player
		redArrowText.transform.rotation = Quaternion.LookRotation(redArrowText.transform.position - this.transform.parent.transform.position);
		greenArrowText.transform.rotation = Quaternion.LookRotation(greenArrowText.transform.position - this.transform.parent.transform.position);
		blueArrowText.transform.rotation = Quaternion.LookRotation(blueArrowText.transform.position - this.transform.parent.transform.position);
		purpleArrowText.transform.rotation = Quaternion.LookRotation(purpleArrowText.transform.position - this.transform.parent.transform.position);
	}
	
	public void ResetAxes(Vector4 right, Vector4 up, Vector4 forward, Vector4 fixedDimension)
	{
		// I had trouble with Animators status, this is why I made a Reset() method
		fourDRight = Vector4.zero;
		fourDUp = Vector4.zero;
		fourDForward = Vector4.zero;
		fourDFixed = fixedDimension;
		arrows[0].GetComponent<Animator> ().SetBool ("Visible", false);
		arrows[1].GetComponent<Animator> ().SetBool ("Visible", false);
		arrows[2].GetComponent<Animator> ().SetBool ("Visible", false);
		arrows[3].GetComponent<Animator> ().SetBool ("Visible", false);
		SetAxes (right, up, forward, fixedDimension);
	}

	/**
	 * From the old and new values of Vectors, compute one arrow new position.
	 * The arrow can stay the same, it can become visible or invisible, or it can just switch orientation (if it goes from +1 to -1 for instance)
	 * */
	private void ComputeFourDVectorChangeForArrow(Vector4 right, Vector4 up, Vector4 forward, Vector4 fixedDimension, int arrowIndex)
	{
		GameObject arrow = arrows[arrowIndex];

		float epsilon = 0.5f; // entre 0 et 1 non compris

		if ( Mathf.Abs(fourDRight[arrowIndex] - right[arrowIndex]) < epsilon && 
		    Mathf.Abs(fourDUp[arrowIndex] - up[arrowIndex]) < epsilon && 
		    Mathf.Abs(fourDForward[arrowIndex] - forward[arrowIndex]) < epsilon && 
		    Mathf.Abs(fourDFixed[arrowIndex] - fixedDimension[arrowIndex]) < epsilon)
		{
			// do nothing
			return;
		}
		if ( (Mathf.Abs(fourDRight[arrowIndex] - right[arrowIndex]) > 1-epsilon && Mathf.Abs(fourDRight[arrowIndex] - right[arrowIndex]) < 1+epsilon) ||
		    (Mathf.Abs(fourDUp[arrowIndex] - up[arrowIndex]) > 1-epsilon && Mathf.Abs(fourDUp[arrowIndex] - up[arrowIndex]) < 1+epsilon) ||
		    (Mathf.Abs(fourDForward[arrowIndex] - forward[arrowIndex]) > 1-epsilon && Mathf.Abs(fourDForward[arrowIndex] - forward[arrowIndex]) < 1+epsilon) ||
		    (Mathf.Abs(fourDFixed[arrowIndex] - fixedDimension[arrowIndex]) > 1-epsilon && Mathf.Abs(fourDFixed[arrowIndex] - fixedDimension[arrowIndex]) < 1+epsilon) )
		{
			// vector go from invisible to visible or vice versa
			if (fixedDimension[arrowIndex] < -epsilon || fixedDimension[arrowIndex] > epsilon)
			{
				if (arrow.GetComponent<Animator>().GetBool("Visible"))
				{
					arrow.GetComponent<Animator> ().SetBool ("Visible", false);
				}
			}
			else
			{
				if (!arrow.GetComponent<Animator>().GetBool("Visible"))
				{
					arrow.GetComponent<Animator> ().SetBool ("Visible", true);
				}
				Quaternion quat = Quaternion.identity;
				quat.SetLookRotation (right[arrowIndex] * gameEngine.transform.right + up[arrowIndex] * gameEngine.transform.up + forward[arrowIndex] * gameEngine.transform.forward);
				arrow.transform.localRotation = quat;
			}
			return;
		}
		if ( Mathf.Abs(fourDRight[arrowIndex] - right[arrowIndex]) > 2-epsilon ||
		    Mathf.Abs(fourDUp[arrowIndex] - up[arrowIndex]) > 2-epsilon ||
		    Mathf.Abs(fourDForward[arrowIndex] - forward[arrowIndex]) > 2-epsilon ||
		    Mathf.Abs(fourDFixed[arrowIndex] - fixedDimension[arrowIndex]) > 2-epsilon )
		{
			// vector switch orientation
			Quaternion quat = Quaternion.identity;
			quat.SetLookRotation (right[arrowIndex] * gameEngine.transform.right + up[arrowIndex] * gameEngine.transform.up + forward[arrowIndex] * gameEngine.transform.forward);
			arrow.transform.localRotation = quat;
			return;
		}
	}

	/**
	 * Update visible status and orientations of the 4 arrows, then update the current values of vectors.
	 * */
	public void SetAxes(Vector4 right, Vector4 up, Vector4 forward, Vector4 fixedDimension)
	{
		for (int arrowIndex = 0 ; arrowIndex < arrows.Count ; arrowIndex++)
		{
			ComputeFourDVectorChangeForArrow(right, up, forward, fixedDimension, arrowIndex);
		}

		fourDRight = right;
		fourDUp = up;
		fourDForward = forward;
		fourDFixed = fixedDimension;
	}
}
