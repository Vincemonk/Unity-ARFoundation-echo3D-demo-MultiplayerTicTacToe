/**************************************************************************
* Copyright (C) echoAR, Inc. 2018-2020.                                   *
* echoAR, Inc. proprietary and confidential.                              *
*                                                                         *
* Use subject to the terms of the Terms of Service available at           *
* https://www.echoar.xyz/terms, or another agreement                      *
* between echoAR, Inc. and you, your company or other organization.       *
***************************************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CustomBehaviour : MonoBehaviour
{
    [HideInInspector]
    public Entry entry;

    // Enter your echoAR API key
    public string APIKey = "<echoAR API Key>";
    // The piece you want to be (valid options are "o" or "x")
    string playerName = "o";


    string whoseTurn = "o";
    bool gameOver = false;
    List<string> placeholderNames = new List<string> {"tl", "tm", "tr", "ml", "mm", "mr", "bl", "bm", "br"};

    GameObject boardObject;
    GameObject xObject;
    GameObject oObject;

    GameObject statusText;
    TextMesh statusTextMesh;

    string boardSpot;
    string switchTurns;

    // Dictionary that maps gameobject to boardspot
    Dictionary<GameObject, string> boardDictionary = new Dictionary<GameObject, string>();

    // Use this for initialization
    void Start()
    {
        // Add RemoteTransformations script to object and set its entry
        this.gameObject.AddComponent<RemoteTransformations>().entry = entry;

        // Query additional data to get the name
        string value = "";
        if (entry.getAdditionalData() != null && entry.getAdditionalData().TryGetValue("name", out value)) {
            // Set name
            this.gameObject.name = value;
        }

        if (APIKey == "<echoAR API Key>") {
          Debug.Log("Enter a valid echoAR API Key in CustomBehavior.cs");
        }

        if (this.gameObject.name == "board") {
          // initialize board
          InitBoard();

          // create text to display turn and game status
          statusText = new GameObject();
          statusText.name = "status";
          statusText.transform.position = new Vector3(1, 3, 8.5f);
          statusTextMesh = statusText.AddComponent<TextMesh>();
          statusTextMesh.text = whoseTurn + "'s Turn";
          statusTextMesh.fontSize = 10;
          statusTextMesh.anchor = TextAnchor.MiddleCenter;
          statusTextMesh.alignment = TextAlignment.Center;
        }
    }

    // Update is called once per frame
    void Update()
    {
      if (this.gameObject.name != "board") return;

      // Wait for board to load in
      if (boardObject == null) {
        boardObject = GameObject.Find("board");
      } else {
        CheckGameOver();
        if (gameOver) {
          UpdateBoard();
          return;
        }
      }
      // Wait for x piece to load in
      if (xObject == null) {
        xObject = GameObject.Find("xPiece");
        xObject.SetActive(false);
      }
      // Wait for o piece to load in
      if (oObject == null) {
        oObject = GameObject.Find("oPiece");
        oObject.SetActive(false);
      }

      // Blocks if API key is not valid
      if (APIKey == "<echoAR API Key>") {
        return;
      }
      
      // Check metadata to detect for turn change and board updates
      if (entry.getAdditionalData() != null) {
        entry.getAdditionalData().TryGetValue("whoseTurn", out whoseTurn);
        statusTextMesh.text = whoseTurn + "'s Turn";
        // Update the board to reflect opponent's move
        UpdateBoard();
      }

      // Blocks current player if it is not their turn
      if (playerName != whoseTurn) {
        return;
      }

      // On click detection
      if (Input.GetMouseButtonDown(0)) {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit)) {
          if (hit.transform != null) {
            // If player clicked on an empty spot then place a piece there
            if (boardDictionary.ContainsKey(hit.transform.gameObject)) {
              // Place the corresponding o or x piece at the empty spot
              GameObject clone = (whoseTurn == "o") ? oObject : xObject;
              Vector3 pos = hit.transform.position;
              GameObject mark = Instantiate(clone, pos, Quaternion.identity);
              mark.SetActive(true);

              boardSpot = hit.transform.gameObject.name;
              switchTurns = (whoseTurn == "o") ? "x" : "o";
              boardDictionary.Remove(hit.transform.gameObject);
              Destroy(hit.transform.gameObject);

              // Update the metadata to reflect piece placement and turn change
              IEnumerator caroutine1 = UpdateMetadata();
              StartCoroutine(caroutine1);
            }
          }
        }
      }
    }

    // Checks metadata to see if there is a winner
    void CheckGameOver() {
      // Query metadata to get data for all the spots
      Dictionary<string, string> data = boardObject.GetComponent<RemoteTransformations>().entry.getAdditionalData();
      string tl, tm, tr, ml, mm, mr, bl, bm, br;
      data.TryGetValue("tl", out tl);
      data.TryGetValue("tm", out tm);
      data.TryGetValue("tr", out tr);
      data.TryGetValue("ml", out ml);
      data.TryGetValue("mm", out mm);
      data.TryGetValue("mr", out mr);
      data.TryGetValue("bl", out bl);
      data.TryGetValue("bm", out bm);
      data.TryGetValue("br", out br);

      string winner = null;
      // check horizontal
      if ((tl == "o" || tl == "x") && tl == tm && tm == tr) winner = tl;
      else if ((ml == "o" || ml == "x") && ml == mm && mm == mr) winner = ml;
      else if ((bl == "o" || bl == "x") && bl == bm && bm == br) winner = bl;
      // check vertical
      else if ((tl == "o" || tl == "x") && tl == ml && ml == bl) winner = tl;
      else if ((tm == "o" || tm == "x") && tm == mm && mm == bm) winner = tm;
      else if ((tr == "o" || tr == "x") && tr == mr && mr == br) winner = tr;
      // check diagonal
      else if ((tl == "o" || tl == "x") && tl == mm && mm == br) winner = tl;
      else if ((tr == "o" || tr == "x") && tr == mm && mm == bl) winner = tr;
      // check draw
      else if (boardDictionary.Count == 0 && winner == null) winner = "draw";

      if (winner != null) {
        if (winner == "x" || winner == "o") {
          statusTextMesh.text = winner + " wins";
          gameOver = true;
        } else if (winner == "draw") {
          statusTextMesh.text = "draw";
          gameOver = true;
        }
      }
    }

    // Generate 9 cube objects to be placeholders (represents the spots on the board)
    void InitBoard() {
      int count = 0;
      int x = -1;
      int y = 1;

      for(int i = 0; i < 3; i++) {
        for(int j = 0; j < 3; j++) {
          GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
          cube.AddComponent<MeshCollider>();
          cube.name = placeholderNames[count];
          cube.transform.position = new Vector3(x, y, 8.5f);
          boardDictionary.Add(cube, cube.name);
          x += 2;
          count++;
        }
        x = -1;
        y -= 2;
      }
    }

    // Update the board for the player after an opponent placed a piece
    void UpdateBoard() {
      for(int i = 0; i < placeholderNames.Count; i++) {
        string item;
        entry.getAdditionalData().TryGetValue(placeholderNames[i], out item);
        if (item != "x" && item != "o") {
          continue;
        } else {
          // has a piece, check if it has been rendered yet
          GameObject placeholder = GameObject.Find(placeholderNames[i]);
          if (placeholder != null) {
            // means it has not been rendered yet, so render it
            GameObject clone = (item == "o") ? oObject : xObject;
            Vector3 pos = placeholder.transform.position;
            GameObject mark = Instantiate(clone, pos, Quaternion.identity);

            boardDictionary.Remove(placeholder);
            Destroy(placeholder);
            mark.SetActive(true);

            // check for game over after updating the board
            CheckGameOver();
            break;
          }
        }
      }
    }

    // Make API calls to update the metadata with piece placement and turn change
    public IEnumerator UpdateMetadata() {
      yield return StartCoroutine(UpdateEntryData(entry.getId(), boardSpot, whoseTurn));
      yield return StartCoroutine(UpdateEntryData(entry.getId(), "whoseTurn", switchTurns));
    }

    public IEnumerator UpdateEntryData(string entryID, string dataKey, string dataValue) {
        // Create form
        WWWForm form = new WWWForm();
        // Set form data
        form.AddField("key", APIKey);        // API Key
        form.AddField("entry", entryID);     // Entry ID
        form.AddField("data", dataKey);      // Key
        form.AddField("value", dataValue);   // Value
        UnityWebRequest www = UnityWebRequest.Post("https://console.echoar.xyz/post", form);
        // Send request
        yield return www.SendWebRequest();
        // Check for errors
        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log("Data update complete!");
        }
    }
}