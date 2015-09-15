using UnityEngine;
using System.Collections;

/**
 * Custom First Person Controller.
 * 
 * The Player can move on a grid of 3x3 squares and make 90° rotations.
 * He can look around him in a limited frame.
 * This controller also deal with gravity and walls (Physics are custom made using Raycasts)
 * */
public class MyFirstPersonController : MonoBehaviour 
{
	public GameEngine gameEngine;

	public Camera playerCamera;
	public float maxHorizontalAngle;
	public float maxVerticalAngle;
	public float cameraMoveSensitiveness;
	public float cameraComeBackSensitiveness;
	private float comeBackCameraStep;
	private Quaternion startCameraQuaternion;
	private Quaternion endCameraQuaternion;
	private bool cameraMoves;
	
	public float playerRotateSensitiveness;
	private float playerRotateStep;
	private Quaternion startPlayerQuaternion;
	private Quaternion endPlayerQuaternion;
	private bool playerRotates;

	public float playerMoveSensitiveness;
	private float playerMoveStep;
	private Vector3 startPlayerPosition;
	private Vector3 endPlayerPosition;
	private bool playerMoves;
	private bool playerFalls;

	public float stepWalk;
	public float stepRotate;

	public bool ignoreFalls;
	public bool ignoreCollisions;
	public bool editMode;

	/**
	 * Set the player position and ensure that both previous level and current level are consistent with it.
	 * 
	 * This method is called when drawing the new level after a 4D rotation.
	 * It means the Player didn't actually move in 4D, only its position in the Unity scene changed.
	 * */
	public void SetPlayerPosition(Vector3 newPosition, GameObject previousLevel)
	{
		Debug.Log ("endPlayerPosition = " + endPlayerPosition.ToString ());
		Debug.Log ("newPosition = " + newPosition.ToString ());

		// If the player moved in the scene, we need to be sure the level in which he was moves with him.
		previousLevel.transform.Translate (newPosition - endPlayerPosition);

		// I struggle with the Y axis...
		startPlayerPosition = newPosition;
		endPlayerPosition = newPosition;
		this.transform.localPosition = newPosition;
	}

	// Use this for initialization
	void Start () 
	{
		ResetPlayer ();
	}

	private Coroutine CheckPlayerFallsCoroutine;
	private Coroutine CheckPlayerQuestionMarkCoroutine;
	private Coroutine CheckPlayerCollectibleCoroutine;

	public void ResetPlayer()
	{
		startCameraQuaternion = Quaternion.identity;
		endCameraQuaternion = Quaternion.identity;
		cameraMoves = false;
		
		playerRotateStep = playerRotateSensitiveness/100;
		startPlayerQuaternion = Quaternion.identity;
		endPlayerQuaternion = this.transform.localRotation;
		playerRotates = false;
		
		playerMoveStep = playerMoveSensitiveness/100;
		startPlayerPosition = Vector3.zero;
		endPlayerPosition = this.transform.localPosition;
		playerMoves = false;
		playerFalls = false;

		if (CheckPlayerFallsCoroutine != null)
		{
			StopCoroutine(CheckPlayerFallsCoroutine);
		}
		if (CheckPlayerQuestionMarkCoroutine != null)
		{
			StopCoroutine(CheckPlayerQuestionMarkCoroutine);
		}
		if (CheckPlayerCollectibleCoroutine != null)
		{
			StopCoroutine(CheckPlayerCollectibleCoroutine);
		}

		// These coroutine will call themselves regularly
		CheckPlayerFallsCoroutine = StartCoroutine(WaitAndCheckIfPlayerFalls(0.1f)); // Every 0.1s, we check if the player should fall
		CheckPlayerQuestionMarkCoroutine = StartCoroutine(WaitAndCheckIfPlayerIsInteractingWithAQuestionMark(0.5f)); // Every 0.5s, we check if the player is in front of a question mark
		CheckPlayerCollectibleCoroutine = StartCoroutine(WaitAndCheckIfPlayerGetsACollectible(1/30.0f));
	}

	IEnumerator WaitAndCheckIfPlayerFalls(float timer)
	{
		yield return new WaitForSeconds (timer);
		// player falls
		if (endPlayerPosition.Equals(this.transform.position) && DistanceBetweenPlayerAndSolidGround() > 0)
		{
			startPlayerPosition = this.transform.localPosition;
			float height = DistanceBetweenPlayerAndSolidGround();
			endPlayerPosition = this.transform.position - this.transform.up * height;
			gameEngine.UpdatePlayerFourDPosition(0,-height/3.0f,0);
			playerMoveStep = 0;
			playerMoves = true;
			playerFalls = true;
		}
		CheckPlayerFallsCoroutine = StartCoroutine(WaitAndCheckIfPlayerFalls(timer));
	}

	IEnumerator WaitAndCheckIfPlayerIsInteractingWithAQuestionMark(float timer)
	{
		yield return new WaitForSeconds (timer);
		// player falls
		QuestionMarkBehaviour questionMarkScript;
		if (IsPlayerSeeingAQuestionMark(out questionMarkScript))
		{
			questionMarkScript.ShowInfoText();
		}
		if (IsPlayerOnAQuestionMark(out questionMarkScript))
		{
			questionMarkScript.ShowInfoText();
			questionMarkScript.HideQuestionMark();
		}
		CheckPlayerQuestionMarkCoroutine = StartCoroutine(WaitAndCheckIfPlayerIsInteractingWithAQuestionMark(timer));
	}

	IEnumerator WaitAndCheckIfPlayerGetsACollectible(float timer)
	{
		yield return new WaitForSeconds (timer);
		// player falls
		if (IsPlayerOnACollectible ())
		{
			gameEngine.AddCollectible();
		}
		CheckPlayerCollectibleCoroutine = StartCoroutine(WaitAndCheckIfPlayerGetsACollectible(timer));
	}
	
	// Update is called once per frame
	void Update () 
	{
		// Edit level 
		if (editMode)
		{
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position + this.transform.up * -3;
				endPlayerPosition = this.transform.localPosition;
				gameEngine.MovePlayerVertically(-1);
				//gameEngine.UpdatePlayerFourDPosition(this.transform.up.x,this.transform.up.y,this.transform.up.z);
				playerMoveStep = 0;
				playerMoves = true;
			} 
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position + this.transform.up * 3;
				endPlayerPosition = this.transform.localPosition;
				gameEngine.MovePlayerVertically(1);
				//gameEngine.UpdatePlayerFourDPosition(this.transform.up.x,this.transform.up.y,this.transform.up.z);
				playerMoveStep = 0;
				playerMoves = true;
			} 
			if (Input.GetKeyDown(KeyCode.Keypad1))
			{
				gameEngine.AddHalfCubeToCurrentPosition("Green");
			} 
			if (Input.GetKeyDown(KeyCode.Keypad2))
			{
				gameEngine.AddHalfCubeToCurrentPosition("Orange");
			} 
			if (Input.GetKeyDown(KeyCode.Keypad3))
			{
				gameEngine.AddHalfCubeToCurrentPosition("Red");
			}
			if (Input.GetKeyDown(KeyCode.Keypad4))
			{
				gameEngine.AddCubeToCurrentPosition("green");
			} 
			if (Input.GetKeyDown(KeyCode.Keypad5))
			{
				gameEngine.AddCubeToCurrentPosition("orange");
			} 
			if (Input.GetKeyDown(KeyCode.Keypad6))
			{
				gameEngine.AddCubeToCurrentPosition("red");
			}
			if (Input.GetKeyDown(KeyCode.Keypad7))
			{
				gameEngine.AddPalmTreeToCurrentPosition();
			}
			if (Input.GetKeyDown(KeyCode.Keypad8))
			{
				gameEngine.AddConiferToCurrentPosition();
			}
			if (Input.GetKeyDown(KeyCode.Keypad9))
			{
				gameEngine.AddBroadleafToCurrentPosition();
			}
			if (Input.GetKeyDown(KeyCode.L))
			{
				gameEngine.AddLightToCurrentPosition();
			}
			
			if (Input.GetKeyDown(KeyCode.R))
			{
				gameEngine.RemoveCellFromCurrentPosition();
			}
			if (Input.GetKeyDown(KeyCode.M))
			{
				gameEngine.SaveLevelToXML("test.xml");
			}
		}

		// During a 4D rotation, all the level is drawn again. 
		// During this time, we don't want the player to be able to move. This is why we use "holdAllInputs"
		if (!gameEngine.holdAllInputs)
		{
			// Move input
			bool isStairs = false;
			if (Input.GetKeyDown(KeyCode.Z) && (!IsObstacleInFrontOfPlayer(this.transform.forward, out isStairs) || isStairs))
			{
				// Move forward if there's no obstacle
				// Don't forget to go slightly upward if the obstacle we face are stairs.
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position + this.transform.forward * stepWalk + this.transform.up * (isStairs ? 1.5f : 0);
				endPlayerPosition = this.transform.localPosition;
				gameEngine.UpdatePlayerFourDPosition(this.transform.forward.x,this.transform.forward.y + (isStairs ? 0.5f : 0),this.transform.forward.z);
				playerMoveStep = 0;
				playerMoves = true;
			}
			if (Input.GetKeyDown(KeyCode.S) && !IsObstacleInFrontOfPlayer(-this.transform.forward, out isStairs))
			{
				// Move backward if there's no obstacle
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position - this.transform.forward * stepWalk;
				endPlayerPosition = this.transform.localPosition;
				gameEngine.UpdatePlayerFourDPosition(-this.transform.forward.x,-this.transform.forward.y,-this.transform.forward.z);
				playerMoveStep = 0;
				playerMoves = true;
			}
			if (Input.GetKeyDown(KeyCode.D) && !IsObstacleInFrontOfPlayer(this.transform.right, out isStairs))
			{
				// Move to the right if there's no obstacle
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position + this.transform.right * stepWalk;
				endPlayerPosition = this.transform.localPosition;
				gameEngine.UpdatePlayerFourDPosition(this.transform.right.x,this.transform.right.y,this.transform.right.z);
				playerMoveStep = 0;
				playerMoves = true;
			}
			if (Input.GetKeyDown(KeyCode.Q) && !IsObstacleInFrontOfPlayer(-this.transform.right, out isStairs))
			{
				// Move to the left if there's no obstacle
				startPlayerPosition = this.transform.localPosition;
				this.transform.localPosition = endPlayerPosition;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.position = this.transform.position - this.transform.right * stepWalk;
				endPlayerPosition = this.transform.localPosition;
				gameEngine.UpdatePlayerFourDPosition(-this.transform.right.x,-this.transform.right.y,-this.transform.right.z);
				playerMoveStep = 0;
				playerMoves = true;
			}
			
			if (Input.GetKeyDown(KeyCode.A))
			{
				// Rotate 90° to the left
				startPlayerQuaternion = this.transform.localRotation;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.RotateAround(this.transform.position, this.transform.up, -stepRotate);
				endPlayerQuaternion = this.transform.localRotation;
				playerRotateStep = 0;
				playerRotates = true;
			}
			if (Input.GetKeyDown(KeyCode.E))
			{
				// Rotate 90° to the right
				startPlayerQuaternion = this.transform.localRotation;
				this.transform.localRotation = endPlayerQuaternion;
				this.transform.RotateAround(this.transform.position, this.transform.up, stepRotate);
				endPlayerQuaternion = this.transform.localRotation;
				playerRotateStep = 0;
				playerRotates = true;
			}
			if (Input.GetKeyDown(KeyCode.W))
			{
				// 4D rotation 90° positive
				// The gameEngine will redraw all the level
				gameEngine.UpdateLevelFourDOrientation(this.transform.forward, 1);
			}
			if (Input.GetKeyDown(KeyCode.C))
			{
				// 4D rotation 90° negative
				// The gameEngine will redraw all the level
				gameEngine.UpdateLevelFourDOrientation(this.transform.forward, -1);
			}
			
			// If the player is rotating, we need to update its orientation.
			// We're using a Lerp to make a smooth rotation.
			if (playerRotates)
			{
				playerRotateStep += playerRotateSensitiveness/100;
				if (playerRotateStep >= 1)
				{
					playerRotateStep = 1;
				}
				Quaternion currentPlayerQuaternion = Quaternion.Lerp(startPlayerQuaternion, endPlayerQuaternion, playerRotateStep);
				this.transform.localRotation = currentPlayerQuaternion;
				if (playerRotateStep == 1)
				{
					playerRotates = false;
				}
			}
			// If the player is moving, we need to update its position.
			// We're using a Lerp to make a smooth translation.
			if (playerMoves)
			{
				playerMoveStep += playerFalls? playerMoveSensitiveness/500 : playerMoveSensitiveness/100;
				float realPlayerMoveStep = playerMoveStep;
				if (playerMoveStep >= 1)
				{
					playerMoveStep = 1;
				}
				Vector3 currentPlayerPosition = Vector3.Lerp(startPlayerPosition, endPlayerPosition, realPlayerMoveStep);
				this.transform.localPosition = currentPlayerPosition;

				// Particular case of falling
				if (playerFalls)
				{
					startPlayerPosition = currentPlayerPosition;
				}

				if (playerMoveStep == 1)
				{
					playerMoves = false;
					playerFalls = false;
				}
			}
		}

		// Camera input
		// Look around in a limited frame
		if (Input.GetMouseButton(0))
		{
			// Input.mousePosition  (x,y) coordinates in window
			float factor = cameraMoveSensitiveness/100;
			Vector3 worldPointCursorPosition = playerCamera.ScreenToWorldPoint(new Vector3(Screen.width/2 + (Input.mousePosition.x-Screen.width/2)*factor, Screen.height/2 + (Input.mousePosition.y-Screen.height/2)*factor, 1));
			Quaternion playerCameraOrientationBefore = playerCamera.transform.localRotation;
			playerCamera.transform.LookAt(worldPointCursorPosition);
			
			Quaternion newLocalRotation = playerCamera.transform.localRotation;
			if (playerCamera.transform.localRotation.eulerAngles.x < 360-maxHorizontalAngle && playerCamera.transform.localRotation.eulerAngles.x > maxVerticalAngle && 
			    playerCamera.transform.localRotation.eulerAngles.y < 360-maxVerticalAngle && playerCamera.transform.localRotation.eulerAngles.y > maxHorizontalAngle)
			{
				// both rotations blocked
				newLocalRotation = playerCameraOrientationBefore;
			}
			else if (playerCamera.transform.localRotation.eulerAngles.x < 360-maxHorizontalAngle && playerCamera.transform.localRotation.eulerAngles.x > maxVerticalAngle)
			{
				// x rotation blocked
				newLocalRotation = Quaternion.Euler(new Vector3(playerCameraOrientationBefore.eulerAngles.x, playerCamera.transform.localRotation.eulerAngles.y, playerCamera.transform.localRotation.eulerAngles.z));
			}
			else if (playerCamera.transform.localRotation.eulerAngles.y < 360-maxVerticalAngle && playerCamera.transform.localRotation.eulerAngles.y > maxHorizontalAngle)
			{
				// y rotation blocked
				newLocalRotation = Quaternion.Euler(new Vector3(playerCamera.transform.localRotation.eulerAngles.x, playerCameraOrientationBefore.eulerAngles.y, playerCamera.transform.localRotation.eulerAngles.z));
			}
			playerCamera.transform.localRotation = newLocalRotation;
		}
		// When we release the left button, the camera goes back to looking in front of us
		if (Input.GetMouseButtonUp(0))
		{
			// come back to base position
			startCameraQuaternion = Quaternion.Euler(playerCamera.transform.localRotation.eulerAngles);
			endCameraQuaternion = Quaternion.identity;
			cameraMoves = true;
			comeBackCameraStep = 0;
		}
		// We need a smooth transition
		if (cameraMoves)
		{
			comeBackCameraStep += cameraComeBackSensitiveness/100;
			if (comeBackCameraStep >= 1)
			{
				comeBackCameraStep = 1;
			}
			Quaternion currentCameraQuaternion = Quaternion.Lerp(startCameraQuaternion, endCameraQuaternion, comeBackCameraStep);
			playerCamera.transform.localRotation = currentCameraQuaternion;
			if (comeBackCameraStep == 1)
			{
				cameraMoves = false;
			}
		}
	}

	/**
	 * Specific test for seeing question marks. They display information on the UI.
	 * */
	private bool IsPlayerSeeingAQuestionMark(out QuestionMarkBehaviour questionMarkBehaviour)
	{
		questionMarkBehaviour = null;
		Ray obstacleRayInFrontOf = new Ray (this.transform.position, this.transform.forward);
		RaycastHit obstacleHit;
		if (Physics.Raycast (obstacleRayInFrontOf, out obstacleHit, 3)) 
		{
			if (obstacleHit.collider.gameObject.tag.Equals("questionMark"))
			{
				questionMarkBehaviour = obstacleHit.collider.GetComponentInParent<QuestionMarkBehaviour>();
				return true;
			}
		}
		return false;
	}
	/**
	 * Specific test for being on a question mark. They display information on the UI.
	 * If the player is on one, we want to make the question mark disappear.
	 * */
	private bool IsPlayerOnAQuestionMark(out QuestionMarkBehaviour questionMarkBehaviour)
	{
		questionMarkBehaviour = null;
		Ray obstacleRayBelow = new Ray (this.transform.position + this.transform.up*1.5f, -this.transform.up);
		RaycastHit obstacleHit;
		if (Physics.Raycast (obstacleRayBelow, out obstacleHit, 2)) 
		{
			if (obstacleHit.collider.gameObject.tag.Equals("questionMark"))
			{
				questionMarkBehaviour = obstacleHit.collider.GetComponentInParent<QuestionMarkBehaviour>();
				return true;
			}
		}
		return false;
	}
	
	/**
	 * Specific test for being on a collectible.
	 * */
	private bool IsPlayerOnACollectible()
	{
		Ray obstacleRayBelow = new Ray (this.transform.position + this.transform.up*1.5f, -this.transform.up);
		RaycastHit obstacleHit;
		if (Physics.Raycast (obstacleRayBelow, out obstacleHit, 2)) 
		{
			if (obstacleHit.collider.gameObject.tag.Equals("Collectible"))
			{
				Destroy (obstacleHit.collider.gameObject);
				return true;
			}
		}
		return false;
	}

	/**
	 * If the player is in front of an obstacle, we don't want him to move through.
	 * Except for question marks and stairs.
	 * */
	private bool IsObstacleInFrontOfPlayer(Vector3 direction, out bool isStairs)
	{
		isStairs = false;
		if (ignoreCollisions)
		{
			return false;
		}
		Ray obstacleRayBelowMiddle = new Ray (endPlayerPosition + this.transform.up * 0.45f, direction);
		Ray obstacleRayOverMiddle = new Ray (endPlayerPosition + this.transform.up * 0.55f, direction);
		RaycastHit obstacleHit;
		if (Physics.Raycast (obstacleRayOverMiddle, out obstacleHit, 3)) 
		{
			if (obstacleHit.collider.tag.Equals("questionMark"))
			{
				return false;
			}
			if (obstacleHit.collider.transform.parent.name.Contains("collectible"))
			{
				return false;
			}
			isStairs = false;
			return true;
		}
		if (Physics.Raycast (obstacleRayBelowMiddle, out obstacleHit, 3)) 
		{
			isStairs = true;
			return true;
		}
		return false;
	}

	/**
	 * This is a check for the player being in air.
	 * This method is called regularly and check for "solid ground" below the player (that is, everything except question marks)
	 * */
	private float DistanceBetweenPlayerAndSolidGround()
	{
		if (ignoreFalls)
		{
			return 0;
		}
		Ray obstacleRay = new Ray (endPlayerPosition - this.transform.up * 0.9f, -this.transform.up);
		RaycastHit obstacleHit;
		int layerMask = (1 << LayerMask.NameToLayer("questionMark")) | (1 << LayerMask.NameToLayer("collectible"));
		if (Physics.Raycast (obstacleRay, out obstacleHit, 100, ~layerMask )) 
		{
			if (obstacleHit.distance < 0.11f)
			{
				return 0;
			}
			return obstacleHit.distance-0.1f;
		}
		return 100;
	}
}
