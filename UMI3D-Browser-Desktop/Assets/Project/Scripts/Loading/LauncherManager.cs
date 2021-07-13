/*
Copyright 2019 Gfi Informatique

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

using BrowserDesktop.Cursor;
using System.IO;
using System.Collections.Generic;
using umi3d.cdk;
using BrowserDesktop.Controller;
using BeardedManStudios.Forge.Networking;

public class LauncherManager : MonoBehaviour
{
    #region Fields

    #region UI Fields

    [SerializeField]
    private UIDocument uiDocument = null;

    [SerializeField]
    private VisualTreeAsset libraryEntryTreeAsset = null;
    [SerializeField]
    private VisualTreeAsset dialogueBoxTreeAsset = null;
    [SerializeField]
    private VisualTreeAsset sessionEntry = null;

    private VisualElement root;

    //SetDomain screen
    VisualElement umiLogo;
    VisualElement urlScreen;
    TextField urlInput;
    Button urlEnterBtn;
    Button connectNewServBtn;

    //Advanced Connection screen
    VisualElement advancedConnectionScreen;
    TextField PortInput;
    TextField IpInput;

    //Asset libraries screen
    VisualElement librariesScreen;
    ScrollView librariesList;
    Button backMenuBnt;
    Button nextMenuBnt;

    //Favorite env
    public VisualTreeAsset favoriteEnvItemTreeAsset;
    SliderElement favoriteEnvironmentSlider;

    //Session screen
    VisualElement sessionScreen;


    #endregion

    #region Data



    private UserPreferencesManager.ServerData currentServerConnectionData;
    private List<UserPreferencesManager.ServerData> favoriteServerConnectionData = new List<UserPreferencesManager.ServerData>();

    private UserPreferencesManager.Data currentConnectionData;

    private List<UserPreferencesManager.Data> favoriteConnectionData = new List<UserPreferencesManager.Data>();

    [SerializeField]
    public string currentScene;
    [SerializeField]
    public string sceneToLoad;

    Action nextStep = null;
    Action previousStep = null;

    public LaucherOnMasterServer masterServer;


    #endregion

    #endregion

    void Start()
    {
        masterServer = new LaucherOnMasterServer();

        Debug.Assert(uiDocument != null);
        Debug.Assert(dialogueBoxTreeAsset != null);
        Debug.Assert(libraryEntryTreeAsset != null);
        root = uiDocument.rootVisualElement;

        InitUI();
   
        ResetLauncher();

        root.Insert(0, new DialogueBoxElement());
    }

    #region UI Binding

    private void InitUI()
    {
        root.Q<Label>("version").text = umi3d.UMI3DVersion.version;
        BindURLScreen();
        BindLibrariesScreen();

        root.RegisterCallback<GeometryChangedEvent>(ResizeElements);
        advancedConnectionScreen = root.Q<VisualElement>("advancedConnectionScreen");
        sessionScreen = root.Q<VisualElement>("sessionScreen");
        backMenuBnt = root.Q<Button>("back-menu-btn");
        backMenuBnt.clickable.clicked += ResetLauncher;
    }

    private void BindURLScreen()
    {
        urlScreen = root.Q<VisualElement>("url-screen");
        backMenuBnt = root.Q<Button>("back-menu-btn");
        nextMenuBnt = root.Q<Button>("nextMenuBtn");

        urlInput = urlScreen.Q<TextField>("url-input");
        urlEnterBtn = urlScreen.Q<Button>("url-enter-btn");

        IpInput = root.Q<TextField>("IpInput");
        PortInput = root.Q<TextField>("PortInput");

        urlEnterBtn.clickable.clicked += ()=> SetServer(urlInput.value);// SetDomain;
        nextStep = ()=> SetServer(urlInput.value);
        urlScreen.Q<VisualElement>("icon-open").style.display = DisplayStyle.None;

        connectNewServBtn = urlScreen.Q<Button>("newConnection");
        connectNewServBtn.clickable.clicked += () => ToggleDisplayElement(urlScreen.Q<VisualElement>("inputs-url-container"));
        connectNewServBtn.clickable.clicked += () => ToggleDisplayElement(urlScreen.Q<VisualElement>("icon-close"));
        connectNewServBtn.clickable.clicked += () => ToggleDisplayElement(urlScreen.Q<VisualElement>("icon-open"));
        //connectNewServBtn.clickable.clicked += () => urlScreen.Q<VisualElement>("newConnection").style.display = DisplayStyle.Flex;

        var manageLibraryBtn = urlScreen.Q<Button>("manage-library-btn");
        manageLibraryBtn.clickable.clicked += DisplayLibraries;

        favoriteEnvironmentSlider = new SliderElement();
        favoriteEnvironmentSlider.SetUp(urlScreen.Q<VisualElement>("slider"));

        umiLogo = urlScreen.Q<VisualElement>("logo");

        root.Q<Button>("advanced-connection-btn").clickable.clicked += BindAdvancedConnection;
    }

    private void BindLibrariesScreen()
    {
        librariesScreen = root.Q<VisualElement>("libraries-manager-screen");
        librariesList = librariesScreen.Q<ScrollView>("libraries-list");
    }

    private void BindAdvancedConnection()
    {
        urlScreen = root.Q<VisualElement>("url-screen");

        backMenuBnt = root.Q<Button>("back-menu-btn");
        nextMenuBnt = root.Q<Button>("nextMenuBtn"); 
        Action nextAction = () => SetDomain();
        if (currentNextButtonAction != null)
        {
            nextMenuBnt.clickable.clicked -= currentNextButtonAction;
        }
        nextMenuBnt.clickable.clicked += nextAction;
        currentNextButtonAction = nextAction;
        backMenuBnt.style.display = DisplayStyle.Flex;
        nextMenuBnt.style.display = DisplayStyle.Flex;
        urlScreen.style.display = DisplayStyle.None;
        sessionScreen.style.display = DisplayStyle.None;
        advancedConnectionScreen.style.display = DisplayStyle.Flex;
        var s = currentConnectionData.ip.Split(':');
        IpInput.value = s[0];
        if(s.Length > 1)
        {
            PortInput.value = s[1];
        }
        nextStep = () => SetDomain();
    }

    public bool updateBindSession = false;
    public bool updateResponse = false;
    public List<MasterServerResponse.Server> serverResponses = new List<MasterServerResponse.Server>();
    private Action enterBtnAction = null;

    private void BindSessionScreen()
    {
        nextMenuBnt = root.Q<Button>("nextMenuBtn");
        backMenuBnt.style.display = DisplayStyle.Flex;
        nextMenuBnt.style.display = DisplayStyle.None;
        urlScreen = root.Q<VisualElement>("url-screen");
        urlScreen.style.display = DisplayStyle.None;
        advancedConnectionScreen.style.display = DisplayStyle.None;
        sessionScreen.style.display = DisplayStyle.Flex;

        if (enterBtnAction == null)
        {
            enterBtnAction = () => masterServer.SendDataSession(sessionScreen.Q<TextField>("pinInput").value, (ser) => { serverResponses.Add(ser); updateResponse = true; Debug.Log(" update UI "); }
            ); 
            root.Q<Button>("pin-enter-btn").clickable.clicked += enterBtnAction;
        }
        nextStep = enterBtnAction;

    }

    private void UpdateSessionList()
    {
        var sessionList = sessionScreen.Q<ListView>("sessionsList");
        sessionList.Clear();
        foreach (MasterServerResponse.Server session in serverResponses)
        {
            VisualElement item = sessionEntry.CloneTree().Q<VisualElement>("session-entry");
            sessionList.Add(item);
            Debug.Log(item);
            item.Q<Label>("server-name").text = session.Name;
            item.Q<Label>("users-count").text = session.PlayerCount.ToString();

            item.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 1)
                {
                    SelectSession(item, session.Address, session.Port);
                }
            });

            item.RegisterCallback<MouseEnterEvent>(e =>
            {
                if (!item.ClassListContains("orange-background"))
                    foreach (var label in item.Q<VisualElement>("server-entry-btn").Children())
                    {

                        label.AddToClassList("orange-text");
                    }
            }
            );
            item.RegisterCallback<MouseLeaveEvent>(e =>
            {
                foreach (var label in item.Q<VisualElement>("server-entry-btn").Children())
                {
                    label.RemoveFromClassList("orange-text");
                }
            }
           );
        }
        serverResponses.Clear();

    }

    private Action currentNextButtonAction = null;
    private VisualElement selectedItem = null;
    private void SelectSession(VisualElement itemSelected, string ip, ushort port)
    {
        //TODO color the element
        if(selectedItem != null)
        {
            selectedItem.RemoveFromClassList("orange-background");
            selectedItem.RemoveFromClassList("black-txt");
            foreach (var label in selectedItem.Q<VisualElement>("server-entry-btn").Children())
            {
                label.AddToClassList("orange.text");
                label.RemoveFromClassList("black-txt");
            }
        }
        string ip_port = ip + ":" + port.ToString();
        Action nextAction = () => SetDomain(ip_port);
        if (currentNextButtonAction != null)
        {
            nextMenuBnt.clickable.clicked -= currentNextButtonAction;
        }
        nextMenuBnt.clickable.clicked += nextAction;
        nextMenuBnt.style.display = DisplayStyle.Flex;
        currentNextButtonAction = nextAction;

        //Color
        itemSelected.AddToClassList("orange-background");
        itemSelected.AddToClassList("black-txt");
        foreach (var label in itemSelected.Q<VisualElement>("server-entry-btn").Children())
        {
            label.RemoveFromClassList("orange.text");
            label.AddToClassList("black-txt");
        }
        selectedItem = itemSelected;
        //this.currentConnectionData.ip = env.ip;
        //DirectConnect();
    }

    #endregion

    #region Action

    private void Update()
    {
        if (updateBindSession)
        {
            BindSessionScreen();
            updateBindSession = false;
        }
        if (updateResponse)
        {
            UpdateSessionList();
            updateResponse = false;
        }
        CheckShortcuts();
    }

    private void ToggleDisplayElement(VisualElement visualElement)
    {
        visualElement.style.display = DisplayStyle.Flex == visualElement.style.display.value ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// Allows users to use escape and return keys to navigate through the launcher.
    /// </summary>
    private void CheckShortcuts()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (DialogueBoxElement.IsADialogueBoxDislayed)
                DialogueBoxElement.CloseDialogueBox(true);
            else
                nextStep?.Invoke();
        }
        else if (Input.GetKeyDown(InputLayoutManager.GetInputCode(InputLayoutManager.Input.MainMenuToggle)))
        {
            if (DialogueBoxElement.IsADialogueBoxDislayed)
                DialogueBoxElement.CloseDialogueBox(false);
            else
                previousStep?.Invoke();
        }
    }

    /// <summary>
    /// Gets the url and port written by users and stores them.
    /// </summary>
    private void SetDomain(string ip_port = "")
    {
        string url = string.IsNullOrEmpty(ip_port) ? IpInput.value.Trim() + ":" + PortInput.value.Trim() : ip_port;
        //string url = urlInput.value;
    
        if (string.IsNullOrEmpty(url))
        {
            //urlScreen.Q<Label>("url-error").style.display = DisplayStyle.Flex;
            //urlScreen.Q<Label>("url-error").text = "The domain is empty.";
        } else
        {
            currentConnectionData.ip = url;

            urlScreen.style.display = DisplayStyle.None;
            previousStep = ResetLauncher;
            DirectConnect();
        }
    }

    /// <summary>
    /// Initiates the connection to the environment.
    /// </summary>
    private void DirectConnect()
    {
        //currentConnectionData.environmentName
        UserPreferencesManager.StoreUserData(currentConnectionData);
      
        StartCoroutine(WaitReady());
    }


    private void SetServer(string serverName)
    {
        if (String.IsNullOrEmpty(serverName))
            return;
        serverName = serverName.Trim();
        if (root.Q<Toggle>("toggleRemember").value)
        {
            if (currentServerConnectionData != null)
                currentServerConnectionData.serverName = serverName;
            else
                currentServerConnectionData = new UserPreferencesManager.ServerData() { serverName = serverName };

            UserPreferencesManager.AddRegisterdeServerData(currentServerConnectionData);
        }
        Connect(serverName);
    }

    /// <summary>
    /// Initiates the connection to the forge master server.
    /// </summary>
    private void Connect(string serverName) 
    {
        
        Debug.Log("Try to connect to : " + serverName);
        masterServer.ConnectToMasterServer(() => { updateBindSession = true; }
            
           // () => masterServer.SendDataSession("test", (ser) => { Debug.Log(" update UI "); })
            , serverName);
        var text = root.Q<Label>("connectedText");
        Debug.Log(text);
        root.Q<Label>("connectedText").text = "Connected to : " + serverName;

    }

    /// <summary>
    /// Load the environment scene when it is ready.
    /// </summary>
    /// <returns></returns>
    IEnumerator WaitReady()
    {
        CursorHandler.Instance.Clear();
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);

        while (!ConnectionMenu.Exists)
            yield return new WaitForEndOfFrame();
        ConnectionMenu.Instance.Connect(currentConnectionData);
        SceneManager.UnloadSceneAsync(currentScene);
    }

    /// <summary>
    /// Reset the display of the launcher.
    /// </summary>
    private void ResetLauncher()
    {
        currentConnectionData = UserPreferencesManager.GetPreviousConnectionData();
        //favoriteConnectionData = UserPreferencesManager.GetFavoriteConnectionData();
        favoriteServerConnectionData = UserPreferencesManager.GetRegisteredServerData();
        //DisplayFavoriteEnvironments();
        DisplayRegisteredServers();
        currentServerConnectionData = UserPreferencesManager.GetPreviousServerData();

        librariesScreen.style.display = DisplayStyle.None;
        urlScreen.style.display = DisplayStyle.Flex;

        backMenuBnt.style.display = DisplayStyle.None;
        nextMenuBnt.style.display = DisplayStyle.None;
        advancedConnectionScreen.style.display = DisplayStyle.None;
        sessionScreen.style.display = DisplayStyle.None;


        previousStep = null;
        nextStep = () => SetDomain();
        urlInput.value = currentServerConnectionData.serverName;// currentConnectionData.ip;
    }

    /// <summary>
    /// Displays the favorites environments stored on  users' computers.
    /// </summary>
    [System.Obsolete("use favorite server not favorite environment")]
    private void DisplayFavoriteEnvironments()
    {
        favoriteEnvironmentSlider.ClearItems();
        foreach (var env in favoriteConnectionData)
        {
            var item = favoriteEnvItemTreeAsset.CloneTree().Q<VisualElement>("favorite-env-item");
            item.Q<Label>().text = string.IsNullOrEmpty(env.environmentName) ? env.ip : env.environmentName;
            item.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 2)
                {
                    this.currentConnectionData.ip = env.ip;
                    DirectConnect(); 
                }
            });
            item.Q<Button>("delete-item").clickable.clicked += () =>
            {
                DialogueBoxElement dialogue = dialogueBoxTreeAsset.CloneTree().Q<DialogueBoxElement>();
                dialogue.Setup(env.environmentName, "Delete this environment from favorites ?", "YES", "NO", (b) =>
                {
                    if (b)
                    {
                        favoriteConnectionData.Remove(favoriteConnectionData.Find(d => d.ip == env.ip));
                        UserPreferencesManager.StoreFavoriteConnectionData(favoriteConnectionData);
                        favoriteEnvironmentSlider.RemoveElement(item);
                    }
                },
                true);
                root.Add(dialogue);
            };
            favoriteEnvironmentSlider.AddElement(item);
        }
    }

    private void DisplayRegisteredServers()
    {
        favoriteEnvironmentSlider.ClearItems();
        bool isEmpty = true;
        foreach (UserPreferencesManager.ServerData env in favoriteServerConnectionData)
        {
            isEmpty = false;
            var item = favoriteEnvItemTreeAsset.CloneTree().Q<VisualElement>("favorite-env-item");
            item.Q<Label>().text = env.serverName;
            item.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 1)
                {
                    this.currentServerConnectionData.serverName = env.serverName;
                    //this.currentConnectionData.ip = env.ip;
                    //DirectConnect();// TODO
                    Connect(env.serverName);
                }
            });
            item.Q<Button>("delete-item").clickable.clicked += () =>
            {
                DialogueBoxElement dialogue = dialogueBoxTreeAsset.CloneTree().Q<DialogueBoxElement>();
                dialogue.Setup(env.serverName, "Delete this server from registered ?", "YES", "NO", (b) =>
                {
                    if (b)
                    {
                        favoriteServerConnectionData.Remove(favoriteServerConnectionData.Find(d => d.serverName == env.serverName));
                        UserPreferencesManager.StoreRegisteredServerData(favoriteServerConnectionData);
                        favoriteEnvironmentSlider.RemoveElement(item);
                    }
                },
                true);
                root.Add(dialogue);
            };
            favoriteEnvironmentSlider.AddElement(item);
        }
        if (isEmpty)
        {
            root.Q<VisualElement>("favorites-environement").style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Displays the libraries install on users' computers and allows tehm to unistall these libs.
    /// </summary>
    private void DisplayLibraries()
    {
        urlScreen.style.display = DisplayStyle.None;
        backMenuBnt.style.display = DisplayStyle.Flex;
        nextMenuBnt.style.display = DisplayStyle.None;
        librariesScreen.style.display = DisplayStyle.Flex;
        nextStep = null;
        previousStep = ResetLauncher;

        librariesList.Clear();

        Dictionary<string, List<UMI3DResourcesManager.DataFile>> libs = new Dictionary<string, List<UMI3DResourcesManager.DataFile>>();
        foreach (var lib in UMI3DResourcesManager.Libraries)
        {
            if (lib.applications != null)
                foreach (var app in lib.applications)
                {
                    if (!libs.ContainsKey(app)) libs[app] = new List<UMI3DResourcesManager.DataFile>();
                    libs[app].Add(lib);
                }
        }

        foreach (var app in libs)
        {
            foreach (var lib in app.Value)
            {
                // 1. Diplay lib name
                var entry = libraryEntryTreeAsset.CloneTree();
                entry.Q<Label>("library-name").text = lib.key;

                //2. Display environments which use this lib
                var dropdown = entry.Q<DropdownElement>();
                dropdown.SetUp(uiDocument, "dropdown-label-medium");
                dropdown.SetOptions(lib.applications);

                //3. Display lib size
                DirectoryInfo dirInfo = new DirectoryInfo(lib.path);
                double dirSize = DirSize(dirInfo) / Mathf.Pow(10, 6);
                dirSize = Math.Round(dirSize, 2);
                entry.Q<Label>("library-size").text = dirSize.ToString() + " mo"; ;


                //4.Bind the button to unistall this lib
                entry.Q<Button>("library-unistall").clickable.clicked += () =>
                {
                    DialogueBoxElement dialogue = dialogueBoxTreeAsset.CloneTree().Q<DialogueBoxElement>();
                    dialogue.Setup("Are you sure ... ?", "This library is required for " + app.Key + " environment", "YES", "NO", (b) =>
                    {
                        if (b)
                        {
                            lib.applications.Remove(app.Key);
                            UMI3DResourcesManager.RemoveLibrary(lib.key);
                            DisplayLibraries();
                        }
                    });
                    root.Add(dialogue);
                };
                librariesList.Add(entry);
            }
        }
    }

    /// <summary>
    /// Returns the size of a directory in bytes.
    /// </summary>
    /// <param name="d"></param>
    /// <returns></returns>
    public long DirSize(DirectoryInfo d)
    {
        long size = 0;
        // Add file sizes.
        FileInfo[] fis = d.GetFiles();
        foreach (FileInfo fi in fis)
        {
            size += fi.Length;
        }
        // Add subdirectory sizes.
        DirectoryInfo[] dis = d.GetDirectories();
        foreach (DirectoryInfo di in dis)
        {
            size += DirSize(di);
        }
        return size;
    }

    /// <summary>
    /// Resize some elements when the window is resized, to make the UI more responsive.
    /// </summary>
    /// <param name="e"></param>
    private void ResizeElements(GeometryChangedEvent e)
    {
        float height = e.newRect.height * 0.16f;
        umiLogo.style.height = height;
        umiLogo.style.minHeight = height;

        umiLogo.style.marginBottom = e.newRect.height * 0.08f;
    }

    #endregion
}
