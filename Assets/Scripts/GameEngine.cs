using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using UnityEngine.UI;

/**
 * FourDLevel contains information about one level.
 * It is possible to load and save it directly from a XML file.
 * */
[XmlRoot("FourDLevel")]
public class FourDLevel
{	
	// At which coordinates the player starts the level
	[XmlElement("startPlayerPosition")]
	public Vector4 startPlayerPosition;

	// At which coordinates the player go to next level
	// deprecated
	[XmlElement("endLevelPosition")]
	public Vector4 endLevelPosition;
	
	[XmlElement("collectibleCountToNextLevel")]
	public int collectibleCountToNextLevel;

	// The XML to load for the next level
	[XmlElement("nextLevelFileName")]
	public string nextLevelFileName;

	// A list of 3D levels (each one should have a different W coordinate in 4D)
	[XmlArrayItem]
	public List<ThreeDLevel> levels;

	/**
	 * Save the 4D Level content in a XML file)
	 * */
	public void Save(string path)
	{
		var serializer = new XmlSerializer(typeof(FourDLevel));
		var encoding = Encoding.GetEncoding("UTF-8");
		
		using(StreamWriter stream = new StreamWriter( path, false, encoding))
		{
			serializer.Serialize(stream, this);
		}
	}
	
	/**
	 * Load the 4D Level content from a XML file)
	 * */
	public static FourDLevel Load(string path)
	{
		var serializer = new XmlSerializer(typeof(FourDLevel));
		using(var stream = new FileStream(path, FileMode.Open))
		{
			return serializer.Deserialize(stream) as FourDLevel;
		}
	}

	/**
	 * Compute the size of the level.
	 * Given the min and max value for each coordinates, output is the minimum size of the array to contain all cells
	 * */
	public void GetSizeOfLevel(out int xSize, out int ySize, out int zSize, out int wSize)
	{
		int xMin = int.MaxValue, xMax = int.MinValue;
		int yMin = int.MaxValue, yMax = int.MinValue;
		int zMin = int.MaxValue, zMax = int.MinValue;
		int wMin = int.MaxValue, wMax = int.MinValue;
		foreach (ThreeDLevel level in levels)
		{
			if (wMin > level.wCoordinate) { wMin = level.wCoordinate; }
			if (wMax < level.wCoordinate) { wMax = level.wCoordinate; }
			foreach (ThreeDCell cell in level.cells)
			{
				if (xMin > cell.xCoordinate) { xMin = cell.xCoordinate; }
				if (xMax < cell.xCoordinate) { xMax = cell.xCoordinate; }
				if (yMin > cell.yCoordinate) { yMin = cell.yCoordinate; }
				if (yMax < cell.yCoordinate) { yMax = cell.yCoordinate; }
				if (zMin > cell.zCoordinate) { zMin = cell.zCoordinate; }
				if (zMax < cell.zCoordinate) { zMax = cell.zCoordinate; }
			}
		}
		xSize = (xMax - xMin) + 1;
		ySize = (yMax - yMin) + 1;
		zSize = (zMax - zMin) + 1;
		wSize = (wMax - wMin) + 1;
	}
}

/**
 * ThreeDLevel contains information about a part of the level.
 * Each level is composed of several 3D layers, all parallel to each others.
 * */
public class ThreeDLevel
{
	[XmlAttribute("w")]
	public int wCoordinate;

	[XmlArrayItem]
	public List<ThreeDCell> cells;	
}

/**
 * A ThreeDCell has 3 coordinates X,Y,Z (W is relative to the whole 3D level).
 * Content is the prefab name at these coordinates
 * infoText is specific to question marks, it gives information to the player through UI.
 * */
public class ThreeDCell
{
	[XmlAttribute("x")]
	public int xCoordinate;
	[XmlAttribute("y")]
	public int yCoordinate;
	[XmlAttribute("z")]
	public int zCoordinate;
	[XmlAttribute("content")]
	public string content;
	[XmlAttribute("infoText")]
	public string infoText;
}

/**
 * Contains all the logic of the game. It's a big class.
 * */
public class GameEngine : MonoBehaviour 
{
	private int worldSize; // I should get rid of this
	private ThreeDCell[,,,] world; // 4D array containing all information about the current level

	public List<GameObject> availableCellPrefabs; // All available prefabs for cells
	private Dictionary<string, GameObject> availableCellPrefabsDico; // Easily accessible through a dictionnary (filled at runtime)

	// We use two parent levels, we want a smooth transition between them.
	public GameObject currentLevel;
	public GameObject nextLevel;

	// We need to keep a 4D position for the player
	public Vector4 fourDPlayerPosition;
	// As well as the axes of the current level.
	// Unity render things in 3D, so these vectors indicate which 3D space to render in our 4D world.
	public Vector4 fourDLevelForward;
	public Vector4 fourDLevelUp;
	public Vector4 fourDLevelRight;
	public Vector4 fourDLevelFixed; // fixed means we don't see this axis. By default, it refers to W axis

	// Next level data
	private Vector4 endLevelPosition;
	private string nextLevelFileName;

	// Actually equal to 3
	public int cellSize;

	public MyFirstPersonController player;

	// Gizmo used to display information about the space we're currently in
	public FourDCompassBehaviour fourDCompass;

	// first level to load !
	public string xmlFileName;

	// UI
	public GameObject canvasInfoPanel;
	public Text canvasInfoText;	
	private Coroutine hidePanelCoroutine;

	// Collectibles
	private int currentCollectibleCount;
	private int endLevelCollectibleCount;

	/**
	 * Hide UI Panel
	 * */
	public void HidePanel()
	{
		canvasInfoPanel.SetActive (false);
	}

	/** 
	 * Show UI Panel for 2 seconds and display text.
	 * */
	public void ShowPanel(string text)
	{
		canvasInfoText.text = text;
		canvasInfoPanel.SetActive (true);
		if (hidePanelCoroutine != null)
		{
			StopCoroutine(hidePanelCoroutine);
		}
		hidePanelCoroutine = StartCoroutine (WaitAndHidePanel (2));
	}

	/**
	 * Coroutine method to hide panel after a given time.
	 * */
	IEnumerator WaitAndHidePanel(float timer)
	{
		yield return new WaitForSeconds (timer);
		HidePanel ();
	}

	/**
	 * The FirstPersonController knows in which direction the player goes in 3D.
	 * The GameEngine knows which 3D space is actually displayed.
	 * 
	 * This method updates the 4D position of the player, depending on which 3D space he's in.
	 * */
	public void UpdatePlayerFourDPosition (float right, float up, float forward)
	{
		Vector4 deltaCoordinates = new Vector4 (right, up, forward, 0);
		Matrix4x4 transformMatrix = new Matrix4x4 ();
		transformMatrix.SetColumn (0, fourDLevelRight);
		transformMatrix.SetColumn (1, fourDLevelUp);
		transformMatrix.SetColumn (2, fourDLevelForward);
		transformMatrix.SetColumn (3, Vector4.zero);
		fourDPlayerPosition += transformMatrix * deltaCoordinates;
		Debug.Log ("New 4D Coordinates = " + fourDPlayerPosition.ToString ());
	}

	public void MovePlayerVertically(int deltaY)
	{
		fourDPlayerPosition.y += deltaY;
	}

	public void AddHalfCubeToCurrentPosition(string color)
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = "half"+color+"Cube";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}
		
	public void AddCubeToCurrentPosition(string color)
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = color+"Cube";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}
	
	public void AddLightToCurrentPosition()
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = "pointLight";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}
	
	public void AddPalmTreeToCurrentPosition()
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = "ground_palmtree";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}
	
	public void AddConiferToCurrentPosition()
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = "ground_conifer";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}
	
	public void AddBroadleafToCurrentPosition()
	{
		ThreeDCell cell = new ThreeDCell ();
		cell.xCoordinate = Mathf.RoundToInt( fourDPlayerPosition.x );
		cell.yCoordinate = Mathf.RoundToInt(fourDPlayerPosition.y);
		cell.zCoordinate = Mathf.RoundToInt( fourDPlayerPosition.z );
		cell.content = "ground_broadleaf";
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = cell;
	}


	public void RemoveCellFromCurrentPosition()
	{
		world [Mathf.RoundToInt(fourDPlayerPosition.x), Mathf.RoundToInt(fourDPlayerPosition.y), Mathf.RoundToInt(fourDPlayerPosition.z), Mathf.RoundToInt(fourDPlayerPosition.w)] = null;
	}

	/**
	 * Compute a Four D Rotation.
	 * A Four D Rotation is always a 90° rotation around a plane.
	 * The plane is always (up, forward) relatively to the player. This means everything in front of the player remains unchanged.
	 * */
	public void UpdateLevelFourDOrientation (Vector3 playerForwardDirection, int fourDRotation)
	{
		// fourDRotation is either +1 or -1
		if (fourDRotation != 1 && fourDRotation != -1)
		{
			return; // no rotation at all
		}
		if (fourDRotation == 1)
		{
			// rotation +90°
			if (Mathf.RoundToInt(playerForwardDirection.x).Equals(-1) || Mathf.RoundToInt(playerForwardDirection.x).Equals(1))
			{
				// up doesn't change
				// right doesn't change
				Vector4 tmp = fourDLevelForward;
				fourDLevelForward = -fourDLevelFixed;
				fourDLevelFixed = tmp;
			}
			else if (Mathf.RoundToInt(playerForwardDirection.z).Equals(-1) || Mathf.RoundToInt(playerForwardDirection.z).Equals(1))
			{
				// up doesn't change
				// forward doesn't change
				Vector4 tmp = fourDLevelRight;
				fourDLevelRight = -fourDLevelFixed;
				fourDLevelFixed = tmp;
			}
			else 
			{
				Debug.LogError("playerForwardDirection is a bad vector : " + playerForwardDirection.ToString());
			}
		}
		else if (fourDRotation == -1)
		{
			// rotation -90°
			if (Mathf.RoundToInt(playerForwardDirection.x).Equals(-1) || Mathf.RoundToInt(playerForwardDirection.x).Equals(1))
			{
				// up doesn't change
				// right doesn't change
				Vector4 tmp = fourDLevelFixed;
				fourDLevelFixed = -fourDLevelForward;
				fourDLevelForward = tmp;
			}
			else if (Mathf.RoundToInt(playerForwardDirection.z).Equals(-1) || Mathf.RoundToInt(playerForwardDirection.z).Equals(1))
			{
				// up doesn't change
				// forward doesn't change
				Vector4 tmp = fourDLevelFixed;
				fourDLevelFixed = -fourDLevelRight;
				fourDLevelRight = tmp;
			} 
			else 
			{
				Debug.LogError("playerForwardDirection is a bad vector : " + playerForwardDirection.ToString());
			}
		}
		// update compass
		fourDCompass.SetAxes (fourDLevelRight, fourDLevelUp, fourDLevelForward, fourDLevelFixed);
		// switch 3D spaces
		BuildNextLevel ();
		SwitchLevels ();
	}

	/**
	 * Fill the 4D array "world" with all data from the XML file.
	 * */
	public void LoadLevelFromXML(string fileName)
	{
		int cellsCount = 0;
		try
		{
			string pathToXMLFile = Path.Combine (Application.streamingAssetsPath, fileName);
			FourDLevel fourDLevel = FourDLevel.Load (pathToXMLFile);
			
			int xSize, ySize, zSize, wSize;
			fourDLevel.GetSizeOfLevel (out xSize, out ySize, out zSize, out wSize);
			worldSize = Mathf.Max (Mathf.Max (xSize, ySize), Mathf.Max(zSize, wSize));
			world = new ThreeDCell[xSize,ySize,zSize,wSize];
			foreach (ThreeDLevel level in fourDLevel.levels)
			{
				int w = level.wCoordinate;
				foreach (ThreeDCell cell in level.cells)
				{
					bool screenshot = false; // used to render a section of the level, to make a beautiful screenshot :-)
					bool passThisCell = false;
					if (screenshot && cell.xCoordinate > 0 && cell.yCoordinate > 0 && cell.zCoordinate > 0 && w > 0)
					{
						if (cell.xCoordinate == xSize-1)
						{
							//passThisCell = true;
						}
						if (cell.yCoordinate == ySize-1)
						{
							//passThisCell = true;
						}
						if (cell.zCoordinate == zSize-1)
						{
							//passThisCell = true;
						}
						if (w == wSize-1)
						{
							//passThisCell = true;
						}
						if (cell.content.Equals("pointLight"))
						{
							passThisCell = true;
						}
						if (cell.content.Equals("infoPoint"))
						{
							passThisCell = true;
						}
					}

					if (!passThisCell)
					{
						world[cell.xCoordinate, cell.yCoordinate, cell.zCoordinate, w] = cell;
					}
					cellsCount++;
				}
			}
			fourDPlayerPosition = fourDLevel.startPlayerPosition;
			//endLevelPosition = fourDLevel.endLevelPosition;
			endLevelCollectibleCount = fourDLevel.collectibleCountToNextLevel;
			nextLevelFileName = fourDLevel.nextLevelFileName;
			// default axes
			fourDLevelRight = new Vector4(1,0,0,0);
			fourDLevelUp = new Vector4(0,1,0,0);
			fourDLevelForward = new Vector4(0,0,1,0);
			fourDLevelFixed = new Vector4(0,0,0,1);
			// update compass
			fourDCompass.SetAxes(fourDLevelRight, fourDLevelUp, fourDLevelForward, fourDLevelFixed);
		}
		catch (Exception e)
		{
			Debug.LogError("Got Exception (e: "+e.ToString()+") while loading XML File");
			cellsCount = 0;
		}
		if (cellsCount == 0)
		{
			Debug.Log("Error while reading XML File. Level is empty");
			Application.Quit();
		}
	}

	public void AddCollectible()
	{
		currentCollectibleCount++;
		world [Mathf.RoundToInt (fourDPlayerPosition.x), Mathf.RoundToInt (fourDPlayerPosition.y), Mathf.RoundToInt (fourDPlayerPosition.z), Mathf.RoundToInt (fourDPlayerPosition.w)] = null;
		if (currentCollectibleCount < endLevelCollectibleCount)
		{
			ShowPanel( (endLevelCollectibleCount-currentCollectibleCount) + " collectible(s) restant(s) !");
		}
		CheckEndLevel ();
	}

	/**
	 * [Debug method]
	 * Save current level in a XML file.
	 * Will be used by the Level Editor.
	 * */
	public void SaveLevelToXML(string fileName)
	{
		FourDLevel fourDLevel = new FourDLevel ();
		fourDLevel.startPlayerPosition = this.fourDPlayerPosition;
		fourDLevel.endLevelPosition = this.endLevelPosition;
		fourDLevel.levels = new List<ThreeDLevel> ();
		fourDLevel.nextLevelFileName = "test.xml";

		bool saveCubesAtTheEnd = false;

		for (int w = 0 ; w < world.GetLength(3) ; w++)
		{
			ThreeDLevel threeDLevel = new ThreeDLevel ();
			threeDLevel.wCoordinate = w;
			threeDLevel.cells = new List<ThreeDCell> ();

			List<ThreeDCell> coloredCubes =  new List<ThreeDCell> ();

			for (int y = 0 ; y < world.GetLength(1) ; y++)
			{
				for (int x = 0 ; x < world.GetLength(0) ; x++)
				{
					for (int z = 0 ; z < world.GetLength(2) ; z++)
					{
						if (world[x,y,z,w] != null)
						{
							if (saveCubesAtTheEnd &&
							    (world[x,y,z,w].content.Equals("greenCube") ||
							    world[x,y,z,w].content.Equals("orangeCube") ||
							    world[x,y,z,w].content.Equals("redCube") ) )
							{
								coloredCubes.Add(world[x,y,z,w]);
							}
							else
							{
								threeDLevel.cells.Add(world[x,y,z,w]);
							}
						}
					}					
				}				
			}
			if (saveCubesAtTheEnd)
			{
				foreach(ThreeDCell cell in coloredCubes)
				{
					threeDLevel.cells.Add(cell);
				}
			}

			fourDLevel.levels.Add (threeDLevel);
		}

		string pathToXMLFile = Path.Combine (Application.streamingAssetsPath, fileName);
		Debug.Log (pathToXMLFile);
		fourDLevel.Save (pathToXMLFile);
	}

	private Coroutine ShowIntroMenuCoroutine;

	/**
	 * Return true if the player is at the end of the level.
	 * */
	private bool CheckEndLevel()
	{
		if (currentCollectibleCount >= endLevelCollectibleCount)
		{
			ShowPanel ("Fin du niveau ! Félicitations !");
			ShowIntroMenuCoroutine = StartCoroutine(WaitAndShowIntroMenu(2.0f));
			return true;
		}
		return false;
		/*
		Vector4 distanceToEnd = (fourDPlayerPosition - endLevelPosition);
		if (distanceToEnd.magnitude < 0.5f)
		{
			ShowPanel ("Fin du niveau ! Félicitations !");
			StartCoroutine(WaitAndShowIntroMenu(2.0f));
			return true;
		}
		return false;
		*/
	}

	IEnumerator WaitAndShowIntroMenu(float timer)
	{
		yield return new WaitForSeconds (timer);		
		ShowIntroMenu();
	}

	// Use this for initialization
	void Start () 
	{
		// hide window
		this.HidePanel ();

		// init prefabs lib
		availableCellPrefabsDico = new Dictionary<string, GameObject>();
		foreach (GameObject prefab in availableCellPrefabs)
		{
			availableCellPrefabsDico.Add(prefab.name, prefab);
		}

		ShowIntroMenu();

		holdAllInputs = true;
	}

	public GameObject IntroMenu;

	public void ShowIntroMenu()
	{
		currentCollectibleCount = 0;
		IntroMenu.SetActive (true);
	}

	public void StartPuzzle(string puzzleFileName)
	{
		if (ShowIntroMenuCoroutine != null)
		{
			StopCoroutine(ShowIntroMenuCoroutine);
		}

		// init
		currentCollectibleCount = 0;
		LoadLevelFromXML (puzzleFileName);
		
		// Build level
		BuildNextLevel ();
		SwitchLevels ();

		// Hide Intro Menu
		IntroMenu.SetActive (false);
		holdAllInputs = true;

		player.ResetPlayer ();
	}
	
	// Update is called once per frame
	void Update () 
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			ShowIntroMenu();			
			holdAllInputs = true;
		}

		//CheckEndLevel ();
	}

	public void ExitApplication()
	{
		Application.Quit ();
	}

	/**
	 * Instantiate all GameObject for the next 3D level to render and store them as children of "nextLevel".
	 * 
	 * The 3D level is a section of the 4D world.
	 * */
	private void BuildNextLevel()
	{
		// Build Level
		// fourDCoordinates represents the coordinates of the current cell to render.
		// Depending on the current section, it can go from smallest coordinates to highest or the opposite.
		Vector4 fourDcoordinates = Vector4.zero;
		fourDcoordinates = new Vector4(fourDLevelFixed.x==0 ? 0 : fourDPlayerPosition.x , fourDLevelFixed.y==0 ? 0 : fourDPlayerPosition.y , fourDLevelFixed.z==0 ? 0 : fourDPlayerPosition.z , fourDLevelFixed.w==0 ? 0 : fourDPlayerPosition.w );
		bool rightSymetry = false;
		bool upSymetry = false;
		bool forwardSymetry = false;
		if (fourDLevelRight.x < 0 || fourDLevelRight.y < 0 || fourDLevelRight.z < 0 || fourDLevelRight.w < 0)
		{
			fourDcoordinates -= fourDLevelRight*(worldSize-1);
			rightSymetry = true;
		}
		if (fourDLevelForward.x < 0 || fourDLevelForward.y < 0 || fourDLevelForward.z < 0 || fourDLevelForward.w < 0)
		{
			fourDcoordinates -= fourDLevelForward*(worldSize-1);
			forwardSymetry = true;
		}
		if (fourDLevelUp.x < 0 || fourDLevelUp.y < 0 || fourDLevelUp.z < 0 || fourDLevelUp.w < 0)
		{
			fourDcoordinates -= fourDLevelUp*(worldSize-1);
			upSymetry = true;
		}
		// right, forward and up refers to the current Unity 3D Scene
		for (int right = 0 ; right < worldSize ; right++)
		{
			for (int forward = 0 ; forward < worldSize ; forward++)
			{
				for (int up = 0 ; up < worldSize ; up++)
				{
					int x = Mathf.RoundToInt(fourDcoordinates.x);
					int y = Mathf.RoundToInt(fourDcoordinates.y);
					int z = Mathf.RoundToInt(fourDcoordinates.z);
					int w = Mathf.RoundToInt(fourDcoordinates.w);
					if (!(x < 0 || x >= world.GetLength(0) || y < 0 || y >= world.GetLength(1) || z < 0 || z >= world.GetLength(2) || w < 0 || w >= world.GetLength(3)))
					{
						// Get the current cell to render
						ThreeDCell cellContent = world[x, y, z, w];
						if (cellContent != null)
						{
							// Instantiate a gameObject from the prefab and put it at its 3D coordinates.
							GameObject gameObject = availableCellPrefabsDico[cellContent.content];
							GameObject newContent = (GameObject) Instantiate(gameObject, new Vector3(right*cellSize, up*cellSize, forward*cellSize), Quaternion.identity);

							// Plein de cas particuliers pour les symétries des objets
							// Meme avec tout ça, il reste des cas particuliers foireux
							// Il y a forcément une formule simple (à moins que j'ai chié mon moteur)
							// A voir plus tard...
							if (!newContent.tag.Equals("Symetrical"))
							{
								if ( (fourDLevelRight.z < 0 && fourDLevelForward.x > 0 && fourDLevelFixed.w > 0) ||
								    (fourDLevelRight.z < 0 && fourDLevelForward.w > 0 && fourDLevelFixed.x < 0) ||
								    (fourDLevelRight.w > 0 && fourDLevelForward.x > 0 && fourDLevelFixed.z > 0) )
								{
									newContent.transform.localEulerAngles = new Vector3(0,90,0);
									newContent.transform.localScale = new Vector3(-1 , 1 , -1);
								}
								else if ( (fourDLevelRight.z < 0 && fourDLevelForward.x < 0 && fourDLevelFixed.w < 0) ||
								         (fourDLevelRight.z < 0 && fourDLevelForward.w < 0 && fourDLevelFixed.x > 0) ||
								         (fourDLevelRight.w < 0 && fourDLevelForward.x < 0 && fourDLevelFixed.z > 0) )
								{
									newContent.transform.localEulerAngles = new Vector3(0,90,0);
									newContent.transform.localScale = new Vector3(1 , 1 , -1);
								}
								else if ( (fourDLevelRight.z > 0 && fourDLevelForward.x > 0 && fourDLevelFixed.w < 0) || 
								         (fourDLevelRight.w < 0 && fourDLevelForward.x > 0 && fourDLevelFixed.z < 0) || 
								         (fourDLevelRight.z > 0 && fourDLevelForward.w > 0 && fourDLevelFixed.x > 0) )
								{
									newContent.transform.localEulerAngles = new Vector3(0,90,0);
									newContent.transform.localScale = new Vector3(-1 , 1 , 1);
								}
								else if ( (fourDLevelRight.z > 0 && fourDLevelForward.x < 0 && fourDLevelFixed.w < 0) ||
								         (fourDLevelRight.z > 0 && fourDLevelForward.x < 0 && fourDLevelFixed.w > 0) ||
								         (fourDLevelRight.z > 0 && fourDLevelForward.w < 0 && fourDLevelFixed.x < 0) ||
								         (fourDLevelRight.w > 0 && fourDLevelForward.x < 0 && fourDLevelFixed.z < 0) )
								{
									newContent.transform.localEulerAngles = new Vector3(0,90,0);
									newContent.transform.localScale = new Vector3(1 , 1 , 1);
								}
								else
								{
									// Cas "général"
									// Je pensais qu'il gèrerait toutes les possibilités
									newContent.transform.localScale = new Vector3(rightSymetry ? -1:1 , upSymetry ? -1:1 , forwardSymetry ? -1:1);
								}
							}

							newContent.transform.parent = nextLevel.transform;
							// Particular case of the question marks
							if (newContent.GetComponent<QuestionMarkBehaviour>() != null && cellContent.infoText != null)
							{
								newContent.GetComponent<QuestionMarkBehaviour>().gameEngine = this;
								newContent.GetComponent<QuestionMarkBehaviour>().infoText = cellContent.infoText;
							}
						}
					}
					if (Mathf.RoundToInt(fourDPlayerPosition.x) == Mathf.RoundToInt(fourDcoordinates.x) &&
					    Mathf.RoundToInt(fourDPlayerPosition.y) == Mathf.RoundToInt(fourDcoordinates.y) &&
					    Mathf.RoundToInt(fourDPlayerPosition.z) == Mathf.RoundToInt(fourDcoordinates.z) &&
					    Mathf.RoundToInt(fourDPlayerPosition.w) == Mathf.RoundToInt(fourDcoordinates.w) )
					{
						// Put player in its position (relatively to new level)
						float offsetY = fourDPlayerPosition.y - Mathf.RoundToInt(fourDPlayerPosition.y);
						player.SetPlayerPosition( new Vector3(right, up+offsetY, forward) * cellSize + Vector3.up , currentLevel );
					}
					fourDcoordinates += fourDLevelUp; // fourDLevelUp can be negative (not for now, but it could)
				}
				fourDcoordinates += fourDLevelForward; // fourDLevelForward can be negative
				fourDcoordinates -= fourDLevelUp * worldSize;
			}
			fourDcoordinates += fourDLevelRight; // fourDLevelRight can be negative
			fourDcoordinates -= fourDLevelForward * worldSize;
		}
		fourDcoordinates -= fourDLevelRight * worldSize;

		// Allow everything in the plane [forward, up] to appear instantly (we want the player to believe these objects didn't move)
		int sinPlayerRotationYAngle = Mathf.RoundToInt (Mathf.Sin ((player.transform.localRotation.eulerAngles.y  * Mathf.PI)/180));
		if ( sinPlayerRotationYAngle == 0 )
		{
			// player is facing forward vec (YZ plane remains fixed)
			// nextLevel will appear
			foreach (Transform child in nextLevel.transform)
			{
				if (Mathf.RoundToInt ( child.position.x - player.transform.position.x) == 0 )
				{
					child.GetComponent<Animator>().SetBool("Instant", true);
				}
				else
				{
					child.GetComponent<Animator>().SetBool("Instant", false);
				}
			}
			// currentLevel will disappear
			foreach (Transform child in currentLevel.transform)
			{
				if (Mathf.RoundToInt ( child.position.x - player.transform.position.x) == 0 )
				{
					child.GetComponent<Animator>().SetBool("Instant", true);
				}
				else
				{
					child.GetComponent<Animator>().SetBool("Instant", false);
				}
			}
		}
		else
		{
			// player is facing right vec (XY plane remains fixed)
			// nextLevel will appear
			foreach (Transform child in nextLevel.transform)
			{
				if (Mathf.RoundToInt ( child.position.z - player.transform.position.z) == 0 )
				{
					child.GetComponent<Animator>().SetBool("Instant", true);
				}
				else
				{
					child.GetComponent<Animator>().SetBool("Instant", false);
				}
			}
			// currentLevel will disappear
			foreach (Transform child in currentLevel.transform)
			{
				if (Mathf.RoundToInt ( child.position.z - player.transform.position.z) == 0 )
				{
					child.GetComponent<Animator>().SetBool("Instant", true);
				}
				else
				{
					child.GetComponent<Animator>().SetBool("Instant", false);
				}
			}
		}
	}

	/**
	 * Make currentLevel disappear and make nextLevel appear.
	 * 
	 * Destroy currentLevel after a time.
	 * */
	private void SwitchLevels()
	{
		// old level disappears
		foreach (Transform child in currentLevel.transform)
		{
			child.GetComponent<Animator>().SetTrigger("Disappears");
		}

		// new level appears
		foreach (Transform child in nextLevel.transform)
		{
			child.GetComponent<Animator>().SetTrigger("Appears");
		}

		// destroy old level
		StartCoroutine (WaitAndDestroyOldLevel (2));
		holdAllInputs = true;

		Debug.Log ("New 4D Coordinates after SwitchLevels = " + fourDPlayerPosition.ToString ());
	}

	public bool holdAllInputs;

	/**
	 * Wait for a time then destroy current level and replace it with next level.
	 * Create a new empty "nextLevel" to store the next one.
	 * */
	IEnumerator WaitAndDestroyOldLevel(float waitingTime)
	{
		yield return new WaitForSeconds(waitingTime);
		Destroy (currentLevel);
		currentLevel = nextLevel;
		currentLevel.name = "CurrentLevel";
		nextLevel = new GameObject ("NextLevel");
		holdAllInputs = false;
	}
}
