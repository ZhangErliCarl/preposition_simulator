﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using StoryGenerator.HomeAnnotation;
using StoryGenerator.Recording;
using StoryGenerator.Rendering;
using StoryGenerator.RoomProperties;
using StoryGenerator.SceneState;
using StoryGenerator.Scripts;
using StoryGenerator.Utilities;
using StoryGenerator.Communication;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StoryGenerator.CharInteraction;
using Unity.Profiling;
using UnityEditor;

namespace StoryGenerator
{
    [RequireComponent(typeof(Recorder))]
    public class TestDriver : MonoBehaviour
    {

        static ProfilerMarker s_GetGraphMarker = new ProfilerMarker("MySystem.GetGraph");
        static ProfilerMarker s_GetMessageMarker = new ProfilerMarker("MySystem.GetMessage");
        static ProfilerMarker s_UpdateGraph = new ProfilerMarker("MySystem.UpdateGraph");
        static ProfilerMarker s_SimulatePerfMarker = new ProfilerMarker("MySystem.Simulate");
        string actionLogPrefix = "Logs\\action_log_";
        public string actionLog = "";
        string timestampPrefix;
        bool done = false;
        int index = 0;
        private const int DefaultPort = 8080;
        private const int DefaultTimeout = 500000;


        //static ProcessingController processingController;
        static HttpCommunicationServer commServer;
        static NetworkRequest networkRequest = null;

        public static DataProviders dataProviders;

        public List<State> CurrentStateList = new List<State>();
        public int num_renderings = 0;
        private int numSceneCameras = 0;
        private int numCharacters = 0;
        public int finishedChars = 0; // Used to count the number of characters finishing their actions.

        public GameObject cam1;
        public GameObject cam2;
        public GameObject cam3;
        public GameObject cam4;
        public GameObject babyCam;

        Recorder recorder;
        // TODO: should we delete this ^
        private List<Recorder> recorders = new List<Recorder>();
        List<Camera> sceneCameras;
        List<Camera> cameras;
        private List<CharacterControl> characters = new List<CharacterControl>();
        private List<ScriptExecutor> sExecutors = new List<ScriptExecutor>();
        private List<GameObject> rooms = new List<GameObject>();



        WaitForSeconds WAIT_AFTER_END_OF_SCENE = new WaitForSeconds(3.0f);

        [Serializable]
        class SceneData
        {
            //public string characterName;
            public string sceneName;
            public int frameRate;
        }

        void Start()
        {
            recorder = GetComponent<Recorder>();

            // Initialize data from files to static variable to speed-up the execution process
            if (dataProviders == null) {
                dataProviders = new DataProviders();
            }
            timestampPrefix = (DateTime.Now.ToString("MM_dd_yyyy_HHmmss"));
            actionLog = actionLogPrefix + timestampPrefix + ".txt";
            index = 0;
            Debug.Log(actionLog);
            CharacterControl.log = actionLog;
            if (!Directory.Exists("Logs/"))
            {
                Directory.CreateDirectory("Logs/");
            }
            var file = System.IO.File.Create(actionLog);
            file.Close();
            List<string> list_assets = dataProviders.AssetsProvider.GetAssetsPaths();



            ////Check all the assets exist
            //foreach (string asset_name in list_assets) {
            //    if (asset_name != null) {
            //        GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(asset_name)) as GameObject;
            //        if (loadedObj == null) {
            //            Debug.Log(asset_name);
            //            Debug.Log(loadedObj);
            //        }
            //    }
            //}

            if (commServer == null) {
                InitServer();
            }
            commServer.Driver = this;
            setRigidBody();
            setConvex();
            ProcessHome(false);
            DeleteChar();

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (networkRequest == null) {
                commServer.UnlockProcessing(); // Allow to proceed with requests
            }
            StartCoroutine(ProcessNetworkRequest());
        }
        
        private void InitServer()
        {
            string[] args = Environment.GetCommandLineArgs();
            var argDict = DriverUtils.GetParameterValues(args);
            string portString = null;
            int port = DefaultPort;

            if (!argDict.TryGetValue("http-port", out portString) || !Int32.TryParse(portString, out port))
                port = DefaultPort;

            commServer = new HttpCommunicationServer(port) { Timeout = DefaultTimeout };
        }

        private void OnApplicationQuit()
        {
            commServer?.Stop();
        }

        void DeleteChar()
        {

            foreach (Transform tf_child in transform)
            {
                foreach (Transform tf_obj in tf_child)
                {
                    if (tf_obj.gameObject.name.ToLower().Contains("male"))
                    {
                        Destroy(tf_obj.gameObject);
                    }
                }
            }


        }

        void ProcessHome(bool randomizeExecution)
        {
            UtilsAnnotator.ProcessHome(transform, randomizeExecution);

            ColorEncoding.EncodeCurrentScene(transform);
            // Disable must come after color encoding. Otherwise, GetComponent<Renderer> failes to get
            // Renderer for the disabled objects.
            UtilsAnnotator.PostColorEncoding_DisableGameObjects();
        }

        public void ProcessRequest(string request)
        {
            Debug.Log(string.Format("Processing request {0}", request));

            NetworkRequest newRequest = null;
            try {
                newRequest = JsonConvert.DeserializeObject<NetworkRequest>(request);
            } catch (JsonException) {
                return;
            }

            if (networkRequest != null)
                return;

            networkRequest = newRequest;
        }

        public void SetRequest(NetworkRequest request)
        {
            networkRequest = request;
        }

        IEnumerator TimeOutEnumerator(IEnumerator enumerator)
        {
            while (enumerator.MoveNext() && !recorder.BreakExecution())
                yield return enumerator.Current;
        }

        static public List<String> GetObjects() {
            // get root objects in scene
            List<String> items = new List<String>();
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Type_Object")) {
                if (go.activeInHierarchy) {
                    items.Add(go.name);
                }

            }
            return items;
        }

        static public List<String> GetEnv() {
            // get root objects in scene
            List<String> items = new List<String>();
            
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Env")) {
                if (go.activeInHierarchy) {
                    items.Add(go.name);
                }

            }
            return items;
        }

        static void setConvex()
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Type_Object"))
            {
                if (go.activeInHierarchy)
                {
                    foreach (MeshCollider mc in go.GetComponentsInChildren<MeshCollider>())
                    {
                        mc.convex = true;
                    }
                }

            }
        }
        static void setRigidBody()
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Env"))
            {
                if (go.activeInHierarchy)
                {
                    Rigidbody gameObjectsRigidBody = go.AddComponent<Rigidbody>();
                    if (gameObjectsRigidBody==null)
                    {
                        gameObjectsRigidBody = go.GetComponent<Rigidbody>();
                    }
                    gameObjectsRigidBody.mass = 10;
                    gameObjectsRigidBody.drag = 10;
                    gameObjectsRigidBody.angularDrag = 10;
                    gameObjectsRigidBody.useGravity = false;
                }

            }
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Type_Object"))
            {
                if (go.activeInHierarchy)
                {
                    Rigidbody gameObjectsRigidBody = go.AddComponent<Rigidbody>();
                    if (gameObjectsRigidBody == null)
                    {
                        gameObjectsRigidBody = go.GetComponent<Rigidbody>();
                    }
                    gameObjectsRigidBody.mass = 10;
                    gameObjectsRigidBody.drag = 10;
                    gameObjectsRigidBody.angularDrag = 10;
                    gameObjectsRigidBody.useGravity = false;
                }

            }
        }
        IEnumerator Capture()
        {
            float counter = 10f;
            string dir = Directory.GetCurrentDirectory();
            Debug.Log(dir);
            dir = Directory.GetParent(dir).ToString();
            if (!Directory.Exists(dir+"/Output/" + timestampPrefix + "/"))
{
                Directory.CreateDirectory(dir+"/Output/" + timestampPrefix + "/");
            }
            while (counter > 0 && !done)
            {
                yield return new WaitForSeconds(0.2f);
                string file = dir + "/Output/" + timestampPrefix + "/" + (DateTime.Now.ToString("MM_dd_yyyy_HHmmss.fffffff")) + ".png";
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log(file);
                counter -= 0.2f;
                index++;
            }
            
            
        }

        IEnumerator ProcessNetworkRequest()
        {
            // There is not always a character
            // StopCharacterAnimation(character);

            sceneCameras = ScriptUtils.FindAllCameras(transform);
            numSceneCameras = sceneCameras.Count;
            cameras = sceneCameras.ToList();
            CameraUtils.DeactivateCameras(cameras);
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();
            EnvironmentGraphCreator currentGraphCreator = null;
            EnvironmentGraph currentGraph = null;
            int expandSceneCount = 0;

            InitRooms();
            CameraExpander.ResetCameraExpander();
            DataProviders dataProviders = new DataProviders();
            while (true) {
                //Debug.Log("Waiting for request");
                 

                yield return new WaitUntil(() => networkRequest != null);

                //Debug.Log("Processing");

                NetworkResponse response = new NetworkResponse() { id = networkRequest.id };

                if (networkRequest.action == "get_objects") {
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(GetObjects());
                    
                } 
                else if (networkRequest.action == "randomize_scene") {
                    Debug.Log("Random");
                    randomize_hanlder(GetEnv());
                    randomize_hanlder(GetObjects());
                    log(GetEnv());
                    log(GetObjects());
                    response.message = JsonConvert.SerializeObject(GetEnv());
                    response.success = true;
                    setRigidBody();
                    setConvex();
                }
                else if (networkRequest.action == "switch_camera")
                {
                    IList<int> indexes = networkRequest.intParams;
                    //CameraConfig camera_config = JsonConvert.DeserializeObject<CameraConfig>(networkRequest.stringParams[0]);
                    //int index = camera_config.index;

                    bool switch_camera = CameraUtils.ChangeCamera(cameras, indexes);
                    if (switch_camera)
                    {
                        response.success = true;
                    } 
                    else
                    {
                        response.success = false;
                        response.message = "Invalid camera index";
                    }

                    
                    

                } else if (networkRequest.action == "capture_screen")
                {
                    response.success = true;
                    ScreenCapture.CaptureScreenshot("test");
                }
                else if (networkRequest.action == "camera_image") {
                    IList<int> indexes = networkRequest.intParams;
                    if (!CheckCameraIndexes(indexes, cameras.Count))
                    {
                        response.success = false;
                        response.message = "Invalid parameters";
                    }
                    else
                    {
                        ImageConfig config = new ImageConfig();
                        int cameraPass = ParseCameraPass(config.mode);
                        //int cameraPass = 0;
                        if (cameraPass == -1)
                        {
                            response.success = false;
                            response.message = "The current camera mode does not exist";
                        }
                        else
                        {

                            foreach (int i in indexes)
                            {
                                //CameraExpander.AdjustCamera(cameras[i]);
                                cameras[i].gameObject.SetActive(true);
                            }
                            yield return new WaitForEndOfFrame();

                            List<string> imgs = new List<string>();

                            foreach (int i in indexes)
                            {
                                byte[] bytes = CameraUtils.RenderImage(cameras[i], config.image_width, config.image_height, cameraPass);
                                cameras[i].gameObject.SetActive(false);
                                imgs.Add(Convert.ToBase64String(bytes));
                            }
                            response.success = true;
                            response.message_list = imgs;
                        }

                    }
                } else if (networkRequest.action == "environment_graph") {
                    if (currentGraph == null) {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        var graph = currentGraphCreator.CreateGraph(transform);
                        response.success = true;
                        response.message = JsonConvert.SerializeObject(graph);
                        currentGraph = graph;
                    } else {
                        //s_GetGraphMarker.Begin();
                        using (s_GetGraphMarker.Auto())
                            currentGraph = currentGraphCreator.GetGraph();
                        //s_GetGraphMarker.End();
                        response.success = true;
                    }


                    using (s_GetMessageMarker.Auto()) {
                        response.message = JsonConvert.SerializeObject(currentGraph);
                    }
                    response.success = true;
                } else if (networkRequest.action == "expand_scene") {
                    cameraInitializer.initialized = false;
                    List<IEnumerator> animationEnumerators = new List<IEnumerator>();

                    try {
                        if (currentGraph == null) {
                            currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = currentGraphCreator.CreateGraph(transform);
                        }

                        ExpanderConfig config = JsonConvert.DeserializeObject<ExpanderConfig>(networkRequest.stringParams[0]);
                        Debug.Log("Successfully de-serialized object");
                        EnvironmentGraph graph = EnvironmentGraphCreator.FromJson(networkRequest.stringParams[1]);


                        if (config.randomize_execution) {
                            InitRandom(config.random_seed);
                        }

                        // Maybe we do not need this
                        if (config.animate_character)
                        {
                            foreach (CharacterControl c in characters)
                            {
                                c.GetComponent<Animator>().speed = 1;
                            }
                        }

                        SceneExpander graphExpander = new SceneExpander(dataProviders) {
                            Randomize = config.randomize_execution,
                            IgnoreObstacles = config.ignore_obstacles,
                            AnimateCharacter = config.animate_character,
                            TransferTransform = config.transfer_transform
                        };

                        if (networkRequest.stringParams.Count > 2 && !string.IsNullOrEmpty(networkRequest.stringParams[2])) {
                            graphExpander.AssetsMap = JsonConvert.DeserializeObject<IDictionary<string, List<string>>>(networkRequest.stringParams[2]);
                        }
                        // TODO: set this with a flag
                        bool exact_expand = false;
                        List<GameObject> added_chars = new List<GameObject>();
                        graphExpander.ExpandScene(transform, graph, currentGraph, expandSceneCount, added_chars, exact_expand);
                        foreach(GameObject added_char in added_chars)
                        {
                            CharacterControl cc = added_char.GetComponent<CharacterControl>();
                            added_char.SetActive(true);
                            var nma = added_char.GetComponent<NavMeshAgent>();
                            nma.Warp(added_char.transform.position);

                            characters.Add(cc);
                            CurrentStateList.Add(null);
                            numCharacters++;
                            List<Camera> charCameras = CameraExpander.AddCharacterCameras(added_char.gameObject, transform, "");
                            CameraUtils.DeactivateCameras(charCameras);
                            cameras.AddRange(charCameras);
                            cameraInitializer.initialized = false;


                        }
                        SceneExpanderResult result = graphExpander.GetResult();

                        response.success = result.Success;
                        response.message = JsonConvert.SerializeObject(result.Messages);

                        // TODO: Do we need this?
                        //currentGraphCreator.SetGraph(graph);
                        currentGraph = currentGraphCreator.UpdateGraph(transform);
                        animationEnumerators.AddRange(result.enumerators);
                        expandSceneCount++;
                    } catch (JsonException e) {
                        response.success = false;
                        response.message = "Error deserializing params: " + e.Message;
                    } catch (Exception e) {
                        response.success = false;
                        response.message = "Error processing input graph: " + e.Message;
                        Debug.Log(e);
                    }

                    foreach (IEnumerator e in animationEnumerators) {
                        yield return e;
                    }
                    foreach (CharacterControl c in characters)
                    {
                        c.GetComponent<Animator>().speed = 0;
                    }

                } 
                else if (networkRequest.action == "add_character")
                {
                    CharacterConfig config = JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
                    CharacterControl newchar;
                    newchar = AddCharacter(config.character_resource, false, config.mode, config.character_position, config.initial_room);

                    if (newchar != null)
                    {
                        int index = characters.Count;
                        if (newchar.name == "Baby")
                        {
                            cameras.Add(newchar.GetComponentInChildren<Camera>());
                        }
                        Debug.Log("add character "+index +": position: " + newchar.transform.position + ", rotation:"+newchar.transform.rotation);
          
                        using (StreamWriter writer = new StreamWriter(actionLog, append:true)) {
                            writer.WriteLine("add character " + index + ": position: " + newchar.transform.position + ", rotation:" + newchar.transform.rotation);
                        }

                        characters.Add(newchar);
                        CurrentStateList.Add(null);
                        numCharacters++;
                        //List<Camera> charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, transform, "");
                        //CameraUtils.DeactivateCameras(charCameras);
                        //cameras.AddRange(charCameras);
                        //cameraInitializer.initialized = false;

                        response.success = true;
                    }
                    else
                    {
                        response.success = false;
                    }

                }

                else if (networkRequest.action == "render_script") {
                    if (numCharacters == 0)
                    {
                        networkRequest = null;

                        response.message = "No character added yet!";
                        response.success = false;

                        commServer.UnlockProcessing(response);
                        continue;
                    }

                    ExecutionConfig config = JsonConvert.DeserializeObject<ExecutionConfig>(networkRequest.stringParams[0]);
                    using (StreamWriter writer = new StreamWriter(actionLog, append:true)) {
                        writer.WriteLine(networkRequest.stringParams[1]);
                    }

                    if (config.randomize_execution) {
                        InitRandom(config.random_seed);
                    }

                    string outDir = Path.Combine(config.output_folder, config.file_name_prefix);
                    if (!config.skip_execution) {
                        Directory.CreateDirectory(outDir);
                    }
                    IObjectSelectorProvider objectSelectorProvider;
                    if (config.find_solution)
                        objectSelectorProvider = new ObjectSelectionProvider(dataProviders.NameEquivalenceProvider);
                    else
                        objectSelectorProvider = new InstanceSelectorProvider(currentGraph);
                    IList<GameObject> objectList = ScriptUtils.FindAllObjects(transform);
                    // TODO: check if we need this
                    if (recorders.Count != numCharacters)
                    {
                        createRecorders(config);
                    }
                    else
                    {
                        updateRecorders(config);
                    }

                    if (!config.skip_execution)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            if (config.skip_animation)
                                characters[i].SetSpeed(150.0f);
                            else
                                characters[i].SetSpeed(1.0f);
                        }
                    }
                    if (config.skip_animation)
                    {
                        UtilsAnnotator.SetSkipAnimation(transform);
                    }
                    // initialize the recorders
                    if (config.record)
                    {
                        done = false;
                        StartCoroutine(Capture());

                    }
                    if (sExecutors.Count != numCharacters)
                    {
                        sExecutors = InitScriptExecutors(config, objectSelectorProvider, sceneCameras, objectList);
                    }

                    bool parseSuccess;
                    try
                    {
                        List<string> scriptLines = networkRequest.stringParams.ToList();
                        scriptLines.RemoveAt(0);
                        // not ok, has video
                        for (int i = 0; i < numCharacters; i++)
                        {
                            sExecutors[i].ClearScript();
                            sExecutors[i].smooth_walk = !config.skip_animation;
                        }

                        ScriptReader.ParseScript(sExecutors, scriptLines, dataProviders.ActionEquivalenceProvider);
                        parseSuccess = true;
                    }
                    catch (Exception e)
                    {
                        parseSuccess = false;
                        response.success = false;
                        response.message = $"Error parsing script: {e.Message}";
                        continue;
                    }

                    //s_SimulatePerfMarker.Begin();


                    if (parseSuccess)
                    {
                        List<Tuple<int, Tuple<String, String>>> error_messages = new List<Tuple<int, Tuple<String, String>>>();
                        if (!config.find_solution)
                            error_messages = ScriptChecker.SolveConflicts(sExecutors);
                        for (int i = 0; i < numCharacters; i++)
                        {
                            StartCoroutine(sExecutors[i].ProcessAndExecute(config.recording, this));
                        }

                        while (finishedChars != numCharacters)
                        {
                            yield return new WaitForSeconds(0.01f);
                        }

                        // Add back errors from concurrent actions

                        for (int error_index = 0; error_index < error_messages.Count; error_index++)
                        {
                            sExecutors[error_messages[error_index].Item1].report.AddItem(error_messages[error_index].Item2.Item1, error_messages[error_index].Item2.Item2);
                        }

                    }

                    //s_SimulatePerfMarker.End();

                    finishedChars = 0;
                    ScriptExecutor.actionsPerLine = new Hashtable();
                    ScriptExecutor.currRunlineNo = 0;
                    ScriptExecutor.currActionsFinished = 0;

                    response.message = "";
                    response.success = false;
                    bool[] success_per_agent = new bool[numCharacters];
                    
                    bool agent_failed_action = false;
                    Dictionary <int, Dictionary <String, String> > messages = new Dictionary<int, Dictionary<String, String> > ();
                    if (!config.recording)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            Dictionary<String, String> current_message = new Dictionary<String, String>();

                            if (!sExecutors[i].Success)
                            {
                                String message = "";
                                message += $"ScriptExcutor {i}: ";
                                message += sExecutors[i].CreateReportString();
                                message += "\n";
                                current_message["message"] = message;

                                success_per_agent[i] = false;
                                agent_failed_action = true;
                            }
                            else
                            {
                                current_message["message"] = "Success";
                                response.success = true;
                                success_per_agent[i] = true;
                            }
                            messages[i] = current_message;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            Dictionary<String, String> current_message = new Dictionary<String, String>();
                            Recorder rec = recorders[i];
                            if (!sExecutors[i].Success)
                            {
                                //response.success = false;
                                String message = "";
                                message += $"ScriptExcutor {i}: ";
                                message += sExecutors[i].CreateReportString();
                                message += "\n";
                                current_message["message"] = message;
                            }
                            else if (rec.Error != null)
                            {
                                //Directory.Delete(rec.OutputDirectory);
                                //response.success = false;
                                agent_failed_action = true;
                                String message = "";
                                message += $"Recorder {i}: ";
                                message += recorder.Error.Message;
                                message += "\n";
                                rec.Recording = false;
                                current_message["message"] = message;
                            }
                            else
                            {
                                current_message["message"] = "Success";
                                response.success = true;
                                success_per_agent[i] = true;
                                    rec.MarkTermination();
                                rec.Recording = false;
                                rec.Animator.speed = 0;
                                CreateSceneInfoFile(rec.OutputDirectory, new SceneData()
                                {
                                    frameRate = config.frame_rate,
                                    sceneName = SceneManager.GetActiveScene().name
                                });
                                rec.CreateTextualGTs();
                            }


                            messages[i] = current_message;
                        }
                    }

                    // If any of the agent fails an action, report failure
                    if (parseSuccess)
                    {
                        done = true;
                        response.message = JsonConvert.SerializeObject(messages);
                    }
                    if (agent_failed_action)
                        response.success = false;
                    ISet<GameObject> changedObjs = new HashSet<GameObject>();
                    IDictionary<Tuple<string, int>, ScriptObjectData> script_object_changed = new Dictionary<Tuple<string, int>, ScriptObjectData>();
                    List<ActionObjectData> last_action = new List<ActionObjectData>();
                    bool single_action = true;
                    for (int char_index = 0; char_index < numCharacters; char_index++)
                    {
                        if (success_per_agent[char_index])
                        {
                            State currentState = this.CurrentStateList[char_index];
                            GameObject rh = currentState.GetGameObject("RIGHT_HAND_OBJECT");
                            GameObject lh = currentState.GetGameObject("LEFT_HAND_OBJECT");
                            EnvironmentObject obj1;
                            EnvironmentObject obj2;
                            foreach (CharacterControl x in characters) {
                                Debug.Log(x.gameObject.name);
                            }
                            if (currentGraphCreator == null) {
                                break;
                            }
                            currentGraphCreator.objectNodeMap.TryGetValue(characters[char_index].gameObject, out obj1);
                            if (obj1 == null) {
                                break;
                            }
                            Character character_graph;
                            Debug.Log(obj1);
                            
                            currentGraphCreator.characters.TryGetValue(obj1, out character_graph);
                            if (sExecutors[char_index].script.Count > 1)
                            {
                                single_action = false;
                            }
                            if (sExecutors[char_index].script.Count == 1)
                            {
                                // If only one action was executed, we will use that action to update the environment
                                // Otherwise, we will update using coordinates
                                ScriptPair script = sExecutors[char_index].script[0];
                                ActionObjectData object_script = new ActionObjectData(character_graph, script, currentState.scriptObjects);
                                last_action.Add(object_script);

                            }
                            Debug.Assert(character_graph != null);
                            if (lh != null)
                            {
                                currentGraphCreator.objectNodeMap.TryGetValue(lh, out obj2);
                                character_graph.grabbed_left = obj2;

                            }
                            else
                            {
                                character_graph.grabbed_left = null;
                            }
                            if (rh != null)
                            {
                                currentGraphCreator.objectNodeMap.TryGetValue(rh, out obj2);
                                character_graph.grabbed_right = obj2;
                            }
                            else
                            {

                                character_graph.grabbed_right = null;
                            }

                            IDictionary<Tuple<string, int>, ScriptObjectData> script_objects_state = currentState.scriptObjects;
                            foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in script_objects_state)
                            {
                                if (!entry.Value.GameObject.IsRoom())
                                {
                                    //if (entry.Key.Item1 == "cutleryknife")
                                    //{

                                    //    //int instance_id = entry.Value.GameObject.GetInstanceID();
                                    //}
                                    changedObjs.Add(entry.Value.GameObject);
                                }

                                if (entry.Value.OpenStatus != OpenStatus.UNKNOWN)
                                {
                                    //if (script_object_changed.ContainsKey(entry.Key))
                                    //{
                                    //    Debug.Log("Error, 2 agents trying to interact at the same time");
                                    if (sExecutors[char_index].script.Count > 0 && sExecutors[char_index].script[0].Action.Name.Instance == entry.Key.Item2)
                                    {
                                        script_object_changed[entry.Key] = entry.Value;
                                    }

                                    //}
                                    //else
                                    //{

                                    //}
                                }

                            }
                            foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in script_object_changed)
                            {
                                if (entry.Value.OpenStatus == OpenStatus.OPEN)
                                {
                                    currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Remove(Utilities.ObjectState.CLOSED);
                                    currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Add(Utilities.ObjectState.OPEN);
                                }
                                else if (entry.Value.OpenStatus == OpenStatus.CLOSED)
                                {
                                    currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Remove(Utilities.ObjectState.OPEN);
                                    currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Add(Utilities.ObjectState.CLOSED);
                                }
                            }
                        }

                        using (s_UpdateGraph.Auto()) {
                            if (currentGraph!=null) {
                                if (single_action)
                                    currentGraph = currentGraphCreator.UpdateGraph(transform, null, last_action);
                                else
                                    currentGraph = currentGraphCreator.UpdateGraph(transform, changedObjs);
                            }
                            
                        }
                    }

                } else if (networkRequest.action == "reset") {
                    
                    cameraInitializer.initialized = false;
                    networkRequest.action = "environment_graph"; // return result after scene reload
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
                    //cc = null;
                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    cameras = cameras.GetRange(0, numSceneCameras);
                    CameraExpander.ResetCameraExpander();

                    if (networkRequest.intParams?.Count > 0)
                    {
                        int sceneIndex = networkRequest.intParams[0];

                        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
                        {

                            SceneManager.LoadScene(sceneIndex);

                            yield break;
                        }
                        else
                        {
                            response.success = false;
                            response.message = "Invalid scene index";
                        }
                    }
                    else
                    {

                        Debug.Log("Reloading");
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        DeleteChar();
                        Debug.Log("Reloaded");
                        yield break;
                    }
                }
                else if (networkRequest.action == "idle") {
                    response.success = true;
                    response.message = "";
                } else {
                    response.success = false;
                    response.message = "Unknown action " + networkRequest.action;
                }
                
                // Ready for next request
                networkRequest = null;

                commServer.UnlockProcessing(response);
            }
        }

        private bool SeenByCamera(Camera camera, Transform transform)
        {
            Vector3 origin = camera.transform.position;
            Bounds bounds = GameObjectUtils.GetBounds(transform.gameObject);
            List<Vector3> checkPoints = new List<Vector3>();
            Vector3 bmin = bounds.min;
            Vector3 bmax = bounds.max;
            checkPoints.Add(bmin);
            checkPoints.Add(bmax);
            checkPoints.Add(new Vector3(bmin.x, bmin.y, bmax.z));
            checkPoints.Add(new Vector3(bmin.x, bmax.y, bmin.z));
            checkPoints.Add(new Vector3(bmax.x, bmin.y, bmin.z));
            checkPoints.Add(new Vector3(bmin.x, bmax.y, bmax.z));
            checkPoints.Add(new Vector3(bmax.x, bmin.y, bmax.z));
            checkPoints.Add(new Vector3(bmax.x, bmax.y, bmin.z));
            checkPoints.Add(bounds.center);

            return checkPoints.Any(p => checkHit(origin, p, bounds));
        }

        private bool checkHit(Vector3 origin, Vector3 dest, Bounds bounds, double yTolerance = 0.001)
        {


            RaycastHit hit;

            if (!Physics.Raycast(origin, (dest - origin).normalized, out hit)) return false;

            if (!bounds.Contains(hit.point))
            {
                return false;
            }
            // if (Mathf.Abs(hit.point.y - dest.y) > yTolerance) return false;

            return true;
        }

        private FrontCameraControl CreateFrontCameraControls(GameObject character)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform, CameraExpander.INT_FORWARD_VIEW_CAMERA_NAME);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == CameraExpander.FORWARD_VIEW_CAMERA_NAME);
            return new FrontCameraControl(camera);
        }

        private FixedCameraControl CreateFixedCameraControl(GameObject character, string cameraName, bool focusToObject)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform, cameraName);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == cameraName);
            return new FixedCameraControl(camera) { DoFocusObject = focusToObject };
        }

        private void updateRecorder(ExecutionConfig config, string outDir, Recorder rec)
        {
            ICameraControl cameraControl = null;
            if (config.recording)
            {
                // Add extra cams
                if (rec.CamCtrls == null)
                    rec.CamCtrls = new List<ICameraControl>();
                if (config.camera_mode.Count > rec.CamCtrls.Count)
                {
                    for (int extra_cam = 0; extra_cam < (config.camera_mode.Count - rec.CamCtrls.Count);)
                    {
                        rec.CamCtrls.Add(null);
                    }
                }
                else
                {
                    for (int extra_cam = config.camera_mode.Count; extra_cam < rec.CamCtrls.Count; extra_cam++)
                    {
                        rec.CamCtrls[extra_cam] = null;
                    }
                }

                for (int cam_id = 0; cam_id < config.camera_mode.Count; cam_id++)
                {

                    if (rec.CamCtrls[cam_id] != null && cam_id < rec.currentCameraMode.Count && rec.currentCameraMode[cam_id].Equals(config.camera_mode[cam_id]))
                    {
                        rec.CamCtrls[cam_id].Activate(true);
                    }
                    else
                    {
                        if (rec.CamCtrls[cam_id] != null)
                        {
                            rec.CamCtrls[cam_id].Activate(false);
                        }

                        CharacterControl chc = characters[rec.charIdx];
                        int index_cam = 0;
                        bool cam_ctrl = false;
                        if (int.TryParse(config.camera_mode[cam_id], out index_cam))
                        {
                            //Camera cam = new Camera();
                            //cam.CopyFrom(sceneCameras[index_cam]);
                            Camera cam = cameras[index_cam];
                            cameraControl = new FixedCameraControl(cam);
                            cam_ctrl = true;
                        }
                        else
                        {
                            if (config.camera_mode[cam_id] == "PERSON_FRONT")
                            {
                                cameraControl = CreateFrontCameraControls(chc.gameObject);
                                cam_ctrl = true;
                            }
                            else
                            {
                                if (config.camera_mode[cam_id] == "AUTO")
                                {
                                    AutoCameraControl autoCameraControl = new AutoCameraControl(sceneCameras, chc.transform, new Vector3(0, 1.0f, 0));
                                    autoCameraControl.RandomizeCameras = config.randomize_execution;
                                    autoCameraControl.CameraChangeEvent += rec.UpdateCameraData;
                                    cameraControl = autoCameraControl;
                                    cam_ctrl = true;
                                }
                                else
                                {

                                    if (CameraExpander.HasCam(config.camera_mode[cam_id]))
                                    {
                                        cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.char_cams[config.camera_mode[cam_id]].name, false);
                                        cam_ctrl = true;
                                    }
                                }
                            }


                        }
                        if (cam_ctrl)
                        {
                            cameraControl.Activate(true);
                            rec.CamCtrls[cam_id] = cameraControl;
                        }

                    }
                }
            }
            //Debug.Log($"config.recording : {config.recording}");
            //Debug.Log($"cameraCtrl is not null2? : {cameraControl != null}");
            rec.Recording = config.recording;
            rec.currentframeNum = 0;
            rec.currentCameraMode = config.camera_mode;
            rec.FrameRate = config.frame_rate;
            rec.imageSynthesis = config.image_synthesis;
            rec.savePoseData = config.save_pose_data;
            rec.saveSceneStates = config.save_scene_states;
            rec.FileName = config.file_name_prefix;
            rec.ImageWidth = config.image_width;
            rec.ImageHeight = config.image_height;
            rec.OutputDirectory = outDir;
        }

        private void createRecorder(ExecutionConfig config, string outDir, int index)
        {
            Recorder rec = recorders[index];
            updateRecorder(config, outDir, rec);
        }

        private void createRecorders(ExecutionConfig config)
        {
            // For the 1st Recorder.
            recorders.Clear();
            recorders.Add(GetComponent<Recorder>());
            recorders[0].charIdx = 0;
            if (numCharacters > 1)
            {
                for (int i = 1; i < numCharacters; i++)
                {
                    recorders.Add(gameObject.AddComponent<Recorder>() as Recorder);
                    recorders[i].charIdx = i;
                }
            }

            for (int i = 0; i < numCharacters; i++)
            {
                string outDir = Path.Combine(config.output_folder, config.file_name_prefix, i.ToString());
                Directory.CreateDirectory(outDir);
                createRecorder(config, outDir, i);
            }
        }

        private void updateRecorders(ExecutionConfig config)
        {
            for (int i = 0; i < numCharacters; i++)
            {
                string outDir = Path.Combine(config.output_folder, config.file_name_prefix, i.ToString());
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }
                updateRecorder(config, outDir, recorders[i]);

            }
        }

        private List<ScriptExecutor> InitScriptExecutors(ExecutionConfig config, IObjectSelectorProvider objectSel, List<Camera> sceneCameras, IList<GameObject> objectList)
        {
            List<ScriptExecutor> sExecutors = new List<ScriptExecutor>();

            InteractionCache interaction_cache = new InteractionCache();
            for (int i = 0; i < numCharacters; i++)
            {
                CharacterControl chc = characters[i];
                chc.DoorControl.Update(objectList);


                // Initialize the scriptExecutor for the character
                ScriptExecutor sExecutor = new ScriptExecutor(objectList, dataProviders.RoomSelector, objectSel, recorders[i], i, interaction_cache, !config.skip_animation);
                sExecutor.RandomizeExecution = config.randomize_execution;
                sExecutor.ProcessingTimeLimit = config.processing_time_limit;
                sExecutor.SkipExecution = config.skip_execution;
                sExecutor.AutoDoorOpening = false;

                sExecutor.Initialize(chc, recorders[i].CamCtrls);
                sExecutors.Add(sExecutor);
            }
            return sExecutors;
        }


        private void InitCharacterForCamera(GameObject character)
        {
            // This helps with rendering issues (disappearing suit on some cameras)
            SkinnedMeshRenderer[] mrCompoments = character.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer mr in mrCompoments) {
                mr.updateWhenOffscreen = true;
                mr.enabled = false;
                mr.enabled = true;
            }
        }

        private void InitRooms()
        {
            List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);

            foreach (GameObject r in rooms) {
                r.AddComponent<Properties_room>();
            }
        }

        private void InitRandom(int seed)
        {
            if (seed >= 0) {
                UnityEngine.Random.InitState(seed);
            } else {
                UnityEngine.Random.InitState((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            }
        }

        private Dictionary<int, float[]> GetInstanceColoring(IList<EnvironmentObject> graphNodes)
        {
            Dictionary<int, float[]> result = new Dictionary<int, float[]>();

            foreach (EnvironmentObject eo in graphNodes) {
                if (eo.transform == null) {
                    result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                } else {
                    int instId = eo.transform.gameObject.GetInstanceID();
                    Color instColor;

                    if (ColorEncoding.instanceColor.TryGetValue(instId, out instColor)) {
                        result[eo.id] = new float[] { instColor.r, instColor.g, instColor.b };
                    } else {
                        result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                    }
                }
            }
            return result;
        }

        private void StopCharacterAnimation(GameObject character)
        {
            Animator animator = character.GetComponent<Animator>();
            if (animator != null) {
                animator.speed = 0;
            }
        }

        private bool CheckCameraIndexes(IList<int> indexes, int count)
        {
            if (indexes == null) return false;
            else if (indexes.Count == 0) return true;
            else return indexes.Min() >= 0 && indexes.Max() < count; 
        }

        private int ParseCameraPass(string string_mode)
        {
            if (string_mode == null) return 0;
            else return Array.IndexOf(ImageSynthesis.PASSNAMES, string_mode.ToLower());
        }

        private void CreateSceneInfoFile(string outDir, SceneData sd)
        {
            using (StreamWriter sw = new StreamWriter(Path.Combine(outDir, "sceneInfo.json"))) {
                sw.WriteLine(JsonUtility.ToJson(sd, true));
            }
        }
        private void log(List<String> prefabs) {
            for(int prefabIndex = 0; prefabIndex < prefabs.Count; prefabIndex++) {
                string name = prefabs[prefabIndex];
                GameObject go = GameObject.Find(name);
                if (go == null) {
                    continue;
                }
                using (StreamWriter writer = new StreamWriter(actionLog, append: true)) {
                    writer.WriteLine(go.name + ": position: " + go.transform.position + ", rotation:" + go.transform.rotation);
                }
            }
            }
        private void randomize_hanlder(List<String> prefabs) {
            for (int prefabIndex = 0;prefabIndex<prefabs.Count;prefabIndex++) {
                string name = prefabs[prefabIndex];
                string className = dataProviders.ObjectSelectorProvider.GetClassName(name);
                List<string> assets = dataProviders.ObjectSelectorProvider.GetClassAssets(name);
                System.Random rnd = new System.Random();
                int index = rnd.Next(assets.Count);
                string assetName = assets[index];
                string path = "Props/" + className + "/" + assetName;
                //Debug.Log(path);
                replace_prefab(path, GameObject.Find(name), className);
            }
            
            return;
        }

        private void replace_prefab(string path, GameObject go, string className) {
            //Debug.Log(ScriptUtils.TransformResourceFileName(path));
            GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(path)) as GameObject;
            if (loadedObj == null) {
                Debug.Log("not found");
                return;
            }
            GameObject newPrefab = Instantiate(loadedObj, go.transform.parent) as GameObject;
            Debug.Log(className);
            Vector3 prefabSize = newPrefab.GetComponentInChildren<Renderer>().bounds.size;
            Debug.Log(prefabSize);
            go.SetActive(false);
            newPrefab.name = loadedObj.name;
            newPrefab.transform.localPosition = go.transform.localPosition;
            newPrefab.SetActive(false);
            if (newPrefab.tag=="Type_Object") {
                float spawnRadius = 0.5f;
                float collisionRadius = 0.5f;
                Vector3 random = UnityEngine.Random.insideUnitSphere;
                random.y = 0;
                Vector3 spawnPoint = go.transform.position + random*spawnRadius;
                //newPrefab.transform.position = go.transform.position;
                
                //Debug.Log(Physics.OverlapSphere(spawnPoint, collisionRadius));
                if (!Physics.CheckSphere(spawnPoint, collisionRadius)) {
                    Debug.Log("no collision");
                    newPrefab.transform.position = spawnPoint;
                }
                else{
                    Debug.Log("collision");
                }
                
                
            }

            
            //newPrefab.transform.localRotation = go.transform.localRotation;
            
            newPrefab.SetActive(true);

        }

        // Add character (prefab) at path to the scene and returns its CharacterControl
        // component
        // - If name == null or prefab does not exist, we keep the character currently in the scene
        // - Any other character in the scene is deactivated
        // - Character is "randomized" if randomizeExecution = true 
        private CharacterControl AddCharacter(string path, bool randomizeExecution, string mode, Vector3 position, string initial_room)
        {
            GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(path)) as GameObject;
            List<GameObject> sceneCharacters = ScriptUtils.FindAllCharacters(transform);

            if (loadedObj == null)
            {
                if (sceneCharacters.Count > 0) return sceneCharacters[0].GetComponent<CharacterControl>();
                else return null;
            }
            else
            {
                Transform destRoom;


                List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);
                foreach (GameObject r in rooms)
                {
                    if (r.GetComponent<Properties_room>() == null)
                        r.AddComponent<Properties_room>();
                }


                if (sceneCharacters.Count > 0 && mode == "random") destRoom = sceneCharacters[0].transform.parent.transform;
                else
                {
                    string room_name = "livingroom";
                    if (mode == "fix_room")
                    {
                        room_name = initial_room;
                    }
                    int index = 0;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (rooms[i].name.Contains(room_name.Substring(1)))
                        {
                            index = i;
                        }
                    }
                    destRoom = rooms[index].transform;
                }

                if (mode == "fix_position")
                {
                    bool contained_somewhere = false;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (GameObjectUtils.GetRoomBounds(rooms[i]).Contains(position))
                        {
                            contained_somewhere = true;
                        }
                    }
                    if (!contained_somewhere)
                        return null;
                }

                GameObject newCharacter = Instantiate(loadedObj, destRoom) as GameObject;

                newCharacter.name = loadedObj.name;
                ColorEncoding.EncodeGameObject(newCharacter);
                CharacterControl cc = newCharacter.GetComponent<CharacterControl>();

                // sceneCharacters.ForEach(go => go.SetActive(false));
                newCharacter.SetActive(true);

                if (mode == "random")
                {
                    if (sceneCharacters.Count == 0 || randomizeExecution)
                    {
                        //newCharacter.transform.position = ScriptUtils.FindRandomCCPosition(rooms, cc);
                        var nma = newCharacter.GetComponent<NavMeshAgent>();
                        nma.Warp(ScriptUtils.FindRandomCCPosition(rooms, cc));
                        newCharacter.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
                    }
                    else
                    {
                        //newCharacter.transform.position = sceneCharacters[0].transform.position;
                        var nma = newCharacter.GetComponent<NavMeshAgent>();
                        nma.Warp(sceneCharacters[0].transform.position);
                    }
                }
                else if (mode == "fix_position")
                {
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(position);
                }
                else if (mode == "fix_room")
                {
                    List<GameObject> rooms_selected = new List<GameObject>();
                    foreach (GameObject room in rooms)
                    {
                        if (room.name == destRoom.name)
                        {
                            rooms_selected.Add(room);
                        }
                    }
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(ScriptUtils.FindRandomCCPosition(rooms_selected, cc));
                    newCharacter.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
                }
                // Must be called after correct char placement so that char's location doesn't change
                // after instantiation.
                //if (recorder.saveSceneStates)
                //{
                //    State_char sc = newCharacter.AddComponent<State_char>();
                //    sc.Initialize(rooms, recorder.sceneStateSequence);
                //    cc.stateChar = sc;
                //}

                return cc;
            }
        }

        public void PlaceCharacter(GameObject character, List<GameObject> rooms)
        {
            CharacterControl cc = character.GetComponent<CharacterControl>();
            var nma = character.GetComponent<NavMeshAgent>();
            nma.Warp(ScriptUtils.FindRandomCCPosition(rooms, cc));
            character.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
        }
    }

    internal class InstanceSelectorProvider : IObjectSelectorProvider
    {
        private Dictionary<int, GameObject> idObjectMap;

        public InstanceSelectorProvider(EnvironmentGraph currentGraph)
        {
            idObjectMap = new Dictionary<int, GameObject>(); 

            foreach (EnvironmentObject eo in currentGraph.nodes) {
                if (eo.transform != null) {
                    idObjectMap[eo.id] = eo.transform.gameObject;
                }
            }
        }

        public IObjectSelector GetSelector(string name, int instance)
        {
            GameObject go;

            if (idObjectMap.TryGetValue(instance, out go)) return new FixedObjectSelector(go);
            else return EmptySelector.Instance;
        }
    }

    class OneTimeInitializer
    {
        public bool initialized = false;
        private Action defaultAction;

        public OneTimeInitializer()
        {
            defaultAction = () => { };
        }

        public OneTimeInitializer(Action defaultAction)
        {
            this.defaultAction = defaultAction;
        }

        public void Initialize(Action action)
        {
            if (!initialized) {
                action();
                initialized = true;
            }
        }

        public void Initialize()
        {
            Initialize(defaultAction);
        }
    }

    public class RecorderConfig
    {
        public string output_folder = "Output/";
        public string file_name_prefix = "script";
        public int frame_rate = 5;
        public List<string> image_synthesis = new List<string>();
        public bool save_pose_data = false;
        public bool save_scene_states = false;
        public bool randomize_recording = false;
        public int image_width = 640;
        public int image_height = 480;
        public List<string> camera_mode = new List<string>();
    }

    public class ImageConfig : RecorderConfig
    {
        public string mode = "normal";
    }

    public class CameraConfig
    {
        //public Vector3 rotation = new Vector3(0.0f, 0.0f, 0.0f);
        //public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
        //public float focal_length = 0.0f;
        //public string camera_name = "default";
        public int index = 0;

    }

    public class ExpanderConfig
    {
        public bool randomize_execution = false;
        public int random_seed = -1;
        public bool ignore_obstacles = false;
        public bool animate_character = false;
        public bool transfer_transform = true;
    }

    public class CharacterConfig
    {
        public int char_index = 0;
        public string character_resource = "Chars/Teacher";
        public Vector3 character_position = new Vector3(0.0f, 0.0f, 0.0f);
        public string initial_room = "livingroom";
        public string mode = "random";
    }

    public class ExecutionConfig : RecorderConfig
    {
        public bool find_solution = true;
        public bool randomize_execution = false;
        public int random_seed = -1;
        public int processing_time_limit = 10;
        public bool recording = false;
        public bool record = false;
        public bool skip_execution = false;
        public bool skip_animation = false;
    }

    public class DataProviders
    {
        public NameEquivalenceProvider NameEquivalenceProvider { get; private set; }
        public DescriptionProvider DescriptionProvider { get; private set; }
        public ActionEquivalenceProvider ActionEquivalenceProvider { get; private set; }
        public AssetsProvider AssetsProvider { get; private set; }
        public ObjectSelectionProvider ObjectSelectorProvider { get; private set; }
        public RoomSelector RoomSelector { get; private set; }
        public ObjectPropertiesProvider ObjectPropertiesProvider { get; private set; }
        public Dictionary<string, string> AssetPathMap;

        public DataProviders()
        {
            Initialize();
        }

        private void Initialize()
        {
            NameEquivalenceProvider = new NameEquivalenceProvider("Data/class_name_equivalence");
            DescriptionProvider = new DescriptionProvider("Data/prefab_description");
            ActionEquivalenceProvider = new ActionEquivalenceProvider("Data/action_mapping");
            AssetsProvider = new AssetsProvider("Data/object_prefabs");
            AssetPathMap = BuildPathMap("Data/object_prefabs");
            ObjectSelectorProvider = new ObjectSelectionProvider(NameEquivalenceProvider);
            RoomSelector = new RoomSelector(NameEquivalenceProvider);
            ObjectPropertiesProvider = new ObjectPropertiesProvider(NameEquivalenceProvider);
        }

        private Dictionary<string, string> BuildPathMap(string resourceName)
        {
            Dictionary<string, string> result = new Dictionary<string, string> ();
            List<string> all_prefabs = new List<string>();
            TextAsset txtAsset = Resources.Load<TextAsset>(resourceName);
            var tmpAssetsMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txtAsset.text);

            foreach (var e in tmpAssetsMap)
            {
                foreach (var str_prefab in e.Value)
                {
                    string prefab_name = Path.GetFileNameWithoutExtension(str_prefab);
                    result[prefab_name] = str_prefab;
                }

            }
            return result;
        }
    }

    public static class DriverUtils
    {
        // For each argument in args of form '--name=value' or '-name=value' returns item ("name", "value") in 
        // the result dicitionary.
        public static IDictionary<string, string> GetParameterValues(string[] args)
        {
            Regex regex = new Regex(@"-{1,2}([^=]+)=([^=]+)");
            var result = new Dictionary<string, string>();

            foreach (string s in args) {
                Match match = regex.Match(s);
                if (match.Success) {
                    result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
                }
            }
            return result;
        }
    }

}